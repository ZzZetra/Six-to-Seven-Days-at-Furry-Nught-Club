using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Ночь в сцене: создаёт мозги фурри (Night из SSDFNC.Core) с уровнями из NightSettings, каждый кадр
// их тикает и связывает с офисом: дверь, дэнс пол, звук, генератор, камеры и кормление Дира.
// Нападение — экран смерти и смена заново. Нова на дэнс полу — мог-батл по фазам (MogBattle): она поворачивает
// игрока к дэнс полу, управление отключено, остальные фурри, голод Дира и бензин стоят (Night.Frozen).
// Звуки: Доли у двери и у генератора рычит одинаково (не понять, где он); слив бензина булькает.
public class NightDirector : MonoBehaviour, IOffice
{
    const float DeathScreenTime = 2.5f;
    const float AvatarSpacing = 0.9f; // м между фурри, если их несколько в одном месте

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

    public Text warning;          // «ДИР ГОЛОДЕН!» и т. п. сверху экрана
    public GameObject deathPanel;
    public Text deathText;
    public AudioSource alarm;

    public Night Night { get; private set; }

    AudioClip hungerClip;
    AudioSource doliLoop;  // рычание Доли: у двери и у генератора один и тот же звук, не объёмный
    AudioSource drainLoop; // бульканье, пока сливают бензин
    float feedHeld; // сколько уже держат «Покормить»

    void Awake()
    {
        var levels = NightSettings.Levels; // из вкладки «Настройка ночи» (по умолчанию — Difficulty.cs)
        Night = new Night(this, levels, Balance.ShiftDuration, Random.Range(1, int.MaxValue));
        Night.PlayerKilled += OnKilled;
        Night.MogBattleStarted += OnMogBattle;
        Night.FurryMoved += (f, from, to) => Debug.Log($"[SSDFNC] {f.Name}: {Rooms.Name(from)} → {Rooms.Name(to)}");
        Night.DirStarving += _ => alarm.PlayOneShot(hungerClip);
        hungerClip = Tones.Beep("DirHunger", 440f, 0.8f, 0.3f);
        doliLoop = NewLoop(Tones.Growl("DoliGrowl"));
        drainLoop = NewLoop(Tones.Glug("FuelDrain"));
        Night.GeneratorSabotaged += r => Debug.Log($"[SSDFNC] {r.Name} у генератора: {r.SabotageName}");
        Debug.Log($"[SSDFNC] Ночь: сложность {NightSettings.Describe()}, уровни Дир/Стрей/Нова/Дали/Доли");
        deathPanel.SetActive(false);
    }

    void OnEnable() => sound.Fired += OnSound;
    void OnDisable() => sound.Fired -= OnSound;
    void OnSound() => Night.SoundFired();

    void Update()
    {
        Night.Tick(Time.deltaTime);
        PlaceAvatars();

        var doli = Night.Doli;
        SetLoop(doliLoop, !Night.Over && doli.Active && (doli.Room == Room.Door || doli.Room == Room.Generator));
        SetLoop(drainLoop, !Night.Over && (Night.Dali.Draining || doli.Draining));
        warning.text = Night.Dir.Starving && !Night.Over
            ? $"ДИР ГОЛОДЕН! {Mathf.CeilToInt(Balance.DirGraceTime - Night.Dir.StarvingFor)}"
            : "";
    }

    // ── IOffice: что видят и делают фурри ──
    public bool DoorClosed => door.IsClosed;
    public bool DanceFloorOn => danceFloor.IsActive;
    public void StallGenerator() => generator.Stall();
    public void DamageGenerator(float hp) => generator.Damage(hp);
    public void DrainFuel(float amount) => generator.Drain(amount);

    // ── Камеры и кормление ──
    public string Describe(Room room) => Night.Describe(room);
    public bool DirActive => Night.Dir.Active;
    public float FeedProgress => feedHeld / Balance.DirFeedHoldTime;

    // Держат «Покормить» (вызывается каждый кадр, пока кнопка зажата). Выдержали — Дир накормлен
    public void HoldFeed(float dt)
    {
        feedHeld += dt;
        if (feedHeld < Balance.DirFeedHoldTime) return;
        feedHeld = 0f;
        Night.FeedDir();
        Debug.Log("[SSDFNC] Дир накормлен");
    }

    public void ReleaseFeed() => feedHeld = 0f;

    void PlaceAvatars()
    {
        int nDoor = 0, nDance = 0, nPassage = 0, nGen = 0; // сколько фурри уже стоит в этом месте
        foreach (var f in Night.All)
        {
            var avatar = avatars[(int)f.Id];
            if (avatar == null) continue;
            if (!f.Active) { avatar.Hide(); continue; }
            switch (f.Room)
            {
                case Room.Door: avatar.Show(Spot(atDoor, nDoor++)); break;
                case Room.DanceFloor: avatar.Show(Spot(onDanceFloor, nDance++)); break;
                case Room.PassageG: avatar.Show(Spot(inPassage, nPassage++)); break;
                case Room.Generator: avatar.Show(Spot(atGenerator, nGen++)); break;
                default: avatar.Hide(); break;
            }
        }
    }

    AudioSource NewLoop(AudioClip clip)
    {
        var source = gameObject.AddComponent<AudioSource>();
        source.clip = clip;
        source.loop = true;
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        return source;
    }

    static void SetLoop(AudioSource source, bool on)
    {
        if (on && !source.isPlaying) source.Play();
        else if (!on && source.isPlaying) source.Stop();
    }

    static Vector3 Spot(Transform anchor, int index) => anchor.position + anchor.right * (index * AvatarSpacing);

    void OnMogBattle(NovaAI nova)
    {
        Debug.Log("[SSDFNC] Нова на дэнс полу: мог-батл! Остальных отбросило на комнату назад");
        SetControls(false);
        look.FaceForward(); // Нова разворачивает игрока к дэнс полу (от генератора — назад в офис)
        generator.Paused = true;
        danceFloor.SetForced(true);
        PlaceAvatars();     // Нова уже стоит на дэнс полу, когда стартует анимация появления
        var body = avatars[(int)FurryId.Nova] != null ? avatars[(int)FurryId.Nova].body : null;
        battle.Begin(nova.Wins, body, won =>
        {
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
        deathText.text = $"{who.Name.ToUpper()}\n{how}";
        StartCoroutine(RestartShift());
    }

    // Проигрыш — смена начинается заново
    IEnumerator RestartShift()
    {
        yield return new WaitForSecondsRealtime(DeathScreenTime);
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }
}
