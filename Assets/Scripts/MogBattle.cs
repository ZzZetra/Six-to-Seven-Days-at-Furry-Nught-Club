using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Атака Новы (мог-батл) по фазам и по раскладке под музыку (MogChart из StreamingAssets/MogCharts):
//   Появление — Нова повернула игрока к дэнс полу, до первого скилл-чека;
//   группы скилл-чеков из раскладки — чеки одного вида подряд, каждая группа — своя фаза;
//   Уход Новы или Смерть — Balance.MogOutroTime.
// Виды скилл-чеков и управление:
//   кружок  — стрелка бежит по кругу, ПРОБЕЛ, когда она в зелёной дуге;
//   кнопка  — как в osu: кольцо сжимается к белой обводке кнопки, клик по кнопке, когда сомкнулось;
//   стрелка — ждёт взмаха: провести мышью в её сторону, пока кольцо не сомкнулось (кольцо — сколько осталось);
//             не в ту сторону — не считается; не успел — стрелка краснеет, дальше следующий чек;
//   полоска — метка ездит (скорость меняется, у краёв отскакивает), ПРОБЕЛ, когда она в зелёной зоне.
// Чеки идут один за другим: следующий виден сразу, как решён предыдущий, и ждёт своего времени.
// Время чеков считается по музыке (если она есть). Запас мога (на экране его нет): с 3,
// попал — +1 (до 4), промах — −1; на нуле Нова нападает сразу, дожил до конца хотя бы с 1 — она уходит.
// Надписей фазы и запаса мога нет (пользователь, 2026-11-06) — только название чека и подсказка.
// Этот же компонент проигрывает раскладку в «Проверке скилл-чеков» в главном меню (MogTestTab) и в
// редакторе раскладок (MogChartEditor), там же он показывает выбранный чек неподвижно (ShowStill).
// Анимаций Новы пока нет: модель стоит на дэнс полу. Под фазы их будет подключать её аниматор по Current.
public class MogBattle : MonoBehaviour
{
    public enum Phase { None, Intro, Checks, Outro }

    const float RingGrow = 2f;       // кольцо появляется втрое больше кнопки, как в osu
    const float FadeIn = 0.35f;      // за какую долю сжатия кольца кнопка и кольцо проявляются
    const float WaitAlpha = 0.3f;    // следующая кнопка, пока её время не пришло, бледная
    const float BurstTime = 0.25f;   // вспышка на месте решённой кнопки, с
    const float MissBurstTime = 0.45f; // промах держится дольше: видно, что кнопка или стрелка покраснела
    const float TestLeadIn = 0.5f;   // проверка скилл-чеков: столько секунд до первого чека
    static readonly Color MissColor = new Color(1f, 0.3f, 0.25f);

    public GameObject panel;       // окно скилл-чека
    public Text title;             // «Полоска 2/5»
    public Text hint;              // что нажимать
    public AudioSource music;

    [Header("Полоска")]
    public GameObject barRoot;
    public RectTransform zone;     // зелёная зона (anchorMin.x / anchorMax.x)
    public RectTransform marker;   // метка (anchorMin.x = anchorMax.x = позиция)

    [Header("Кружок")]
    public GameObject circleRoot;
    public Image circleRing;       // серое кольцо
    public Image circleZone;       // зелёная дуга: то же кольцо, залитое на долю оборота
    public RectTransform needle;   // стрелка, вращается вокруг центра

    [Header("Кнопка и стрелка")]
    public RectTransform playArea; // в ней стоят кнопки и стрелки: x, y — доли этой области
    public RectTransform buttonRoot;
    public Image buttonDisc;
    public Image buttonBorder;     // белая обводка: к ней смыкается кольцо
    public Image buttonRing;       // сжимающееся кольцо
    public RectTransform arrowRoot;
    public Image arrowDisc;
    public Image arrowBorder;
    public Text arrowGlyph;        // «→», поворачивается в сторону стрелки
    public Image arrowRing;
    public RectTransform burst;    // вспышка на месте решённой кнопки или стрелки
    public Image burstDisc;
    public Image burstBorder;

    public Phase Current { get; private set; }
    public bool Running => Current != Phase.None;
    public MogTimeline Timeline { get; private set; }
    public float Clock => clock;
    public bool Won => won;

    List<MogChart> charts;
    MogChart lastChart;
    Action<bool> done;
    float clock, outroTime;
    bool clockRunning, won;
    Vector2 swipe;          // сколько провели мышью для текущей стрелки, px
    int swipeIndex = -1;
    CanvasGroup buttonGroup, arrowGroup, burstGroup;
    float burstAge = -1f;   // < 0 — вспышки нет
    bool burstHit;
    Text burstGlyph;        // стрелка на вспышке — копия arrowGlyph
    Vector2 panelPos;       // где окно в игре (редактор раскладок ставит его по-своему)
    MogCheck still;         // редактор: чек, показанный неподвижно

    void Awake()
    {
        GameSounds.VolumeChanged += ApplyMusicVolume; // громкость «Музыка мог-батла» в настройках
        circleRing.sprite = MogShapes.Ring;
        circleZone.sprite = MogShapes.Ring;
        circleZone.type = Image.Type.Filled;
        circleZone.fillMethod = Image.FillMethod.Radial360;
        circleZone.fillOrigin = (int)Image.Origin360.Top;
        circleZone.fillClockwise = true;
        buttonDisc.sprite = arrowDisc.sprite = burstDisc.sprite = MogShapes.Disc;
        buttonBorder.sprite = arrowBorder.sprite = burstBorder.sprite = MogShapes.Border;
        buttonRing.sprite = arrowRing.sprite = MogShapes.ThinRing;
        buttonGroup = Group(buttonRoot);
        arrowGroup = Group(arrowRoot);
        burstGroup = Group(burst);
        burstGlyph = Instantiate(arrowGlyph, burst, false);
        burstGlyph.name = "Glyph";
        burst.gameObject.SetActive(false);
        panelPos = ((RectTransform)panel.transform).anchoredPosition;
    }

    static CanvasGroup Group(Component c)
    {
        var g = c.GetComponent<CanvasGroup>();
        return g != null ? g : c.gameObject.AddComponent<CanvasGroup>();
    }

    // Атака Новы: случайная раскладка, по возможности не та же, что в прошлый раз
    public void Begin(int novaWins, Action<bool> onDone)
    {
        if (charts == null) charts = MogCharts.LoadAll();
        var pool = charts.Count > 1 ? charts.FindAll(c => c != lastChart) : charts;
        lastChart = pool[UnityEngine.Random.Range(0, pool.Count)];
        Begin(lastChart, novaWins, onDone);
    }

    // Проиграть конкретную раскладку. novaWins — сколько раз Нову уже победили (зоны уже).
    // startAt — с какой секунды музыки (редактор раскладок: «Проверить отсюда»)
    public void Begin(MogChart chart, int novaWins, Action<bool> onDone, float startAt = 0f)
    {
        float zoneScale = Mathf.Max(Balance.MogMinZoneScale, 1f - Balance.MogZoneShrinkPerWin * novaWins);
        Timeline = new MogTimeline(chart, zoneScale);
        Timeline.CheckResolved += (c, hit) =>
            Debug.Log($"[SSDFNC] Мог-батл: {MogTimeline.PhaseName(c.Type)} на {c.hit:0.00} с — " +
                      $"{(hit ? "попал" : "мимо")}, мог {Timeline.Lives}");
        Timeline.CheckResolved += Burst;
        still = null;
        FitPanel(null);

        done = onDone;
        clock = startAt;
        clockRunning = false;
        won = false;
        swipeIndex = -1;
        Current = Phase.Intro;
        panel.SetActive(false);
        Debug.Log($"[SSDFNC] Мог-батл: раскладка «{chart.name}», чеков {Timeline.Count}, фаз {Timeline.PhaseCount}, музыка: " +
                  $"{(string.IsNullOrEmpty(chart.music) ? "нет" : chart.music)}, зоны ×{zoneScale:0.0}");
        StartCoroutine(StartMusic(chart, startAt));
    }

    // Проверка скилл-чеков (меню, редактор раскладок): без появления Новы — сразу к первому чеку.
    // from — не раньше этой секунды (редактор: «Проверить отсюда»)
    public void BeginTest(MogChart chart, Action<bool> onDone, float from = 0f) =>
        Begin(chart, 0, onDone, Mathf.Max(from, chart.FirstCheckStart - TestLeadIn));

    // Прервать (проверка скилл-чеков, Esc): без итога
    public void Abort()
    {
        if (!Running) return;
        StopAllCoroutines();
        done = null;
        Finish();
    }

    void OnDestroy() => GameSounds.VolumeChanged -= ApplyMusicVolume;

    void ApplyMusicVolume()
    {
        if (music != null) music.volume = GameSounds.Volume(VolumeGroup.Music);
    }

    // Музыка грузится, пока идёт появление; время раскладки стартует вместе с музыкой
    IEnumerator StartMusic(MogChart chart, float startAt)
    {
        string path = MogCharts.MusicPath(chart);
        if (path != null)
            yield return MogCharts.LoadClip(path, clip =>
            {
                if (clip == null) return;
                music.clip = clip;
                music.time = Mathf.Clamp(startAt + chart.offset, 0f, clip.length - 0.05f);
                ApplyMusicVolume();
                music.Play();
            });
        clockRunning = true;
    }

    void Update()
    {
        DrawBurst();
        if (Current == Phase.None) return;

        if (clockRunning)
            clock = music.clip != null && music.isPlaying
                ? (float)music.timeSamples / music.clip.frequency - Timeline.Chart.offset
                : clock + Time.deltaTime;

        switch (Current)
        {
            case Phase.Intro:
                if (clockRunning && clock >= Timeline.Chart.FirstCheckStart) EnterChecks();
                break;
            case Phase.Checks:
                ReadInput();
                Timeline.Update(clock);
                if (Timeline.Done) EnterOutro();
                break;
            case Phase.Outro:
                outroTime += Time.deltaTime;
                if (outroTime >= Balance.MogOutroTime) Finish();
                break;
        }
        DrawUI();
    }

    // Управление зависит от вида чека: ПРОБЕЛ, клик по кнопке или взмах мышью
    void ReadInput()
    {
        if (GameFlow.InputBlocked || !Timeline.Active(clock)) return; // пауза: нажатия не засчитываются
        var c = Timeline.Current;
        var kb = Keyboard.current;
        var mouse = Mouse.current;
        switch (c.Type)
        {
            case MogCheckType.Circle:
            case MogCheckType.Bar:
                if (kb != null && kb.spaceKey.wasPressedThisFrame) Timeline.Press(clock);
                break;
            case MogCheckType.Button:
                if (mouse != null && mouse.leftButton.wasPressedThisFrame && OverButton(mouse.position.ReadValue()))
                    Timeline.Press(clock);
                break;
            case MogCheckType.Arrow:
                if (mouse != null) TrackSwipe(mouse.delta.ReadValue());
                break;
        }
    }

    // Клик внутри круга кнопки (с запасом 15%)
    bool OverButton(Vector2 screen)
    {
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(buttonRoot, screen, null, out var local)) return false;
        return local.magnitude <= buttonRoot.rect.width / 2f * 1.15f;
    }

    // Взмах мышью: копим движение, пока стрелка на экране; набралось Balance.MogSwipeDistance от высоты
    // экрана — взмах в ту сторону, куда двигали больше всего. Не в ту — не считается, копим заново
    void TrackSwipe(Vector2 delta)
    {
        if (swipeIndex != Timeline.Index)
        {
            swipeIndex = Timeline.Index;
            swipe = Vector2.zero;
        }
        swipe += delta;
        if (swipe.magnitude < Balance.MogSwipeDistance * Screen.height) return;
        var dir = Mathf.Abs(swipe.x) >= Mathf.Abs(swipe.y)
            ? (swipe.x > 0f ? MogDir.Right : MogDir.Left)
            : (swipe.y > 0f ? MogDir.Up : MogDir.Down);
        swipe = Vector2.zero;
        Timeline.Swipe(clock, dir);
    }

    void EnterChecks()
    {
        Current = Phase.Checks;
        Debug.Log("[SSDFNC] Мог-батл: скилл-чеки");
    }

    void EnterOutro()
    {
        won = Timeline.PlayerWon;
        Current = Phase.Outro;
        outroTime = 0f;
        panel.SetActive(false);
        Debug.Log($"[SSDFNC] Мог-батл: попаданий {Timeline.Hits}, промахов {Timeline.Misses}, мог {Timeline.Lives}" +
                  $" — {(won ? "Нова уходит" : $"смерть на чеке {Timeline.Index} из {Timeline.Count}")}");
    }

    void Finish()
    {
        music.Stop();
        if (music.clip != null) // клип грузится заново каждый батл — старый выгружаем
        {
            Destroy(music.clip);
            music.clip = null;
        }
        panel.SetActive(false);
        Current = Phase.None;
        var callback = done;
        done = null;
        callback?.Invoke(won);
    }

    void DrawUI()
    {
        if (Current == Phase.Checks) DrawCheck();
    }

    // Следующий чек показывается сразу, как решён предыдущий, и ждёт своего времени: стрелка кружка
    // сверху, метка полоски у левого края, кнопка бледная
    void DrawCheck()
    {
        var c = Timeline.Current;
        var type = c.Type;
        string name = MogTimeline.PhaseName(type);
        var (number, total) = Timeline.ProgressInGroup();
        bool active = Timeline.Active(clock);
        panel.SetActive(true);
        title.text = still == null ? $"{name} {number}/{total}" : name;
        hint.text = Hint(type);

        barRoot.SetActive(type == MogCheckType.Bar);
        circleRoot.SetActive(type == MogCheckType.Circle);
        buttonRoot.gameObject.SetActive(type == MogCheckType.Button);
        arrowRoot.gameObject.SetActive(type == MogCheckType.Arrow);

        switch (type)
        {
            case MogCheckType.Bar:
            {
                float half = Timeline.ZoneWidth / 2f;
                zone.anchorMin = new Vector2(Mathf.Clamp01(c.pos - half), 0f);
                zone.anchorMax = new Vector2(Mathf.Clamp01(c.pos + half), 1f);
                marker.anchorMin = marker.anchorMax = new Vector2(Timeline.MarkerPos(clock), 0.5f);
                break;
            }
            case MogCheckType.Circle:
                // Дуга от верха по часовой длиной в зону, повёрнута так, чтобы её середина была на pos
                circleZone.fillAmount = Timeline.ZoneWidth;
                circleZone.rectTransform.localEulerAngles = new Vector3(0f, 0f, -(c.pos - Timeline.ZoneWidth / 2f) * 360f);
                needle.localEulerAngles = new Vector3(0f, 0f, -(active ? Timeline.NeedleAngle(clock) : 0f) * 360f);
                break;
            case MogCheckType.Button:
                Place(buttonRoot, c);
                DrawApproach(buttonGroup, buttonRing, active);
                break;
            case MogCheckType.Arrow:
                Place(arrowRoot, c);
                arrowGlyph.rectTransform.localEulerAngles = new Vector3(0f, 0f, 90f * (int)c.Direction);
                DrawApproach(arrowGroup, arrowRing, active);
                break;
        }
    }

    // Кнопка и стрелка как в osu: пока время не пришло — бледные; кольцо появляется втрое больше,
    // за первую треть сжатия проявляется вместе с кнопкой и в бит смыкается ровно с белой обводкой
    void DrawApproach(CanvasGroup group, Image ring, bool active)
    {
        float progress = Timeline.RingProgress(clock);   // 1 — кольцо только появилось, 0 — сомкнулось
        float shown = active ? Mathf.Clamp01((1f - progress) / FadeIn) : 0f;
        group.alpha = Mathf.Lerp(WaitAlpha, 1f, shown);
        ring.enabled = active;
        ring.color = new Color(1f, 1f, 1f, shown);
        ring.rectTransform.localScale = Vector3.one * (1f + RingGrow * progress);
    }

    // Решена кнопка или стрелка: на её месте вспышка. Попал — белая, расходится; мимо — красная, гаснет
    void Burst(MogCheck c, bool hit)
    {
        if (c.Type != MogCheckType.Button && c.Type != MogCheckType.Arrow) return;
        var source = c.Type == MogCheckType.Button ? buttonRoot : arrowRoot;
        burst.sizeDelta = source.sizeDelta;
        Place(burst, c);
        burstDisc.color = hit ? (c.Type == MogCheckType.Button ? buttonDisc.color : arrowDisc.color) : MissColor;
        burstBorder.color = hit ? Color.white : MissColor;
        burstGlyph.gameObject.SetActive(c.Type == MogCheckType.Arrow);
        burstGlyph.rectTransform.localEulerAngles = new Vector3(0f, 0f, 90f * (int)c.Direction);
        burstGlyph.color = hit ? arrowGlyph.color : Color.white;
        burstHit = hit;
        burstAge = 0f;
        burst.gameObject.SetActive(true);
    }

    void DrawBurst()
    {
        if (burstAge < 0f) return;
        burstAge += Time.deltaTime;
        float k = burstAge / (burstHit ? BurstTime : MissBurstTime);
        if (k >= 1f || !panel.activeSelf)
        {
            burstAge = -1f;
            burst.gameObject.SetActive(false);
            return;
        }
        burst.localScale = Vector3.one * (burstHit ? 1f + 0.4f * k : 1f - 0.15f * k);
        burstGroup.alpha = 1f - k;
    }

    // Кнопка или стрелка на своё место: x, y — доли области playArea (0, 0 — левый нижний угол),
    // кнопка целиком внутри области
    void Place(RectTransform target, MogCheck c)
    {
        var free = playArea.rect.size - target.rect.size;
        target.anchoredPosition = new Vector2((c.x - 0.5f) * free.x, (c.y - 0.5f) * free.y);
    }

    // ── Редактор раскладок ──

    // Показать один чек неподвижно в момент t (без ввода и музыки). Чек тот же объект, что в раскладке:
    // правки видны сразу
    public void ShowStill(MogCheck check, float t)
    {
        if (Running) return;
        if (still != check) Timeline = new MogTimeline(new MogChart { checks = new[] { check } });
        still = check;
        clock = t;
        DrawCheck();
    }

    public void HideStill()
    {
        if (Running || still == null) return;
        still = null;
        panel.SetActive(false);
    }

    // Окно поменьше и на своём месте: прямоугольник в пикселях экрана (y снизу). null — как в игре
    public void FitPanel(Rect? screen)
    {
        var rt = (RectTransform)panel.transform;
        if (screen == null)
        {
            rt.anchoredPosition = panelPos;
            rt.localScale = Vector3.one;
            return;
        }
        var r = screen.Value;
        var parent = (RectTransform)rt.parent;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, r.center, null, out var center);
        float k = parent.lossyScale.x; // пикселей экрана на единицу канваса
        rt.localScale = Vector3.one * Mathf.Min(r.width / (rt.rect.width * k), r.height / (rt.rect.height * k));
        rt.anchoredPosition = center;
    }

    // Куда в окне (x, y от 0 до 1) встанет кнопка или стрелка, если её центр — в этой точке экрана
    public Vector2 PlaceAt(Vector2 screen, MogCheckType type)
    {
        var target = type == MogCheckType.Arrow ? arrowRoot : buttonRoot;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(playArea, screen, null, out var local);
        var free = playArea.rect.size - target.rect.size;
        return new Vector2(Mathf.Clamp01(local.x / free.x + 0.5f), Mathf.Clamp01(local.y / free.y + 0.5f));
    }

    // Кружок: доля оборота от верха по часовой, куда показывает точка экрана
    public float CircleTurnAt(Vector2 screen)
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)circleRoot.transform, screen, null, out var local);
        float turn = Mathf.Atan2(local.x, local.y) / (2f * Mathf.PI);
        return turn <= 0f ? turn + 1f : turn;
    }

    // Полоска: доля полоски под точкой экрана
    public float BarPosAt(Vector2 screen)
    {
        var bar = (RectTransform)barRoot.transform;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(bar, screen, null, out var local);
        return Mathf.Clamp01(local.x / bar.rect.width + 0.5f);
    }

    static string Hint(MogCheckType type)
    {
        switch (type)
        {
            case MogCheckType.Circle: return "ПРОБЕЛ — когда стрелка в зелёной дуге";
            case MogCheckType.Button: return "Клик по кнопке, когда кольцо сомкнётся";
            case MogCheckType.Arrow: return "Проведи мышью по стрелке, пока кольцо не сомкнулось";
            default: return "ПРОБЕЛ — когда метка в зелёной зоне";
        }
    }
}
