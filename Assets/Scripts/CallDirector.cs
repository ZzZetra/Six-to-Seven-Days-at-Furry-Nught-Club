using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// Звонки за смену. В начале ночи составляет расписание (табличка) и план звонков: клиенты (CallPlan, сколько — по ночи,
// Nights.cs) и начальство (BossCallPlan, тексты — Calls/Boss/night_N.json). Ещё звонки по условию — Request(условие).
// Начальство и звонки по условию — монолог: ЛКМ — следующая реплика, после последней гудки; можно не брать трубку.
// Звонок: телефон звенит Balance.CallRingTime секунд → клик по телефону → реплика клиента → клик ЛКМ где угодно →
// варианты ответа (мышью или 1–3) → реакция клиента, за верный ответ деньги (Wallet) → гудки.
// Клиент не знает правильного ответа: реагирует на то, что ему сказали. Ошибка видна только по деньгам.
// Окно звонка видно только когда смотришь вперёд; отвернулся — разговор ждёт, пока не повернёшься обратно.
// Пока идёт мог-батл, звонки стоят. Ночь во время разговора не останавливается.
public class CallDirector : MonoBehaviour
{
    enum State { Idle, Ringing, ClientLine, Options, Reaction, Talk }

    // Что позвонит: клиент (talk == null) или монолог — начальство или звонок по условию
    class Planned
    {
        public float time;
        public PhoneTalk talk;
        public string kind;
    }

    const string HangUpLine = "Алло?.. Алло?! Ну и сервис… (гудки)";
    static readonly Color HangUpColor = new Color(1f, 0.45f, 0.4f);

    public NightDirector night;
    public CameraLook look;
    public Phone phone;
    public CallDialog dialog;
    public ScheduleBoard board;

    public Schedule Schedule { get; private set; }
    public Call Current { get; private set; }
    public PhoneTalk CurrentTalk { get; private set; } // идёт монолог (начальство, звонок по условию)
    public int Answered { get; private set; }
    public int Right { get; private set; }
    public int Missed { get; private set; }
    public int Earned { get; private set; }
    public float Clock => clock;
    public float[] Plan => plan;

    System.Random rng;
    List<CallTemplate> templates;
    float[] plan;
    readonly List<Planned> queue = new List<Planned>(); // по времени
    float clock, timer, patience;
    int talkLine;
    int pickedFrame = -1;
    State state;

    void Start()
    {
        rng = new System.Random(Random.Range(1, int.MaxValue));
        Schedule = Schedule.Generate(CallData.LoadSchedule(), rng);
        board.Show(Schedule);
        templates = CallData.LoadDialogues();
        plan = templates.Count > 0
            ? CallPlan.Times(Balance.ShiftDuration, rng, NightProgress.CallsMin, NightProgress.CallsMax)
            : new float[0];
        foreach (float t in plan) queue.Add(new Planned { time = t, kind = "клиент" });
        var boss = CallData.LoadBossCalls(NightProgress.Current);
        var bossTimes = BossCallPlan.Times(boss, Balance.ShiftDuration, rng);
        for (int i = 0; i < boss.Length; i++) queue.Add(new Planned { time = bossTimes[i], talk = boss[i], kind = "начальство" });
        queue.Sort((a, b) => a.time.CompareTo(b.time));
        phone.PickedUp += PickUp;
        dialog.Chosen += Choose;
        dialog.Close();
        Debug.Log($"[SSDFNC] Звонки: диалогов {templates.Count}, клиентов за ночь {plan.Length}, начальства {boss.Length} " +
                  $"({string.Join(", ", queue.ConvertAll(p => $"{p.kind} {p.time:0} с"))})");
    }

    void Update()
    {
        var n = night.Night;
        if (n == null) return;
        if (n.Over)
        {
            if (state != State.Idle) End();
            return;
        }

        dialog.SetVisible(!n.Frozen && look.IsLooking(CameraLook.Side.Forward));
        phone.Pause(n.Frozen);
        if (n.Frozen) return; // мог-батл: звонки ждут

        float dt = Time.deltaTime;
        clock += dt;
        switch (state)
        {
            case State.Idle:
                if (queue.Count > 0 && clock >= queue[0].time)
                {
                    var call = queue[0];
                    queue.RemoveAt(0);
                    if (call.talk != null) RingTalk(call.talk);
                    else Ring();
                }
                break;
            case State.Ringing:
                timer -= dt;
                if (timer <= 0f)
                {
                    if (CurrentTalk == null) Missed++; // начальство и звонки по условию — без последствий
                    Debug.Log(CurrentTalk == null ? "[SSDFNC] Звонок пропущен" : $"[SSDFNC] Звонок пропущен: {CurrentTalk.caller}");
                    End();
                }
                break;
            case State.Talk:
                var click = Mouse.current;
                if (dialog.Visible && !GameFlow.InputBlocked && click != null && click.leftButton.wasPressedThisFrame && Time.frameCount != pickedFrame)
                {
                    talkLine++;
                    if (talkLine >= CurrentTalk.lines.Length) End();
                    else ShowTalkLine();
                }
                break;
            case State.ClientLine:
                if (Impatient(dt)) break;
                var mouse = Mouse.current;
                if (dialog.Visible && !GameFlow.InputBlocked && mouse != null && mouse.leftButton.wasPressedThisFrame && Time.frameCount != pickedFrame)
                    ShowOptions();
                break;
            case State.Options:
                if (Impatient(dt)) break;
                int key = PressedDigit();
                if (dialog.Visible && key >= 0 && key < Current.Options.Length) Choose(key);
                break;
            case State.Reaction:
                timer -= dt;
                if (timer <= 0f) End();
                break;
        }
    }

    // Для панели F1: позвонить прямо сейчас. Не выйдет, если телефон уже занят, идёт мог-батл или нет диалогов.
    // Звонки по плану от этого не сдвигаются
    public bool RingNow()
    {
        var n = night.Night;
        if (state != State.Idle || templates.Count == 0 || n == null || n.Over || n.Frozen) return false;
        Ring();
        return Current != null;
    }

    // Звонок по условию (какие условия — решим потом): позвонит через Balance.CallTriggerDelay, как только телефон
    // свободен. Текст — Calls/Triggered/<trigger>.json; файла нет — заглушка
    public void Request(string trigger)
    {
        var n = night.Night;
        if (n == null || n.Over) return;
        queue.Add(new Planned { time = clock + Balance.CallTriggerDelay, talk = CallData.LoadTriggered(trigger), kind = "условие " + trigger });
        queue.Sort((a, b) => a.time.CompareTo(b.time));
        Debug.Log($"[SSDFNC] Звонок по условию «{trigger}» — через {Balance.CallTriggerDelay:0} с");
    }

    // Для панели F1: позвонить начальством прямо сейчас (первый звонок этой ночи; нет текста — заглушка)
    public bool RingBossNow()
    {
        var n = night.Night;
        if (state != State.Idle || n == null || n.Over || n.Frozen) return false;
        var boss = CallData.LoadBossCalls(NightProgress.Current);
        RingTalk(boss.Length > 0 ? boss[0] : new PhoneTalk { lines = new[] { "(звонок начальства — текста пока нет)" } });
        return true;
    }

    void RingTalk(PhoneTalk talk)
    {
        CurrentTalk = talk;
        state = State.Ringing;
        timer = Balance.CallRingTime;
        phone.StartRinging();
        Debug.Log($"[SSDFNC] Звонит телефон: {talk.caller}");
    }

    void ShowTalkLine()
    {
        bool last = talkLine == CurrentTalk.lines.Length - 1;
        dialog.ShowLine(CurrentTalk.caller, CurrentTalk.lines[talkLine], last ? "ЛКМ — положить трубку" : "ЛКМ — дальше", Color.white);
    }

    void Ring()
    {
        for (int attempt = 0; attempt < 10 && Current == null; attempt++)
            Current = CallBuilder.Build(templates[rng.Next(templates.Count)], Schedule, rng);
        if (Current == null) return;
        state = State.Ringing;
        timer = Balance.CallRingTime;
        phone.StartRinging();
        Debug.Log($"[SSDFNC] Звонит телефон: {Current.TemplateId}");
    }

    void PickUp()
    {
        if (state != State.Ringing) return;
        phone.StopRinging(offHook: true);
        pickedFrame = Time.frameCount;
        if (CurrentTalk != null)
        {
            state = State.Talk;
            talkLine = 0;
            ShowTalkLine();
            return;
        }
        state = State.ClientLine;
        patience = Balance.CallPatience;
        dialog.ShowLine(Current.Caller, Current.ClientLine, "ЛКМ — ответить", Color.white);
    }

    void ShowOptions()
    {
        state = State.Options;
        dialog.ShowOptions(Current.Options);
    }

    void Choose(int index)
    {
        if (state != State.Options) return;
        Answered++;
        bool right = index == Current.Correct;
        string reaction = Current.Reactions[index];
        if (right)
        {
            Right++;
            Earned += Current.Reward;
            Wallet.Add(Current.Reward);
            GameSounds.Play(SoundSlot.CallRight);
            reaction += $"   <color=#8CFF8C>+{Current.Reward} $</color>";
        }
        dialog.ShowLine(Current.Caller, reaction, null, Color.white);
        Debug.Log($"[SSDFNC] Ответ на звонок: «{Current.Options[index]}» — {(right ? $"верно, +{Current.Reward} $" : "неверно")}");
        state = State.Reaction;
        timer = Balance.CallReactionTime;
    }

    // Клиент ждёт ответа не больше Balance.CallPatience — потом бросает трубку
    bool Impatient(float dt)
    {
        patience -= dt;
        if (patience > 0f) return false;
        Missed++;
        Debug.Log("[SSDFNC] Клиент не дождался ответа");
        dialog.ShowLine(Current.Caller, HangUpLine, null, HangUpColor);
        state = State.Reaction;
        timer = Balance.CallReactionTime;
        return true;
    }

    void End()
    {
        phone.StopRinging(offHook: false);
        dialog.Close();
        Current = null;
        CurrentTalk = null;
        state = State.Idle;
    }

    static int PressedDigit()
    {
        var kb = Keyboard.current;
        if (kb == null || GameFlow.InputBlocked) return -1;
        if (kb.digit1Key.wasPressedThisFrame || kb.numpad1Key.wasPressedThisFrame) return 0;
        if (kb.digit2Key.wasPressedThisFrame || kb.numpad2Key.wasPressedThisFrame) return 1;
        if (kb.digit3Key.wasPressedThisFrame || kb.numpad3Key.wasPressedThisFrame) return 2;
        return -1;
    }
}
