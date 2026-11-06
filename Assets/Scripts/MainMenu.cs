using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Главное меню: «Играть», «Настройки», «Выход».
// «Играть» — кнопка встаёт влево (вровень с первым рядом ночей), справа от неё появляются открытые ночи
// (NightProgress), двумя столбцами по три: 1–6 — по мере прохождения, 7-я — ниже посередине, когда пройдена 6-я
// (без 50 $ видна, но с причиной). Кнопки высотой с «Играть», первый ряд — вровень с ней. Пройденные — светлее, ещё не пройденные — тёмные, выбранная — яркая.
// Выбрал ночь — «Играть» ещё раз начинает её. Сразу выбрана самая дальняя открытая ночь.
// «Своя ночь» — отдельная кнопка под списком (когда пройдена 7-я): нажал — ночи пропадают, она встаёт на место первой,
// под ней — уровни фурри (NightSettingsTab), «Играть» начинает свою ночь, «Назад» — обратно к списку.
// «Настройки»: «Громкость» (VolumeSettingsPanel), «Звуки» (какой звук у какого события — то же окно, что в паузе,
// SoundSettingsPanel), «Скилл-чеки» (проверка раскладок мог-батла, MogTestTab) и «Свет: обычный / неон»
// (OfficeLights.NeonEnabled; в офисе — F4).
// «Назад» и Esc — на шаг назад. В правом верхнем углу — деньги (Wallet). Анимация простая: всё сразу на своём месте и
// коротко проявляется / гаснет (FadeTime).
public class MainMenu : MonoBehaviour
{
    const float FadeTime = 0.12f;     // с, появление / исчезание (кнопки не ездят: сразу на месте и проявляются)
    const int Columns = 2, Rows = 3;              // ночи 1–6: два столбца по три
    const bool ByColumns = false;                 // false: 1 2 / 3 4 / 5 6;  true: столбцы 1-2-3 и 4-5-6
    const float GridX = 200f, GridTop = 210f;     // центр первой ночи (левый верхний угол сетки)
    const float ColumnStep = 220f, RowStep = 130f; // шаг сетки вправо и вниз
    const float CustomGap = 30f;                  // px, отступ «Своей ночи» от сетки
    static readonly Vector2 PlayAside = new Vector2(-240f, GridTop); // куда отъезжает «Играть»: влево, вровень с 1-м рядом
    static readonly Vector2 BackNights = new Vector2(-240f, -470f);  // «Назад» в списке ночей — под «Играть»
    static readonly Color ItemSelected = new Color(0.62f, 0.34f, 0.86f); // выбранная
    static readonly Color ItemPassed = new Color(0.18f, 0.12f, 0.24f);   // пройденная — как кнопка «Играть»
    static readonly Color ItemNew = new Color(0.11f, 0.075f, 0.15f);     // ещё не пройденная — темнее (фон 0.07/0.04/0.1)

    public Button playButton, settingsButton, quitButton, backButton;
    public Button[] nightButtons;   // ночи 1–7
    public Text[] nightLabels;
    public Button customButton;     // «Своя ночь»
    public Text selectionLabel;     // под «Играть»: какая ночь выбрана
    public RectTransform customPanel; // уровни своей ночи (NightSettingsTab)
    public Button volumeButton, soundsButton, checksButton, lightButton;
    public RectTransform volumePanel, checksPanel;
    public SoundSettingsPanel soundsPanel;
    public MogTestTab checksTest;
    public Text moneyLabel;
    public string gameScene = "SampleScene";

    enum Screen { Home, Nights, Custom, Settings, Volume, Checks, Sounds }

    // Кнопка или панель: встаёт на своё место и плавно появляется / исчезает
    class Item
    {
        public RectTransform rt;
        public CanvasGroup group;
        public Vector2 home;   // место из сборщика
        public Vector2 target;
        public float alpha;
    }

    readonly Dictionary<Transform, Item> items = new Dictionary<Transform, Item>();
    Screen screen = Screen.Home;
    int selected = 1; // 1–7 — ночь, выбранная в списке

    void Awake()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        playButton.onClick.AddListener(OnPlay);
        settingsButton.onClick.AddListener(() => Show(Screen.Settings));
        quitButton.onClick.AddListener(Quit);
        backButton.onClick.AddListener(Back);
        for (int i = 0; i < nightButtons.Length; i++)
        {
            int night = i + 1;
            nightButtons[i].onClick.AddListener(() => Select(night));
        }
        customButton.onClick.AddListener(() => Show(screen == Screen.Custom ? Screen.Nights : Screen.Custom));
        volumeButton.onClick.AddListener(() => Show(Screen.Volume));
        soundsButton.onClick.AddListener(() => Show(Screen.Sounds));
        checksButton.onClick.AddListener(() => Show(Screen.Checks));
        if (lightButton != null)
        {
            lightButton.onClick.AddListener(() =>
            {
                OfficeLights.NeonEnabled = !OfficeLights.NeonEnabled;
                ShowLight();
            });
            ShowLight();
        }
        soundsPanel.Closed += () => Show(Screen.Settings);
        soundsPanel.gameObject.SetActive(false);

        foreach (var t in Animated()) Track(t);
        foreach (var it in items.Values) Snap(it, 0f);
        Select(NightProgress.LastUnlocked);
        Show(Screen.Home);
        if (moneyLabel != null) moneyLabel.text = $"Деньги: {Wallet.Money} $";
    }

    IEnumerable<Transform> Animated()
    {
        yield return playButton.transform;
        yield return settingsButton.transform;
        yield return quitButton.transform;
        yield return backButton.transform;
        foreach (var b in nightButtons) yield return b.transform;
        yield return customButton.transform;
        yield return selectionLabel.transform;
        yield return customPanel;
        yield return volumeButton.transform;
        yield return soundsButton.transform;
        yield return checksButton.transform;
        if (lightButton != null) yield return lightButton.transform;
        yield return volumePanel;
        yield return checksPanel;
    }

    void Track(Transform t)
    {
        var rt = (RectTransform)t;
        var group = t.GetComponent<CanvasGroup>();
        if (group == null) group = t.gameObject.AddComponent<CanvasGroup>();
        items[t] = new Item { rt = rt, group = group, home = rt.anchoredPosition, target = rt.anchoredPosition };
    }

    // ── Экраны ──

    void Show(Screen next)
    {
        screen = next;
        bool home = next == Screen.Home, nights = next == Screen.Nights, custom = next == Screen.Custom,
             settings = next == Screen.Settings;

        // «Играть» на главной — на месте, в списке ночей — сбоку; «Настройки» и «Выход» — только на главной
        var play = items[playButton.transform];
        if (home || nights || custom) Appear(play, nights || custom ? PlayAside : play.home);
        else Hide(play);
        if (home)
        {
            Appear(items[settingsButton.transform], null);
            Appear(items[quitButton.transform], null);
        }
        else
        {
            Hide(items[settingsButton.transform]);
            Hide(items[quitButton.transform]);
        }
        playButton.interactable = !nights || NightProgress.IsUnlocked(selected);

        // Ночи — сеткой
        for (int i = 0; i < nightButtons.Length; i++)
        {
            int night = i + 1;
            var it = items[nightButtons[i].transform];
            if (nights && NightVisible(night)) Appear(it, Cell(night));
            else Hide(it);
        }
        // «Своя ночь»: в списке — внизу с отступом; открыта — наверху, на месте первой ночи, под ней уровни
        var customItem = items[customButton.transform];
        float middle = GridX + ColumnStep * (Columns - 1) / 2f; // «Своя ночь» — во всю ширину сетки
        if (custom) Appear(customItem, new Vector2(middle, GridTop));
        else if (nights && NightProgress.Passed >= Nights.Count)
            Appear(customItem, new Vector2(middle, GridTop - (Rows + 1) * RowStep - CustomGap));
        else Hide(customItem);
        customButton.image.color = custom ? ItemSelected : ItemPassed; // как обычная кнопка
        SetShown(items[selectionLabel.transform], nights || custom);
        SetShown(items[customPanel], custom);
        if (nights || custom) ShowSelection();

        // Настройки
        SetShown(items[volumeButton.transform], settings);
        SetShown(items[soundsButton.transform], settings);
        SetShown(items[checksButton.transform], settings);
        if (lightButton != null) SetShown(items[lightButton.transform], settings);
        SetShown(items[volumePanel], next == Screen.Volume);
        SetShown(items[checksPanel], next == Screen.Checks);
        if (next == Screen.Checks) checksTest.Refresh();
        if (next == Screen.Sounds) soundsPanel.Open();

        var back = items[backButton.transform];
        if (!home && next != Screen.Sounds) Appear(back, nights || custom ? BackNights : back.home);
        else Hide(back);
    }

    void Back()
    {
        switch (screen)
        {
            case Screen.Nights:
            case Screen.Settings: Show(Screen.Home); break;
            case Screen.Custom: Show(Screen.Nights); break;
            case Screen.Volume:
            case Screen.Checks: Show(Screen.Settings); break;
            case Screen.Sounds: soundsPanel.Close(); break; // сам вернёт в настройки (Closed)
        }
    }

    void Update()
    {
        float dt = Time.unscaledDeltaTime;
        foreach (var it in items.Values) Tick(it, dt);

        var kb = Keyboard.current;
        bool testing = checksTest != null && checksTest.battle.Running; // Esc там прерывает проверку
        if (kb != null && kb.escapeKey.wasPressedThisFrame && !testing && screen != Screen.Home) Back();
    }

    void ShowLight() => lightButton.GetComponentInChildren<Text>().text = OfficeLights.NeonEnabled ? "Свет: неон" : "Свет: обычный";

    // ── Ночи ──

    // Место ночи в сетке: 1–6 — два столбца по три, 7-я — следующим рядом посередине
    static Vector2 Cell(int night)
    {
        if (night > Columns * Rows) return new Vector2(GridX + ColumnStep * (Columns - 1) / 2f, GridTop - Rows * RowStep);
        int i = night - 1;
        int col = ByColumns ? i / Rows : i % Columns;
        int row = ByColumns ? i % Rows : i / Columns;
        return new Vector2(GridX + col * ColumnStep, GridTop - row * RowStep);
    }

    // В списке только открытые; 7-я — как только пройдена 6-я (даже если не хватает денег — видно, что нужно)
    static bool NightVisible(int night) =>
        night < Nights.Count ? NightProgress.IsUnlocked(night) : NightProgress.Passed >= Nights.Count - 1;

    void OnPlay()
    {
        if (screen == Screen.Home) Show(Screen.Nights);
        else if (screen == Screen.Nights) StartNight(selected);
        else if (screen == Screen.Custom) StartNight(0);
    }

    void Select(int night)
    {
        selected = night;
        for (int i = 0; i < nightButtons.Length; i++)
        {
            int n = i + 1;
            string reason = NightProgress.LockReason(n);
            bool passed = n <= NightProgress.Passed;
            nightButtons[i].image.color = n == selected ? ItemSelected : passed ? ItemPassed : ItemNew;
            string note = reason ?? (passed ? "пройдена" : "");
            nightLabels[i].text = note == "" ? $"Ночь {n}" : $"Ночь {n}\n<size=24>{note}</size>";
        }
        if (screen == Screen.Nights) ShowSelection();
    }

    // Подпись под «Играть» и можно ли начать
    void ShowSelection()
    {
        if (screen == Screen.Custom)
        {
            selectionLabel.text = "Своя ночь";
            playButton.interactable = true;
            return;
        }
        string lockReason = NightProgress.LockReason(selected);
        selectionLabel.text = lockReason != null ? $"Ночь {selected}: {lockReason}"
            : selected <= NightProgress.Passed ? $"Ночь {selected} · пройдена" : $"Ночь {selected}";
        playButton.interactable = lockReason == null;
    }

    // Без номера (так зовут автотесты) — самая дальняя открытая ночь
    public void StartNight() => StartNight(NightProgress.LastUnlocked);

    // night 1–7 — ночь прохождения, 0 — «Своя ночь» (уровни из NightSettings)
    public void StartNight(int night)
    {
        if (night > 0 && !NightProgress.IsUnlocked(night)) return;
        NightProgress.Select(night);
        Debug.Log($"[SSDFNC] Начать: {NightProgress.Name}" + (night == 0 ? " " + NightSettings.Describe() : ""));
        SceneLoader.Load(gameScene, night == 0 ? "Своя ночь…" : $"Ночь {night}…");
    }

    public void Quit()
    {
        Debug.Log("[SSDFNC] Выйти");
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false; // в редакторе Application.Quit ничего не делает
#else
        Application.Quit();
#endif
    }

    // ── Анимация ──

    // Появиться на месте at (или на своём). Сменила место — сразу там и проявляется заново
    void Appear(Item it, Vector2? at)
    {
        var to = at ?? it.home;
        if (it.rt.anchoredPosition != to)
        {
            it.rt.anchoredPosition = to;
            it.group.alpha = 0f;
        }
        it.target = to;
        it.alpha = 1f;
        SetInput(it, true);
    }

    void Hide(Item it)
    {
        it.alpha = 0f;
        SetInput(it, false);
    }

    void SetShown(Item it, bool on)
    {
        if (on) Appear(it, null);
        else Hide(it);
    }

    static void Snap(Item it, float alpha)
    {
        it.alpha = it.group.alpha = alpha;
        it.rt.anchoredPosition = it.target;
        SetInput(it, alpha > 0.5f);
    }

    // Спрятанная кнопка только не ловит клики. Выключать её (interactable) нельзя: Unity на миг красит выключенную
    // кнопку в свой светлый цвет — было белое мигание
    static void SetInput(Item it, bool on) => it.group.blocksRaycasts = on;

    static void Tick(Item it, float dt) => it.group.alpha = Mathf.MoveTowards(it.group.alpha, it.alpha, dt / FadeTime);
}
