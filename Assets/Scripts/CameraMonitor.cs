using UnityEngine;
using UnityEngine.UI;

// Монитор камер: 8 кнопок — комнаты 2–8 и проход Г (Rooms.Cameras). На экране название комнаты
// и кто там из фурри. На камере 7 (кормушка) — кнопка «ПОКОРМИТЬ»: держать DirFeedHoldTime секунд.
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
    public Generator generator;   // не задан — питание есть всегда
    public NightDirector director;
    public Color idleScreen = new Color(0.04f, 0.1f, 0.06f);
    public Color idleButton = new Color(0.2f, 0.2f, 0.22f);

    int selected = -1;
    bool wasRebooting;

    bool Powered => generator == null || generator.IsPowered;
    bool Rebooting => director != null && director.MonitorRebooting;
    bool OnFeeding => selected >= 0 && Rooms.Cameras[selected] == Room.Feeding;

    void Awake()
    {
        for (int i = 0; i < buttons.Length; i++)
        {
            int index = i;
            buttons[i].onClick.AddListener(() => Select(index));
        }
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
        body.text = director.Describe(Rooms.Cameras[selected]);

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

    void Redraw()
    {
        bool on = Powered && !Rebooting;
        bool none = selected < 0;
        screen.color = on ? idleScreen : OffColor;
        title.text = !on ? "" : none ? "Выбери камеру" : $"КАМ {Rooms.CameraLabel(Rooms.Cameras[selected])} · {Rooms.Name(Rooms.Cameras[selected])}";
        body.text = "";
        feedButton.gameObject.SetActive(on && OnFeeding && director != null && director.DirActive);
        if (director != null) director.ReleaseFeed();

        for (int i = 0; i < buttons.Length; i++)
            buttons[i].image.color = !on ? OffColor : i == selected ? SelectedButton : idleButton;
    }
}
