using UnityEngine;
using UnityEngine.UI;

// Монитор камер: 8 кнопок — комнаты 2–8 и проход Г (Rooms.Cameras). На экране название комнаты
// и кто там из фурри. На камере 7 (кормушка) — кнопка «ПОКОРМИТЬ»: держать DirFeedHoldTime секунд.
// Без питания от генератора экран чёрный и ничего не работает.
public class CameraMonitor : MonoBehaviour
{
    static readonly Color OffColor = new Color(0.02f, 0.02f, 0.02f);
    static readonly Color SelectedButton = new Color(0.25f, 0.75f, 0.35f);

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

    bool Powered => generator == null || generator.IsPowered;
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
        if (!Powered) return;
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
        if (!Powered || selected < 0 || director == null) return;
        body.text = director.Describe(Rooms.Cameras[selected]);

        if (!OnFeeding) return;
        if (feedButton.IsHeld) director.HoldFeed(Time.deltaTime);
        else director.ReleaseFeed();
        feedLabel.text = feedButton.IsHeld ? $"КОРМИМ… {director.FeedProgress * 100f:0}%" : "ПОКОРМИТЬ (держать)";
    }

    void Redraw()
    {
        bool on = Powered;
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
