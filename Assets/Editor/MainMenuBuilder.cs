using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class MainMenuBuilder
{
    public const string BootScenePath = "Assets/Scenes/Boot.unity";
    const string MenuScenePath = "Assets/Scenes/MainMenu.unity";
    const string GameScenePath = "Assets/Scenes/SampleScene.unity";

    // Версия сборщика меню, как у TestPlatformBuilder: устарело — Unity предложит пересобрать.
    // 2 — вкладки «Главная» и «Настройка ночи». 3 — счётчик денег. 4 — вкладка «Скилл-чеки».
    // 5 — кнопка редактора раскладок (убрана в 6: редактор — отдельная программа). 7 — сцена Boot.
    // 8 — ночи 1–7. 9 — «Играть» / «Настройки» / «Выход», ночи выезжают списком, настройки: громкость, звуки, скилл-чеки.
    // 10 — «Играть» слева вровень с первой ночью, кнопки ночей короче, всё ближе к центру.
    // 11 — «Своя ночь» — отдельная кнопка: открывает уровни на месте списка ночей.
    // 12 — ночи сеткой, кнопки 200×110, пройденные светлее. 13 — два столбца по три. 14 — без белого мигания кнопок.
    // 15 — «Свет: обычный / неон» в настройках. 16 — скилл-чеки без рамки и ниже. 17 — без надписей фазы и мога.
    const int Version = 17;
    const string VersionNote = "«Играть» / «Настройки» / «Выход»: ночи выезжают списком справа от «Играть», в настройках — громкость, звуки и скилл-чеки";
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

    // Собирает сцены Boot (загрузочный экран при запуске) и главного меню заново и прописывает сцены игры
    // в Build Settings: Boot, меню, офис.
    [MenuItem("SSDFNC/Build Main Menu")]
    public static void Build()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        BuildBoot();
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
        Place(title.rectTransform, new Vector2(0, 375), new Vector2(1600, 200));

        var menu = canvasGo.AddComponent<MainMenu>();

        // Деньги — в правом верхнем углу
        menu.moneyLabel = TestPlatformBuilder.NewText("Money", root, 44);
        menu.moneyLabel.alignment = TextAnchor.UpperRight;
        menu.moneyLabel.color = new Color(1f, 0.85f, 0.35f);
        var money = menu.moneyLabel.rectTransform;
        money.anchorMin = money.anchorMax = money.pivot = Vector2.one;
        money.anchoredPosition = new Vector2(-40, -30);
        money.sizeDelta = new Vector2(600, 70);

        // Главная: «Играть», «Настройки», «Выход». Где что и как выезжает — MainMenu (места ниже — «домашние»)
        menu.playButton = NewButton("PlayButton", root, "Играть", new Vector2(0, 60), new Vector2(560, 110), 52);
        menu.settingsButton = NewButton("SettingsButton", root, "Настройки", new Vector2(0, -70), new Vector2(560, 100), 44);
        menu.quitButton = NewButton("QuitButton", root, "Выход", new Vector2(0, -190), new Vector2(560, 100), 44);
        menu.backButton = NewButton("BackButton", root, "Назад", new Vector2(0, -470), new Vector2(300, 80), 38);

        // Список ночей: кнопки 1–7 и «Своя ночь» (ColorTint почти белый — цвет выбранной ставит MainMenu.Select)
        menu.nightButtons = new Button[Nights.Count];
        menu.nightLabels = new Text[Nights.Count];
        for (int i = 0; i < Nights.Count; i++)
        {
            menu.nightButtons[i] = ItemButton("Night" + (i + 1), root, $"Ночь {i + 1}");
            menu.nightLabels[i] = menu.nightButtons[i].GetComponentInChildren<Text>();
        }
        menu.customButton = ItemButton("CustomNight", root, "Своя ночь");
        ((RectTransform)menu.customButton.transform).sizeDelta = new Vector2(420, 110); // во всю ширину сетки ночей
        menu.selectionLabel = TestPlatformBuilder.NewText("Selection", root, 34);
        menu.selectionLabel.color = new Color(0.8f, 0.7f, 1f);
        Place(menu.selectionLabel.rectTransform, new Vector2(-240, 115), new Vector2(600, 50));

        // «Своя ночь»: уровни фурри — справа, под кнопкой «Своя ночь», когда её открыли (ночи при этом уходят)
        var custom = Container("CustomPanel", root);
        var customRt = (RectTransform)custom;
        customRt.anchorMin = customRt.anchorMax = new Vector2(0.5f, 0.5f);
        customRt.sizeDelta = new Vector2(600, 500);
        customRt.anchoredPosition = new Vector2(310, -95);
        menu.customPanel = customRt;
        var tab = custom.gameObject.AddComponent<NightSettingsTab>();
        string[] names = { "Дир", "Стрей", "Нова", "Дали", "Доли" };
        tab.values = new Text[5];
        tab.minus = new Button[5];
        tab.plus = new Button[5];
        for (int i = 0; i < 5; i++)
        {
            float y = 175 - i * 56;
            var name = TestPlatformBuilder.NewText("Name" + i, custom, 34);
            name.text = names[i];
            name.alignment = TextAnchor.MiddleLeft;
            Place(name.rectTransform, new Vector2(-190, y), new Vector2(180, 52));
            tab.minus[i] = NewButton("Minus" + i, custom, "−", new Vector2(-10, y), new Vector2(60, 48), 38);
            tab.values[i] = TestPlatformBuilder.NewText("Value" + i, custom, 34);
            Place(tab.values[i].rectTransform, new Vector2(80, y), new Vector2(110, 52));
            tab.plus[i] = NewButton("Plus" + i, custom, "+", new Vector2(170, y), new Vector2(60, 48), 38);
        }
        tab.easy = NewButton("Easy", custom, "Easy", new Vector2(-215, -115), new Vector2(120, 52), 26);
        tab.normal = NewButton("Normal", custom, "Normal", new Vector2(-80, -115), new Vector2(140, 52), 26);
        tab.hard = NewButton("Hard", custom, "Hard", new Vector2(55, -115), new Vector2(120, 52), 26);
        tab.onlyNova = NewButton("OnlyNova", custom, "Только Нова", new Vector2(210, -115), new Vector2(180, 52), 24);
        tab.defaults = NewButton("Defaults", custom, "По умолчанию", new Vector2(0, -175), new Vector2(300, 52), 26);
        tab.summary = TestPlatformBuilder.NewText("Summary", custom, 24); // «Normal (6/5/1/6/4)…»
        tab.summary.color = new Color(0.8f, 0.7f, 1f);
        tab.summary.horizontalOverflow = HorizontalWrapMode.Wrap;
        Place(tab.summary.rectTransform, new Vector2(0, -235), new Vector2(580, 60));

        // Настройки: «Громкость», «Звуки», «Скилл-чеки»
        menu.volumeButton = NewButton("VolumeButton", root, "Громкость", new Vector2(0, 60), new Vector2(560, 100), 44);
        menu.soundsButton = NewButton("SoundsButton", root, "Звуки", new Vector2(0, -60), new Vector2(560, 100), 44);
        menu.checksButton = NewButton("ChecksButton", root, "Скилл-чеки", new Vector2(0, -180), new Vector2(560, 100), 44);
        menu.lightButton = NewButton("LightButton", root, "Свет: обычный", new Vector2(0, -300), new Vector2(560, 100), 44);

        // Громкость: ползунок на группу, справа проценты (VolumeSettingsPanel)
        var volume = Container("VolumePanel", root);
        menu.volumePanel = (RectTransform)volume;
        var volumeTitle = TestPlatformBuilder.NewText("Header", volume, 44);
        volumeTitle.text = "Громкость";
        volumeTitle.color = new Color(0.8f, 0.6f, 1f);
        Place(volumeTitle.rectTransform, new Vector2(0, 170), new Vector2(800, 70));
        var vol = volume.gameObject.AddComponent<VolumeSettingsPanel>();
        int groups = GameSounds.GroupNames.Length;
        vol.sliders = new Slider[groups];
        vol.values = new Text[groups];
        var sliderRes = new DefaultControls.Resources
        {
            standard = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd"),
            background = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd"),
            knob = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd"),
        };
        for (int i = 0; i < groups; i++)
        {
            float y = 80 - i * 80;
            var label = TestPlatformBuilder.NewText("Label" + i, volume, 36);
            label.text = GameSounds.GroupNames[i];
            label.alignment = TextAnchor.MiddleRight;
            Place(label.rectTransform, new Vector2(-300, y), new Vector2(440, 60));
            var sliderGo = DefaultControls.CreateSlider(sliderRes);
            sliderGo.name = "Slider" + i;
            sliderGo.transform.SetParent(volume, false);
            Place((RectTransform)sliderGo.transform, new Vector2(170, y), new Vector2(440, 36));
            var slider = sliderGo.GetComponent<Slider>();
            slider.transform.Find("Background").GetComponent<Image>().color = new Color(0.25f, 0.18f, 0.32f);
            slider.fillRect.GetComponent<Image>().color = new Color(0.55f, 0.3f, 0.75f);
            slider.handleRect.GetComponent<Image>().color = new Color(1f, 0.85f, 0.35f);
            ((RectTransform)slider.handleRect).sizeDelta = new Vector2(36, 0);
            vol.sliders[i] = slider;
            vol.values[i] = TestPlatformBuilder.NewText("Value" + i, volume, 34);
            vol.values[i].color = new Color(1f, 0.85f, 0.35f);
            Place(vol.values[i].rectTransform, new Vector2(470, y), new Vector2(140, 60));
        }

        // Скилл-чеки: список раскладок, итог. Само окно мог-батла — на своём канвасе поверх меню
        var checks = Container("ChecksPanel", root);
        menu.checksPanel = (RectTransform)checks;
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
        Place(result.rectTransform, new Vector2(0, -400), new Vector2(1600, 60));

        // Звуки: то же окно, что в паузе офиса (строит себя само при первом открытии)
        var soundsGo = new GameObject("SoundsPanel", typeof(RectTransform), typeof(SoundSettingsPanel));
        soundsGo.transform.SetParent(root, false);
        TestPlatformBuilder.Stretch((RectTransform)soundsGo.transform);
        menu.soundsPanel = soundsGo.GetComponent<SoundSettingsPanel>();

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

        // Boot первым: с него запускается собранная игра (загрузочный экран, потом меню)
        EditorBuildSettings.scenes = new[]
        {
            new EditorBuildSettingsScene(BootScenePath, true),
            new EditorBuildSettingsScene(MenuScenePath, true),
            new EditorBuildSettingsScene(GameScenePath, true),
        };
        EditorPrefs.SetInt(VersionKey, Version);
        Debug.Log("[SSDFNC] Main menu built.");
    }

    // Сцена Boot: камера и Boot — он сразу показывает загрузочный экран (SceneLoader) и грузит меню
    static void BuildBoot()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var cam = new GameObject("Main Camera", typeof(Camera)).GetComponent<Camera>();
        cam.tag = "MainCamera";
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.07f, 0.04f, 0.1f);
        new GameObject("Boot", typeof(Boot));
        EditorSceneManager.SaveScene(scene, BootScenePath);
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
        colors.disabledColor = new Color(0.11f, 0.08f, 0.14f); // выключенная — темнее (у Unity по умолчанию почти белая)
        btn.colors = colors;

        var text = TestPlatformBuilder.NewText("Label", go.transform, fontSize);
        text.text = label;
        TestPlatformBuilder.Stretch(text.rectTransform);
        return btn;
    }

    // Кнопка ночи: ColorTint почти белый, сам цвет (выбрана, пройдена, нет) ставит MainMenu.Select.
    // Место — тоже MainMenu (сетка строится по тому, какие ночи открыты)
    static Button ItemButton(string name, Transform parent, string label)
    {
        var btn = NewButton(name, parent, label, Vector2.zero, new Vector2(200, 110), 34); // высотой с «Играть»
        var colors = btn.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.25f, 1.25f, 1.25f);
        colors.pressedColor = new Color(0.8f, 0.8f, 0.8f);
        colors.selectedColor = Color.white;
        colors.disabledColor = new Color(0.6f, 0.6f, 0.6f);
        btn.colors = colors;
        return btn;
    }

    static void Place(RectTransform r, Vector2 pos, Vector2 size)
    {
        r.anchoredPosition = pos;
        r.sizeDelta = size;
    }
}
