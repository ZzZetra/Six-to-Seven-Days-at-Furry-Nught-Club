using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Ночь в сцене: создаёт мозги фурри (Night из SSDFNC.Core) с уровнями ночи (NightProgress: ночь 1–7 из Nights.cs
// или «Своя ночь» из NightSettings), каждый кадр
// их тикает и связывает с офисом: дверь, дэнс пол, звук, генератор, камеры и кормление Дира.
// Нападение — экран смерти: голова фурри бросается на игрока (DeathScreamer, звук «Скример») поверх немного
// затемнённой игры, потом экран гаснет в чёрный и на нём — кто и как убил, кнопки «Начать заново» и «В меню»;
// Дир — сразу чёрный экран «СКОНЧАЛСЯ», через секунду кнопки.
// В офисе фурри видно (FurryAvatar): Доли у двери — нет (не рисуем), Стрей у двери выглядывает из-за края проёма,
// Дали в проходе Г — её рука в вентиляции генераторной (пользователь, 2026-11-06). Нова на дэнс полу — мог-батл по фазам (MogBattle): она поворачивает
// игрока к дэнс полу, управление отключено, остальные фурри, голод Дира и бензин стоят (Night.Frozen).
// Звуки (GameSounds, меняются в паузе → «Звуки»): фон ночи; Доли, подойдя к двери или к генератору, один раз рычит
// одинаково (не понять, где она); слив бензина булькает; фурри подошёл вплотную (к двери, дэнс полу, генератору) — его звук; за закрытой
// дверью стоит фурри — удар об дверь, но не раньше DoorHitDelay после того, как её закрыли (пришёл позже — сразу).
// Фон ночи вступает через AmbientDelay, а не с первой секунды. Сбой монитора (Night.MonitorRebootStarted): звук
// «Сбой монитора» («встроенный» — monitorRebootClip, если он есть), перезагрузку рисует CameraMonitor.
public class NightDirector : MonoBehaviour, IOffice
{
    const float DeathButtonsDelay = 1f; // с до кнопок на экране «СКОНЧАЛСЯ» (Дир) — чтобы не нажать по инерции
    const float ScreamerTime = 1.3f;    // с: голова фурри на экране
    const float DeathFadeTime = 0.6f;   // с: потом экран гаснет в чёрный
    const float DeathTextDelay = 0.3f;  // с на чёрном до причины смерти и кнопок
    const float AvatarSpacing = 1.2f; // м между фурри, если их несколько в одном месте (Нова с руками ~1.2 м)
    const float AmbientDelay = 5f;    // с от начала ночи до фона (эмбиэнта)
    const float DoorHitDelay = 2f;    // с после закрытия двери до удара фурри об неё
    static readonly Color ScreamerDim = new Color(0f, 0f, 0f, 0.55f); // смерть от фурри: игру немного затемняет
    static readonly Color DirDeath = Color.black;                      // смерть от Дира: чёрный экран

    public Generator generator;
    public Door door;
    public DanceFloor danceFloor;
    public SoundSignal sound;
    public CameraLook look;
    public SpaceAction space;
    public ShiftTimer shift;
    public MogBattle battle;

    public FurryAvatar[] avatars; // по FurryId; у Дира — нет (его видно только на камере)
    public Transform atDoor, onDanceFloor, inPassage, atGenerator; // где показывать фурри в офисе
    public Transform strayPeek;   // Стрей у двери: за стеной сбоку от проёма, наклонён в него
    public FurryAvatar daliHand;  // Дали в проходе Г: рука из вентиляции в генераторной

    // Надписи «ДИР ГОЛОДЕН!» сверху экрана нет (пользователь, 2026-11-06) — о голоде говорит звук «Дир голоден»
    public GameObject deathPanel;
    public Image deathBackground;         // фон экрана смерти (цвет ставится при смерти)
    public DeathScreamer screamer;        // голова фурри на экране смерти
    public Text deathText;
    public GameObject deathButtons;       // «Начать заново» и «В меню»
    public Button deathRestartButton, deathMenuButton;
    public string menuScene = "MainMenu";
    public AudioSource alarm;
    public AudioClip monitorRebootClip; // звук сбоя монитора; сборщик берёт Assets/Audio/MonitorReboot.*, пока его нет

    public Night Night { get; private set; }
    public int Feeds { get; private set; } // сколько раз за ночь накормили Дира (для итогов ночи)

    AudioClip hungerClip;
    SoundBinding drainLoop; // бульканье, пока сливают бензин
    SoundBinding ambient;   // фон ночи
    float nightTime;        // с от начала ночи (на паузе стоит)
    float doorClosedAt = -99f, hitAt = -1f; // когда закрыли дверь; когда ударить (меньше нуля — не надо)
    float feedHeld; // сколько уже держат «Покормить»

    void Awake()
    {
        var levels = NightProgress.Levels; // ночь 1–7 — из Nights.cs, своя ночь — из вкладки «Своя ночь»
        Night = new Night(this, levels, Balance.ShiftDuration, Random.Range(1, int.MaxValue),
            NightProgress.MonitorRebootChancePerMinute);
        Night.PlayerKilled += OnKilled;
        Night.MogBattleStarted += OnMogBattle;
        Night.FurryMoved += OnFurryMoved;
        Night.DirStarving += _ => GameSounds.Play(SoundSlot.DirHunger, alarm);
        hungerClip = Tones.Beep("DirHunger", 440f, 0.8f, 0.3f);
        GameSounds.SetBuiltin(SoundSlot.DirHunger, hungerClip);
        GameSounds.SetBuiltin(SoundSlot.DoliGrowl, Tones.Growl("DoliGrowl"));
        GameSounds.SetBuiltin(SoundSlot.FuelDrain, Tones.Glug("FuelDrain"));
        GameSounds.SetBuiltin(SoundSlot.MonitorReboot, monitorRebootClip);
        drainLoop = NewLoop(SoundSlot.FuelDrain);
        ambient = NewLoop(SoundSlot.Ambient);
        Night.GeneratorSabotaged += r => Debug.Log($"[SSDFNC] {r.Name} у генератора: {r.SabotageName}");
        Night.MonitorRebootStarted += OnMonitorReboot;
        Debug.Log($"[SSDFNC] {NightProgress.Name}: уровни Дир/Стрей/Нова/Дали/Доли {string.Join("/", levels)}, " +
                  $"сбой монитора {NightProgress.MonitorRebootChancePerMinute * 100f:0}%/мин");
        deathPanel.SetActive(false);
        deathRestartButton.onClick.AddListener(() =>
            SceneLoader.Load(SceneManager.GetActiveScene().name, "Ещё одна попытка…"));
        deathMenuButton.onClick.AddListener(() => SceneLoader.Load(menuScene, "Возвращаемся в меню…"));
    }

    void OnEnable()
    {
        sound.Fired += OnSound;
        door.ActiveChanged += OnDoor;
    }

    void OnDisable()
    {
        sound.Fired -= OnSound;
        door.ActiveChanged -= OnDoor;
    }

    void OnSound() => Night.SoundFired();

    void Update()
    {
        Night.Tick(Time.deltaTime);
        PlaceAvatars();
        nightTime += Time.deltaTime;
        if (hitAt >= 0f && nightTime >= hitAt)
        {
            hitAt = -1f;
            if (door.IsClosed && FurryAtDoor()) GameSounds.Play(SoundSlot.DoorHit);
        }

        var doli = Night.Doli;
        drainLoop.SetPlaying(!Night.Over && (Night.Dali.Draining || doli.Draining));
        ambient.SetPlaying(!Night.Over && nightTime >= AmbientDelay);
    }

    // ── IOffice: что видят и делают фурри ──
    public bool DoorClosed => door.IsClosed;
    public bool DanceFloorOn => danceFloor.IsActive;
    public bool MonitorPowered => generator.IsPowered;
    public void StallGenerator() => generator.Stall();
    public void DamageGenerator(float hp) => generator.Damage(hp);
    public void DrainFuel(float amount) => generator.Drain(amount);

    // ── Камеры и кормление ──
    public string Describe(Room room) => Night.Describe(room);
    public bool MonitorRebooting => Night != null && Night.MonitorRebooting; // монитор включается раньше ночи
    public float MonitorRebootLeft => Night != null ? Night.MonitorRebootLeft : 0f;
    public bool DirActive => Night.Dir.Active;
    public float FeedProgress => feedHeld / Balance.DirFeedHoldTime;

    // Держат «Покормить» (вызывается каждый кадр, пока кнопка зажата). Выдержали — Дир накормлен
    public void HoldFeed(float dt)
    {
        feedHeld += dt;
        if (feedHeld < Balance.DirFeedHoldTime) return;
        feedHeld = 0f;
        Night.FeedDir();
        Feeds++;
        Debug.Log("[SSDFNC] Дир накормлен");
    }

    public void ReleaseFeed() => feedHeld = 0f;

    void PlaceAvatars()
    {
        int nDoor = 0, nDance = 0, nPassage = 0, nGen = 0; // сколько фурри уже стоит в этом месте
        bool hand = false;
        foreach (var f in Night.All)
        {
            var avatar = avatars[(int)f.Id];
            if (avatar == null) continue;
            if (!f.Active) { avatar.Hide(); continue; }
            switch (f.Room)
            {
                case Room.Door:
                    if (f.Id == FurryId.Doli) avatar.Hide(); // Доли у двери не видно
                    else if (f.Id == FurryId.Stray && strayPeek != null) avatar.Show(strayPeek);
                    else avatar.Show(Spot(atDoor, nDoor++));
                    break;
                case Room.DanceFloor: avatar.Show(Spot(onDanceFloor, nDance++)); break;
                case Room.PassageG: // проход Г — это вентиляция: Дали видно по руке из короба
                    hand |= f.Id == FurryId.Dali && daliHand != null;
                    if (f.Id == FurryId.Dali && daliHand != null) avatar.Hide();
                    else avatar.Show(Spot(inPassage, nPassage++));
                    break;
                case Room.Generator: avatar.Show(Spot(atGenerator, nGen++)); break;
                default: avatar.Hide(); break;
            }
        }
        if (daliHand == null) return;
        if (hand) daliHand.ShowHere();
        else daliHand.Hide();
    }

    SoundBinding NewLoop(SoundSlot slot)
    {
        var source = gameObject.AddComponent<AudioSource>();
        source.spatialBlend = 0f;
        return GameSounds.Bind(source, slot);
    }

    // Фурри подошёл вплотную — его звук; упёрся в закрытую дверь — удар
    void OnFurryMoved(Furry f, Room from, Room to)
    {
        Debug.Log($"[SSDFNC] {f.Name}: {Rooms.Name(from)} → {Rooms.Name(to)}");
        if (to == Room.Door && door.IsClosed) ScheduleHit();
        if (to != Room.Door && to != Room.DanceFloor && to != Room.Generator) return;
        switch (f.Id)
        {
            case FurryId.Doli:
                GameSounds.Play(SoundSlot.DoliNear);
                // рычит один раз, когда подошла (у двери и у генератора одинаково — не понять, где она); раньше —
                // по кругу всё время, пока там стоит (пользователь, 2026-11-06: «только один раз»)
                if (to != Room.DanceFloor) GameSounds.Play(SoundSlot.DoliGrowl);
                break;
            case FurryId.Stray: GameSounds.Play(SoundSlot.StrayNear); break;
            case FurryId.Dali: GameSounds.Play(SoundSlot.DaliNear); break;
            case FurryId.Nova: GameSounds.Play(SoundSlot.NovaNear); break;
        }
    }

    // Дверь закрыли, когда за ней уже стоит фурри, — через DoorHitDelay он в неё ударит. Открыли — удара не будет
    void OnDoor(bool closed)
    {
        if (!closed)
        {
            hitAt = -1f;
            return;
        }
        doorClosedAt = nightTime;
        if (Night != null && FurryAtDoor()) ScheduleHit();
    }

    // Удар — не раньше DoorHitDelay после закрытия; если фурри пришёл позже — сразу
    void ScheduleHit()
    {
        float at = Mathf.Max(nightTime, doorClosedAt + DoorHitDelay);
        if (hitAt < 0f || at < hitAt) hitAt = at;
    }

    bool FurryAtDoor()
    {
        foreach (var f in Night.All)
            if (f.Active && f.Room == Room.Door) return true;
        return false;
    }

    static Vector3 Spot(Transform anchor, int index) => anchor.position + anchor.right * (index * AvatarSpacing);

    void OnMonitorReboot()
    {
        Debug.Log($"[SSDFNC] Сбой монитора: перезагрузка {Balance.MonitorRebootTime:0} с (корм Дира {Night.Dir.Food * 100f:0}%)");
        GameSounds.Play(SoundSlot.MonitorReboot, alarm);
    }

    void OnMogBattle(NovaAI nova)
    {
        Debug.Log("[SSDFNC] Нова на дэнс полу: мог-батл! Остальных отбросило на комнату назад");
        SetControls(false);
        look.FaceForward(); // Нова разворачивает игрока к дэнс полу (от генератора — назад в офис)
        generator.Paused = true;
        danceFloor.SetForced(true);
        PlaceAvatars();     // Нова уже стоит на дэнс полу, когда начинается фаза появления
        battle.KeyHints = NightProgress.Current == 1; // в 1-ю ночь на чеках написано, что нажимать
        look.Sway = true; // голова чуть ходит за мышью, пока идёт батл
        battle.Begin(nova.Wins, won =>
        {
            look.Sway = false;
            generator.Paused = false;
            danceFloor.SetForced(false);
            if (won) SetControls(true); // проиграл — дальше экран смерти, управление не нужно
            Night.FinishMogBattle(won);
        });
    }

    // Камера остаётся включённой, чтобы её можно было повернуть за игрока, — отключается только ввод
    void SetControls(bool on)
    {
        look.InputLocked = !on;
        space.enabled = on;
    }

    void OnKilled(Furry who, string how)
    {
        Debug.Log($"[SSDFNC] Смерть: {who.Name} {how}");
        SetControls(false);
        shift.enabled = false;
        deathPanel.SetActive(true);
        deathButtons.SetActive(false);
        bool head = who.Id != FurryId.Dir && screamer != null && screamer.Has(who.Id);
        if (deathBackground != null) deathBackground.color = head ? ScreamerDim : DirDeath;
        string reason = $"{who.Name.ToUpper()}\n{how}";
        deathText.text = who.Id == FurryId.Dir ? "СКОНЧАЛСЯ" : head ? "" : reason;
        if (head)
        {
            screamer.Show(who.Id);
            GameSounds.Play(SoundSlot.Screamer, ui: true); // ui — не замолкает, когда всё встанет
        }
        StartCoroutine(DeathSequence(head, reason));
    }

    // Скример ScreamerTime, потом экран гаснет в чёрный (DeathFadeTime) и на чёрном — причина смерти и кнопки
    // (пользователь, 2026-11-06). Без головы (Дир) — через DeathButtonsDelay кнопки. Тогда же всё замирает (GameFlow)
    IEnumerator DeathSequence(bool head, string reason)
    {
        if (head)
        {
            yield return new WaitForSecondsRealtime(ScreamerTime);
            var from = deathBackground != null ? deathBackground.color : DirDeath;
            for (float t = 0f; t < DeathFadeTime; t += Time.unscaledDeltaTime)
            {
                float k = t / DeathFadeTime;
                if (deathBackground != null) deathBackground.color = Color.Lerp(from, DirDeath, k);
                screamer.SetAlpha(1f - k);
                yield return null;
            }
            if (deathBackground != null) deathBackground.color = DirDeath;
            screamer.Hide();
            yield return new WaitForSecondsRealtime(DeathTextDelay);
            deathText.text = reason;
        }
        else yield return new WaitForSecondsRealtime(DeathButtonsDelay);
        GameFlow.EndNight();
        deathButtons.SetActive(true);
    }
}
