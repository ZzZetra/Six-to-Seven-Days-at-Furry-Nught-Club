using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class MainMenuBuilder
{
    const string MenuScenePath = "Assets/Scenes/MainMenu.unity";
    const string GameScenePath = "Assets/Scenes/SampleScene.unity";

    // Версия сборщика меню, как у TestPlatformBuilder: устарело — Unity предложит пересобрать.
    // 2 — вкладки «Главная» и «Настройка ночи». 3 — счётчик денег. 4 — вкладка «Скилл-чеки».
    // 5 — кнопка редактора раскладок (убрана в 6: редактор — отдельная программа).
    const int Version = 6;
    const string VersionNote = "кнопка в окне скилл-чеков как в osu, проверка без «Приготовься»; редактор раскладок — отдельная программа";
    static string VersionKey => "SSDFNC.MainMenuVersion." + Application.dataPath;

    [InitializeOnLoadMethod]
    static void OfferRebuildIfOutdated()
    {
        if (Application.isBatchMode || EditorPrefs.GetInt(VersionKey, 0) >= Version) return;
        if (SessionState.GetBool("SSDFNC.MenuRebuildAsked." + Version, false)) return;

        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            SessionState.SetBool("SSDFNC.MenuRebuildAsked." + Version, true);
            if (EditorUtility.DisplayDialog("SSDFNC",
                    $"Главное меню обновилось: {VersionNote}.\nПересобрать сцену меню сейчас?",
                    "Пересобрать", "Позже"))
                Build();
        };
    }

    // Собирает сцену главного меню заново и прописывает сцены игры в Build Settings.
    [MenuItem("SSDFNC/Build Main Menu")]
    public static void Build()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var cam = new GameObject("Main Camera", typeof(Camera)).GetComponent<Camera>();
        cam.tag = "MainCamera";
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Color.black;

        new GameObject("EventSystem",
            typeof(UnityEngine.EventSystems.EventSystem),
            typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));

        var canvasGo = new GameObject("Menu", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster),
            typeof(CanvasGroup)); // CanvasGroup — спрятать меню на время проверки скилл-чеков
        canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        var root = canvasGo.transform;

        var bg = TestPlatformBuilder.NewImage("Background", root, new Color(0.07f, 0.04f, 0.1f));
        TestPlatformBuilder.Stretch(bg.rectTransform);

        var title = TestPlatformBuilder.NewText("Title", root, 72);
        title.text = "Six to Seven Days\nat the Furry Night Club";
        Place(title.rectTransform, new Vector2(0, 360), new Vector2(1600, 200));

        var menu = canvasGo.AddComponent<MainMenu>();

        // Деньги — в правом верхнем углу
        menu.moneyLabel = TestPlatformBuilder.NewText("Money", root, 44);
        menu.moneyLabel.alignment = TextAnchor.UpperRight;
        menu.moneyLabel.color = new Color(1f, 0.85f, 0.35f);
        var money = menu.moneyLabel.rectTransform;
        money.anchorMin = money.anchorMax = money.pivot = Vector2.one;
        money.anchoredPosition = new Vector2(-40, -30);
        money.sizeDelta = new Vector2(600, 70);

        // Вкладки: цвет кнопки вкладки задаёт MainMenu.ShowTab (подсветка выбранной)
        menu.mainTabButton = TabButton("MainTabButton", root, "Главная", new Vector2(-360, 210));
        menu.settingsTabButton = TabButton("SettingsTabButton", root, "Настройка ночи", new Vector2(0, 210));
        menu.checksTabButton = TabButton("ChecksTabButton", root, "Скилл-чеки", new Vector2(360, 210));

        // Вкладка «Главная»
        var mainTab = Container("MainTab", root);
        menu.mainTab = mainTab.gameObject;
        menu.startButton = NewButton("StartButton", mainTab, "Начать ночь", new Vector2(0, -20), new Vector2(520, 110), 48);
        menu.quitButton = NewButton("QuitButton", mainTab, "Выйти", new Vector2(0, -160), new Vector2(520, 110), 48);

        // Вкладка «Настройка ночи»
        var settings = Container("SettingsTab", root);
        menu.settingsTab = settings.gameObject;
        var tab = settings.gameObject.AddComponent<NightSettingsTab>();
        string[] names = { "Дир", "Стрей", "Нова", "Дали", "Доли" };
        tab.values = new Text[5];
        tab.minus = new Button[5];
        tab.plus = new Button[5];
        for (int i = 0; i < 5; i++)
        {
            float y = 110 - i * 75;
            var name = TestPlatformBuilder.NewText("Name" + i, settings, 42);
            name.text = names[i];
            name.alignment = TextAnchor.MiddleLeft;
            Place(name.rectTransform, new Vector2(-250, y), new Vector2(240, 70));
            tab.minus[i] = NewButton("Minus" + i, settings, "−", new Vector2(-20, y), new Vector2(76, 64), 44);
            tab.values[i] = TestPlatformBuilder.NewText("Value" + i, settings, 42);
            Place(tab.values[i].rectTransform, new Vector2(90, y), new Vector2(140, 70));
            tab.plus[i] = NewButton("Plus" + i, settings, "+", new Vector2(200, y), new Vector2(76, 64), 44);
        }
        tab.easy = NewButton("Easy", settings, "Easy", new Vector2(-330, -275), new Vector2(180, 66), 34);
        tab.normal = NewButton("Normal", settings, "Normal", new Vector2(-135, -275), new Vector2(180, 66), 34);
        tab.hard = NewButton("Hard", settings, "Hard", new Vector2(60, -275), new Vector2(180, 66), 34);
        tab.onlyNova = NewButton("OnlyNova", settings, "Только Нова", new Vector2(290, -275), new Vector2(250, 66), 34);
        tab.defaults = NewButton("Defaults", settings, "По умолчанию", new Vector2(-180, -355), new Vector2(340, 66), 34);
        menu.settingsStartButton = NewButton("SettingsStart", settings, "Начать ночь", new Vector2(180, -355), new Vector2(340, 66), 34);
        tab.summary = TestPlatformBuilder.NewText("Summary", settings, 34);
        tab.summary.color = new Color(0.8f, 0.7f, 1f);
        Place(tab.summary.rectTransform, new Vector2(0, -430), new Vector2(1200, 60));

        // Вкладка «Скилл-чеки»: список раскладок, итог. Само окно мог-батла — на своём канвасе поверх меню
        var checks = Container("ChecksTab", root);
        menu.checksTab = checks.gameObject;
        var checksTitle = TestPlatformBuilder.NewText("Header", checks, 36);
        checksTitle.text = "Проверка скилл-чеков: выбери раскладку. Esc — прервать";
        checksTitle.color = new Color(0.8f, 0.7f, 1f);
        Place(checksTitle.rectTransform, new Vector2(0, 130), new Vector2(1400, 60));
        var listGo = new GameObject("List", typeof(RectTransform));
        listGo.transform.SetParent(checks, false);
        var list = (RectTransform)listGo.transform;
        list.anchorMin = list.anchorMax = list.pivot = new Vector2(0.5f, 1f);
        list.anchoredPosition = new Vector2(0, -470);
        list.sizeDelta = new Vector2(1100, 540);
        var template = NewButton("ChartButton", list, "Раскладка", Vector2.zero, new Vector2(1100, 58), 32);
        var tr = (RectTransform)template.transform;
        tr.anchorMin = tr.anchorMax = tr.pivot = new Vector2(0.5f, 1f);
        template.gameObject.SetActive(false);
        var result = TestPlatformBuilder.NewText("Result", checks, 34);
        result.color = new Color(1f, 0.85f, 0.35f);
        Place(result.rectTransform, new Vector2(0, -470), new Vector2(1600, 60));

        var testHud = new GameObject("MogTestHUD", typeof(Canvas), typeof(CanvasScaler), typeof(MogBattle), typeof(AudioSource));
        var testCanvas = testHud.GetComponent<Canvas>();
        testCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        testCanvas.sortingOrder = 10;
        var testScaler = testHud.GetComponent<CanvasScaler>();
        testScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        testScaler.referenceResolution = new Vector2(1920, 1080);
        var test = testHud.AddComponent<MogTestTab>();
        test.battle = MogBattleBuilder.Build(testHud);
        test.menu = canvasGo.GetComponent<CanvasGroup>();
        test.list = list;
        test.template = template;
        test.result = result;
        menu.checksTest = test;

        EditorSceneManager.SaveScene(scene, MenuScenePath);

        // Меню первым: с него запускается собранная игра
        EditorBuildSettings.scenes = new[]
        {
            new EditorBuildSettingsScene(MenuScenePath, true),
            new EditorBuildSettingsScene(GameScenePath, true),
        };
        EditorPrefs.SetInt(VersionKey, Version);
        Debug.Log("[SSDFNC] Main menu built.");
    }

    // Пустой контейнер на весь экран для содержимого вкладки
    static Transform Container(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        TestPlatformBuilder.Stretch((RectTransform)go.transform);
        return go.transform;
    }

    internal static Button NewButton(string name, Transform parent, string label, Vector2 pos, Vector2 size, int fontSize)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        Place((RectTransform)go.transform, pos, size);

        // Картинка белая, цвет кнопке задаёт ColorTint: обычный, при наведении, при нажатии
        var btn = go.GetComponent<Button>();
        var colors = btn.colors;
        colors.normalColor = new Color(0.18f, 0.12f, 0.24f);
        colors.highlightedColor = new Color(0.45f, 0.25f, 0.6f);
        colors.pressedColor = new Color(0.3f, 0.15f, 0.4f);
        colors.selectedColor = colors.normalColor;
        btn.colors = colors;

        var text = TestPlatformBuilder.NewText("Label", go.transform, fontSize);
        text.text = label;
        TestPlatformBuilder.Stretch(text.rectTransform);
        return btn;
    }

    // Кнопка вкладки: ColorTint почти белый, сам цвет ставит MainMenu.ShowTab
    static Button TabButton(string name, Transform parent, string label, Vector2 pos)
    {
        var btn = NewButton(name, parent, label, pos, new Vector2(340, 80), 38);
        var colors = btn.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.25f, 1.25f, 1.25f);
        colors.pressedColor = new Color(0.8f, 0.8f, 0.8f);
        colors.selectedColor = Color.white;
        btn.colors = colors;
        return btn;
    }

    static void Place(RectTransform r, Vector2 pos, Vector2 size)
    {
        r.anchoredPosition = pos;
        r.sizeDelta = size;
    }
}
