using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

// Монитор камер: 7 кнопок — камера 1 (не работает: «НЕТ СИГНАЛА», сильные помехи) и комнаты 2–7 (Rooms.Cameras). На экране название комнаты и кадр камеры
// (CameraFeeds: картинка или видео по кругу для «в комнате ровно эти фурри»); кадра нет — текстом, кто там из фурри. На камере 7 (кормушка) — кнопка «ПОКОРМИТЬ»: держать DirFeedHoldTime секунд.
// Без питания от генератора экран чёрный и ничего не работает.
// Сбой (Night.MonitorRebooting): экран гаснет и идёт перезагрузка Balance.MonitorRebootTime секунд — полоска
// загрузки, камеры и кормление не работают; потом снова «Выбери камеру».
public class CameraMonitor : MonoBehaviour
{
    static readonly Color OffColor = new Color(0.02f, 0.02f, 0.02f);
    static readonly Color SelectedButton = new Color(0.25f, 0.75f, 0.35f);
    static readonly Color RebootScreen = new Color(0.02f, 0.04f, 0.12f);
    const float Blackout = 0.4f; // с в начале сбоя экран просто чёрный
    const int BarCells = 12;

    public Image screen;
    public Text title;
    public Text body;
    public Button[] buttons;      // по порядку Rooms.Cameras
    public HoldButton feedButton;
    public Text feedLabel;
    public RawImage feed;         // кадр камеры (под названием и кнопками)
    public VideoPlayer video;     // для видео-кадров (mp4)
    public MonitorStatic noise;   // помехи поверх кадра
    public Text clock;            // время смены в правом нижнем углу (пишет ShiftTimer)
    public Generator generator;   // не задан — питание есть всегда
    public NightDirector director;
    public Color idleScreen = new Color(0.04f, 0.1f, 0.06f);
    public Color idleButton = new Color(0.2f, 0.2f, 0.22f);

    int selected = -1;
    bool wasRebooting;
    CameraFeeds.Feed shown;
    RenderTexture videoTexture;

    bool Powered => generator == null || generator.IsPowered;
    bool Rebooting => director != null && director.MonitorRebooting;
    bool OnFeeding => selected >= 0 && Rooms.Cameras[selected] == Room.Feeding;

    void Awake()
    {
        // Всё на экране — по его краям: ползущая полоса помех и дёрганье кадра при сбое выходили за монитор
        if (!TryGetComponent<RectMask2D>(out _)) gameObject.AddComponent<RectMask2D>();
        for (int i = 0; i < buttons.Length; i++)
        {
            int index = i;
            buttons[i].onClick.AddListener(() => Select(index));
        }
        if (video != null) video.prepareCompleted += OnVideoReady;
    }

    void OnDestroy()
    {
        if (videoTexture != null) videoTexture.Release();
    }

    void OnEnable()
    {
        if (generator != null) generator.PowerChanged += OnPowerChanged;
        Redraw();
    }

    void OnDisable()
    {
        if (generator != null) generator.PowerChanged -= OnPowerChanged;
    }

    // -1 — ни одна камера не выбрана
    public void Select(int index)
    {
        if (!Powered || Rebooting) return;
        if (index != selected && noise != null) noise.Burst();
        selected = index;
        Redraw();
    }

    // После отключения монитор включается без выбранной камеры
    void OnPowerChanged(bool powered)
    {
        selected = -1;
        Redraw();
    }

    void Update()
    {
        bool rebooting = Rebooting;
        if (rebooting != wasRebooting) // сбой начался или кончился: камера сбрасывается, как после отключения
        {
            wasRebooting = rebooting;
            selected = -1;
            Redraw();
        }
        if (rebooting)
        {
            if (Powered) DrawReboot();
            return;
        }
        if (!Powered || selected < 0 || director == null) return;
        var room = Rooms.Cameras[selected];
        if (noise != null) noise.SetNoSignal(room == Room.Camera1);
        if (room == Room.Camera1)
        {
            body.text = "НЕТ СИГНАЛА";
            return;
        }
        var now = director.Night != null ? CameraFeeds.Find(room, director.Night.In(room).Select(f => f.Id)) : null;
        if (now != shown) ShowFeed(now);
        body.text = now != null ? "" : director.Describe(room);

        if (!OnFeeding) return;
        if (feedButton.IsHeld) director.HoldFeed(Time.deltaTime);
        else director.ReleaseFeed();
        feedLabel.text = feedButton.IsHeld ? $"КОРМИМ… {director.FeedProgress * 100f:0}%" : "ПОКОРМИТЬ (держать)";
    }

    // Перезагрузка: сначала экран чёрный, потом загрузка — точки, полоска и строки, как при старте системы
    void DrawReboot()
    {
        float elapsed = Balance.MonitorRebootTime - director.MonitorRebootLeft;
        if (elapsed < Blackout)
        {
            screen.color = OffColor;
            title.text = body.text = "";
            return;
        }
        float k = Mathf.Clamp01((elapsed - Blackout) / (Balance.MonitorRebootTime - Blackout));
        int cells = Mathf.RoundToInt(k * BarCells);
        screen.color = RebootScreen;
        title.text = "ПЕРЕЗАГРУЗКА" + new string('.', (int)(elapsed * 3f) % 4);
        body.text = $"[{new string('█', cells)}{new string('░', BarCells - cells)}] {k * 100f:0}%" +
                    (k > 0.15f ? "\nВидеосистема клуба" : "") +
                    (k > 0.45f ? "\nКамеры: " + Cameras(k) : "") +
                    (k > 0.8f ? "\nКормушка: связь есть" : "");
    }

    // Номера камер появляются по одному по мере загрузки
    static string Cameras(float k)
    {
        int n = Mathf.Clamp(Mathf.CeilToInt((k - 0.45f) / 0.35f * Rooms.Cameras.Length), 0, Rooms.Cameras.Length);
        var labels = new string[n];
        for (int i = 0; i < n; i++) labels[i] = Rooms.CameraLabel(Rooms.Cameras[i]);
        return string.Join(" ", labels);
    }

    // Кадр камеры: картинка — сразу, видео — когда VideoPlayer подготовит файл; null — кадра нет (текст)
    void ShowFeed(CameraFeeds.Feed f)
    {
        shown = f;
        if (feed == null) return;
        if (video != null) video.Stop();
        feed.enabled = false;
        if (f == null) return;
        if (!f.video)
        {
            feed.texture = CameraFeeds.Texture(f);
            feed.enabled = true;
            return;
        }
        if (video == null) return;
        if (videoTexture == null) videoTexture = new RenderTexture(960, 540, 0) { name = "CameraFeedVideo" };
        video.targetTexture = videoTexture;
        video.url = f.path;
        video.Prepare();
    }

    void OnVideoReady(VideoPlayer source)
    {
        if (shown == null || !shown.video || source.url != shown.path) return;
        source.Play();
        feed.texture = videoTexture;
        feed.enabled = true;
    }

    void Redraw()
    {
        ShowFeed(null); // кадр подберёт Update по тому, кто сейчас в комнате
        bool on = Powered && !Rebooting;
        if (noise != null)
        {
            noise.SetOn(on);
            noise.SetNoSignal(false);
        }
        bool none = selected < 0;
        screen.color = on ? idleScreen : OffColor;
        if (clock != null) clock.enabled = on;
        title.text = !on ? "" : none ? "Выбери камеру" : $"КАМ {Rooms.CameraLabel(Rooms.Cameras[selected])} · {Rooms.Name(Rooms.Cameras[selected])}";
        body.text = "";
        feedButton.gameObject.SetActive(on && OnFeeding && director != null && director.DirActive);
        if (director != null) director.ReleaseFeed();

        for (int i = 0; i < buttons.Length; i++)
            buttons[i].image.color = !on ? OffColor : i == selected ? SelectedButton : idleButton;
    }
}
