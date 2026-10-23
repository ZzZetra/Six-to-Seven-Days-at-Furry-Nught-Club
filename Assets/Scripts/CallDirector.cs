using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// Звонки клиентов за смену. В начале ночи составляет расписание (табличка над столом) и план звонков (CallPlan).
// Звонок: телефон звенит Balance.CallRingTime секунд → клик по телефону → реплика клиента → клик ЛКМ где угодно →
// варианты ответа (мышью или 1–3) → реакция клиента, за верный ответ деньги (Wallet) → гудки.
// Клиент не знает правильного ответа: реагирует на то, что ему сказали. Ошибка видна только по деньгам.
// Окно звонка видно только когда смотришь вперёд; отвернулся — разговор ждёт, пока не повернёшься обратно.
// Пока идёт мог-батл, звонки стоят. Ночь во время разговора не останавливается.
public class CallDirector : MonoBehaviour
{
    enum State { Idle, Ringing, ClientLine, Options, Reaction }

    const string HangUpLine = "Алло?.. Алло?! Ну и сервис… (гудки)";
    static readonly Color HangUpColor = new Color(1f, 0.45f, 0.4f);

    public NightDirector night;
    public CameraLook look;
    public Phone phone;
    public CallDialog dialog;
    public ScheduleBoard board;

    public Schedule Schedule { get; private set; }
    public Call Current { get; private set; }
    public int Answered { get; private set; }
    public int Right { get; private set; }
    public int Missed { get; private set; }
    public int Earned { get; private set; }
    public float Clock => clock;
    public float[] Plan => plan;

    System.Random rng;
    List<CallTemplate> templates;
    float[] plan;
    int next;
    float clock, timer, patience;
    int pickedFrame = -1;
    State state;

    void Start()
    {
        rng = new System.Random(Random.Range(1, int.MaxValue));
        Schedule = Schedule.Generate(CallData.LoadSchedule(), rng);
        board.Show(Schedule);
        templates = CallData.LoadDialogues();
        plan = templates.Count > 0 ? CallPlan.Times(Balance.ShiftDuration, rng) : new float[0];
        phone.PickedUp += PickUp;
        dialog.Chosen += Choose;
        dialog.Close();
        Debug.Log($"[SSDFNC] Звонки: диалогов {templates.Count}, звонков за ночь {plan.Length} " +
                  $"(в {string.Join(", ", System.Array.ConvertAll(plan, t => $"{t:0} с"))})");
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
                if (next < plan.Length && clock >= plan[next])
                {
                    next++;
                    Ring();
                }
                break;
            case State.Ringing:
                timer -= dt;
                if (timer <= 0f)
                {
                    Missed++;
                    Debug.Log("[SSDFNC] Звонок пропущен");
                    End();
                }
                break;
            case State.ClientLine:
                if (Impatient(dt)) break;
                var mouse = Mouse.current;
                if (dialog.Visible && mouse != null && mouse.leftButton.wasPressedThisFrame && Time.frameCount != pickedFrame)
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
        state = State.ClientLine;
        patience = Balance.CallPatience;
        pickedFrame = Time.frameCount;
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
        state = State.Idle;
    }

    static int PressedDigit()
    {
        var kb = Keyboard.current;
        if (kb == null) return -1;
        if (kb.digit1Key.wasPressedThisFrame || kb.numpad1Key.wasPressedThisFrame) return 0;
        if (kb.digit2Key.wasPressedThisFrame || kb.numpad2Key.wasPressedThisFrame) return 1;
        if (kb.digit3Key.wasPressedThisFrame || kb.numpad3Key.wasPressedThisFrame) return 2;
        return -1;
    }
}
