using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class TestPlatformBuilder
{
    const string ScenePath = "Assets/Scenes/SampleScene.unity";
    const string RootName = "TestPlatform";
    const string ShiftName = "Shift";

    // Версия сборщика. Увеличивай, когда сборщик начинает строить офис по-новому.
    // Если офис собран старой версией, Unity при загрузке скриптов предложит его пересобрать.
    // 2 — добавлен генератор. 3 — стены с дверными проёмами, генератор за проёмом слева, к нему можно подойти.
    // 4 — отпугивающие факторы: стол с кнопками дэнс пола и звука, дэнс пол впереди, дверь с рычагом справа.
    // 5 — пробел: дэнс пол (если смотришь вперёд) и рычаг двери (если смотришь вправо).
    // 6 — фурри: директор ночи, 8 камер с кормлением Дира, фигуры фурри, экран смерти, мог-батл.
    // 7 — панель сложности F1 во время ночи.
    const int Version = 8;
    const string VersionNote = "атака Новы по фазам: фаза в правом верхнем углу, скилл-чеки по раскладкам под музыку";
    static string VersionKey => "SSDFNC.TestPlatformVersion." + Application.dataPath;

    [InitializeOnLoadMethod]
    static void OfferRebuildIfOutdated()
    {
        if (Application.isBatchMode || EditorPrefs.GetInt(VersionKey, 0) >= Version) return;
        if (SessionState.GetBool("SSDFNC.RebuildAsked." + Version, false)) return; // спрашиваем раз за запуск Unity

        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            SessionState.SetBool("SSDFNC.RebuildAsked." + Version, true);
            if (EditorUtility.DisplayDialog("SSDFNC",
                    $"Сборщик офиса обновился: {VersionNote}.\nПересобрать сцену офиса сейчас?",
                    "Пересобрать", "Позже"))
                Build();
        };
    }

    [MenuItem("SSDFNC/Build Test Platform")]
    public static void Build()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        var old = GameObject.Find(RootName);
        if (old != null) Object.DestroyImmediate(old);

        var root = new GameObject(RootName);

        // Центральная платформа
        Make(PrimitiveType.Cube, "CenterPlatform", root.transform,
            new Vector3(0, -0.25f, 0), new Vector3(8, 0.5f, 8), new Color(0.35f, 0.35f, 0.4f));

        // Три направления: влево (там генератор, поэтому без объекта на конце), вперёд, вправо
        BuildArm(root.transform, "Left",    Vector3.left,    new Color(0.8f, 0.25f, 0.25f), null);
        BuildArm(root.transform, "Forward", Vector3.forward, new Color(0.25f, 0.7f, 0.3f),  PrimitiveType.Sphere);
        BuildArm(root.transform, "Right",   Vector3.right,   new Color(0.25f, 0.4f, 0.85f), PrimitiveType.Cylinder);

        // Стены офиса слева и справа, в каждой дверной проём: слева — к генератору, справа — в коридор 2
        BuildWallWithDoorway(root.transform, "LeftWall", -3.5f);
        BuildWallWithDoorway(root.transform, "RightWall", 3.5f);

        // Камера в центре платформы, поворачивается на три стороны
        var cam = Camera.main;
        if (cam != null)
        {
            cam.transform.position = new Vector3(0, 1.6f, 0);
            cam.transform.rotation = Quaternion.identity;
            if (cam.GetComponent<CameraLook>() == null) cam.gameObject.AddComponent<CameraLook>();
            // Чтобы мышью можно было кликать по 3D-объектам (ручка генератора)
            if (cam.GetComponent<UnityEngine.EventSystems.PhysicsRaycaster>() == null)
                cam.gameObject.AddComponent<UnityEngine.EventSystems.PhysicsRaycaster>();
        }

        var generator = BuildGenerator(root.transform, cam);
        var monitor = BuildMonitor(root.transform, cam);
        monitor.generator = generator;

        // Отпугивающие факторы: стол с кнопками дэнс пола и звука, дэнс пол впереди, дверь с рычагом справа
        var (danceFloor, sound) = BuildDesk(root.transform, cam, generator);
        var door = BuildDoor(root.transform, cam, generator);

        // Пробел — действие в ту сторону, куда смотришь
        var space = new GameObject("SpaceAction", typeof(SpaceAction)).GetComponent<SpaceAction>();
        space.transform.SetParent(root.transform, false);
        space.look = cam != null ? cam.GetComponent<CameraLook>() : null;
        space.danceFloor = danceFloor;
        space.door = door;

        var oldShift = GameObject.Find(ShiftName);
        if (oldShift != null) Object.DestroyImmediate(oldShift);
        BuildShift();
        var shift = GameObject.Find(ShiftName).GetComponent<ShiftTimer>();

        // Фурри: директор ночи, фигуры в офисе, предупреждения, экран смерти, мог-батл
        var director = BuildFurries(root.transform, cam, generator, door, danceFloor, sound, space, shift);
        monitor.director = director;
        BuildDifficultyPanel(root.transform, director);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        EditorPrefs.SetInt(VersionKey, Version);
        Debug.Log("[SSDFNC] Test platform built.");
    }

    // Экран-монитор и ряд кнопок выбора камеры: справа внизу в поле зрения «вперёд»,
    // при повороте вправо (±45° от центра обзора) не видны.
    static CameraMonitor BuildMonitor(Transform parent, Camera cam)
    {
        var oldEs = Object.FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>();
        if (oldEs != null) Object.DestroyImmediate(oldEs.gameObject);
        new GameObject("EventSystem",
            typeof(UnityEngine.EventSystems.EventSystem),
            typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));

        // Экран 640×380 px: название камеры, кто в комнате, на камере 7 — кнопка «ПОКОРМИТЬ»
        var screenCanvas = NewCanvas("MonitorPanel", parent, cam, new Vector2(640, 380), pitch: 12f, yaw: 28f);
        var screen = NewImage("Screen", screenCanvas.transform, new Color(0.04f, 0.1f, 0.06f));
        Stretch(screen.rectTransform);
        var title = NewText("Title", screenCanvas.transform, 40);
        title.alignment = TextAnchor.UpperLeft;
        Place(title.rectTransform, new Vector2(0, 150), new Vector2(600, 60));
        var body = NewText("Body", screenCanvas.transform, 38);
        body.alignment = TextAnchor.UpperLeft;
        body.color = new Color(0.7f, 1f, 0.75f);
        Place(body.rectTransform, new Vector2(0, 10), new Vector2(600, 220));

        var feedGo = new GameObject("FeedButton", typeof(RectTransform), typeof(UnityEngine.UI.Image),
            typeof(UnityEngine.UI.Button), typeof(HoldButton));
        feedGo.transform.SetParent(screenCanvas.transform, false);
        Place((RectTransform)feedGo.transform, new Vector2(0, -145), new Vector2(440, 70));
        var feedBtn = feedGo.GetComponent<UnityEngine.UI.Button>();
        var feedColors = feedBtn.colors;
        feedColors.normalColor = new Color(0.15f, 0.45f, 0.2f);
        feedColors.highlightedColor = new Color(0.25f, 0.6f, 0.3f);
        feedColors.pressedColor = new Color(0.4f, 0.8f, 0.45f);
        feedColors.selectedColor = feedColors.normalColor;
        feedBtn.colors = feedColors;
        var feedLabel = NewText("Label", feedGo.transform, 34);
        feedLabel.text = "ПОКОРМИТЬ (держать)";
        Stretch(feedLabel.rectTransform);

        // Кнопки камер: комнаты 2–8 и проход Г
        var buttonCanvas = NewCanvas("CameraButtons", parent, cam, new Vector2(500, 80), pitch: 24f, yaw: 28f);
        int count = Rooms.Cameras.Length;
        var buttons = new UnityEngine.UI.Button[count];
        const float size = 54, gap = 8;
        for (int i = 0; i < count; i++)
        {
            string camLabel = Rooms.CameraLabel(Rooms.Cameras[i]);
            var go = new GameObject($"CamButton{camLabel}", typeof(RectTransform), typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.Button));
            go.transform.SetParent(buttonCanvas.transform, false);
            Place((RectTransform)go.transform, new Vector2((i - (count - 1) / 2f) * (size + gap), 0), new Vector2(size, size));
            var btn = go.GetComponent<UnityEngine.UI.Button>();
            btn.transition = UnityEngine.UI.Selectable.Transition.None;
            buttons[i] = btn;

            var num = NewText("Number", go.transform, 34);
            num.text = camLabel;
            Stretch(num.rectTransform);
        }

        var monitor = screenCanvas.gameObject.AddComponent<CameraMonitor>();
        monitor.screen = screen;
        monitor.title = title;
        monitor.body = body;
        monitor.buttons = buttons;
        monitor.feedButton = feedGo.GetComponent<HoldButton>();
        monitor.feedLabel = feedLabel;
        return monitor;
    }

    // Генератор за дверным проёмом слева, в 6.5 м от центра офиса: корпус, ручка стартера, табло
    // и точка GeneratorSpot, куда встаёт камера, когда игрок подходит (A, когда уже смотришь влево).
    // Точка самого генератора — центр основания корпуса, от неё считается «достаёт ли игрок».
    static Generator BuildGenerator(Transform parent, Camera cam)
    {
        var go = new GameObject("Generator", typeof(Generator));
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(-6.5f, 0f, 0f);
        var generator = go.GetComponent<Generator>();

        Make(PrimitiveType.Cube, "GeneratorBody", go.transform,
            new Vector3(0f, 0.45f, 0f), new Vector3(0.7f, 0.9f, 1.2f), new Color(0.75f, 0.45f, 0.1f));

        // Камера у генератора: в 1.5 м от передней стенки, смотрит на него и на 20° вниз
        var spot = new GameObject("GeneratorSpot").transform;
        spot.SetParent(go.transform, false);
        spot.localPosition = new Vector3(1.85f, 1.6f, 0f);
        spot.localRotation = Quaternion.Euler(20f, -90f, 0f);
        if (cam != null) cam.GetComponent<CameraLook>().generatorSpot = spot;

        // Ручка на передней стенке, справа от игрока. Её ось Z смотрит на игрока — туда она и тянется.
        var handle = new GameObject("StarterHandle", typeof(GeneratorHandle));
        handle.transform.SetParent(go.transform, false);
        handle.transform.localPosition = new Vector3(0.35f, 0.7f, 0.4f);
        handle.transform.localRotation = Quaternion.LookRotation(Vector3.right);
        handle.GetComponent<GeneratorHandle>().generator = generator;

        // Тросик наполовину уходит в корпус, рукоять поперёк на его конце
        var cord = Make(PrimitiveType.Cylinder, "GeneratorCord", handle.transform,
            Vector3.zero, new Vector3(0.025f, 0.15f, 0.025f), new Color(0.15f, 0.15f, 0.15f));
        cord.transform.localRotation = Quaternion.Euler(90, 0, 0);
        var grip = Make(PrimitiveType.Cylinder, "GeneratorGrip", handle.transform,
            new Vector3(0, 0, 0.17f), new Vector3(0.09f, 0.15f, 0.09f), new Color(0.12f, 0.12f, 0.12f));
        grip.transform.localRotation = Quaternion.Euler(0, 0, 90);

        BuildGeneratorPanel(go.transform, cam, generator, spot.position);
        return generator;
    }

    // Табло 1000×520 px (1.3 × 0.68 м) на корпусе генератора, лицом к глазам игрока у генератора
    static void BuildGeneratorPanel(Transform parent, Camera cam, Generator generator, Vector3 eye)
    {
        var canvas = NewCanvasAt("GeneratorPanel", parent, cam, new Vector2(1000, 520),
            parent.position + new Vector3(-0.1f, 1.3f, 0f), scale: 0.0013f, lookFrom: eye);
        var t = canvas.transform;
        Stretch(NewImage("Background", t, new Color(0.08f, 0.08f, 0.1f)).rectTransform);

        var panel = canvas.gameObject.AddComponent<GeneratorPanel>();
        panel.generator = generator;

        panel.lamp = NewImage("Lamp", t, Color.green);
        Place(panel.lamp.rectTransform, new Vector2(-430, 190), new Vector2(70, 70));
        panel.stateLabel = NewText("State", t, 66);
        panel.stateLabel.alignment = TextAnchor.MiddleLeft;
        Place(panel.stateLabel.rectTransform, new Vector2(60, 190), new Vector2(840, 90));

        var healthTitle = NewText("HealthTitle", t, 54);
        healthTitle.text = "HP";
        Place(healthTitle.rectTransform, new Vector2(-385, 90), new Vector2(190, 60));
        panel.healthFill = NewBar("HealthBar", t, new Vector2(100, 90), new Color(0.85f, 0.15f, 0.15f));

        var fuelTitle = NewText("FuelTitle", t, 54);
        fuelTitle.text = "Бензин";
        Place(fuelTitle.rectTransform, new Vector2(-385, -10), new Vector2(190, 60));
        panel.fuelFill = NewBar("FuelBar", t, new Vector2(100, -10), new Color(1f, 0.6f, 0.1f));

        panel.reserveLabel = NewText("Reserve", t, 46);
        panel.reserveLabel.alignment = TextAnchor.MiddleLeft;
        Place(panel.reserveLabel.rectTransform, new Vector2(100, -80), new Vector2(720, 70));

        // Кнопка «Залить»: Button даёт подсветку, HoldButton — «держат ли её сейчас»
        var btnGo = new GameObject("RefuelButton", typeof(RectTransform), typeof(UnityEngine.UI.Image),
            typeof(UnityEngine.UI.Button), typeof(HoldButton));
        btnGo.transform.SetParent(t, false);
        Place((RectTransform)btnGo.transform, new Vector2(0, -180), new Vector2(560, 100));
        var btn = btnGo.GetComponent<UnityEngine.UI.Button>();
        var colors = btn.colors;
        colors.normalColor = new Color(0.55f, 0.32f, 0.05f);
        colors.highlightedColor = new Color(0.8f, 0.5f, 0.1f);
        colors.pressedColor = new Color(1f, 0.65f, 0.15f);
        colors.selectedColor = colors.normalColor;
        btn.colors = colors;
        var btnText = NewText("Label", btnGo.transform, 54);
        btnText.text = "ЗАЛИТЬ (держать)";
        Stretch(btnText.rectTransform);
        panel.refuelButton = btnGo.GetComponent<HoldButton>();
    }

    // Стол перед игроком (под монитором), на нём кнопки дэнс пола и звука с подписями
    static (DanceFloor, SoundSignal) BuildDesk(Transform parent, Camera cam, Generator generator)
    {
        Make(PrimitiveType.Cube, "Desk", parent, new Vector3(0.1f, 0.45f, 1.3f), new Vector3(1.9f, 0.9f, 1.0f),
            new Color(0.35f, 0.25f, 0.18f));

        // Дэнс пол: 3×3 плитки на полу коридора впереди (z от 5 до 8) и свет над ними
        var floorGo = new GameObject("DanceFloor", typeof(DanceFloor));
        floorGo.transform.SetParent(parent, false);
        var floor = floorGo.GetComponent<DanceFloor>();
        floor.generator = generator;
        floor.tiles = new Renderer[9];
        for (int i = 0; i < 9; i++)
        {
            int x = i % 3 - 1, z = i / 3 - 1;
            floor.tiles[i] = Make(PrimitiveType.Cube, $"DanceTile{i + 1}", floorGo.transform,
                new Vector3(x, 0.01f, 6.5f + z), new Vector3(0.95f, 0.02f, 0.95f), new Color(0.1f, 0.1f, 0.12f))
                .GetComponent<Renderer>();
        }
        var glow = new GameObject("DanceLight", typeof(Light)).GetComponent<Light>();
        glow.transform.SetParent(floorGo.transform, false);
        glow.transform.localPosition = new Vector3(0f, 2.5f, 6.5f);
        glow.type = LightType.Point;
        glow.range = 7f;
        glow.intensity = 4f;
        glow.enabled = false;
        floor.glow = glow;
        BuildDeskButton(parent, "DanceButton", new Vector3(-0.45f, 0.9f, 1.45f), new Color(0.9f, 0.2f, 0.8f), floor);

        // Кнопка звука
        var soundGo = new GameObject("SoundSignal", typeof(SoundSignal), typeof(AudioSource));
        soundGo.transform.SetParent(parent, false);
        var signal = soundGo.GetComponent<SoundSignal>();
        signal.generator = generator;
        signal.source = soundGo.GetComponent<AudioSource>();
        signal.source.playOnAwake = false;
        signal.lamp = BuildDeskButton(parent, "SoundButton", new Vector3(-0.15f, 0.9f, 1.45f), new Color(0.85f, 0.1f, 0.1f), signal);

        // Подписи за кнопками
        var labels = NewCanvasAt("DeskLabels", parent, cam, new Vector2(900, 90), new Vector3(-0.3f, 0.98f, 1.72f), scale: 0.0006f);
        var danceLabel = NewText("DanceLabel", labels.transform, 44);
        danceLabel.text = "ДЭНС ПОЛ (держать, пробел)";
        Place(danceLabel.rectTransform, new Vector2(-250, 0), new Vector2(420, 90));
        var soundLabel = NewText("SoundLabel", labels.transform, 44);
        soundLabel.text = "ЗВУК";
        Place(soundLabel.rectTransform, new Vector2(250, 0), new Vector2(300, 90));
        return (floor, signal);
    }

    // Фурри: директор ночи (NightDirector), места, где фурри видно из офиса, фигуры-заглушки,
    // экран с предупреждением, экран смерти и окно мог-батла
    static NightDirector BuildFurries(Transform parent, Camera cam, Generator generator, Door door, DanceFloor danceFloor,
        SoundSignal sound, SpaceAction space, ShiftTimer shift)
    {
        var go = new GameObject("NightDirector", typeof(NightDirector), typeof(AudioSource));
        go.transform.SetParent(parent, false);
        var director = go.GetComponent<NightDirector>();
        director.generator = generator;
        director.door = door;
        director.danceFloor = danceFloor;
        director.sound = sound;
        director.look = cam != null ? cam.GetComponent<CameraLook>() : null;
        director.space = space;
        director.shift = shift;
        director.alarm = go.GetComponent<AudioSource>();
        director.alarm.playOnAwake = false;

        // Где фурри видно из офиса. Второй и следующий встают вдоль оси right места
        director.atDoor = NewSpot("FurriesAtDoor", go.transform, new Vector3(4.4f, 0f, 0f), 0f);          // за правым проёмом
        director.onDanceFloor = NewSpot("FurriesOnDanceFloor", go.transform, new Vector3(0f, 0f, 6.5f), 0f);
        director.inPassage = NewSpot("FurriesInPassage", go.transform, new Vector3(-10.5f, 0f, 0f), 180f); // за генератором
        director.atGenerator = NewSpot("FurriesAtGenerator", go.transform, new Vector3(-6.5f, 0f, -1f), 180f);

        // Фигуры-заглушки: капсула с именем, по FurryId (у Дира нет — его видно только на камере)
        director.avatars = new FurryAvatar[5];
        director.avatars[(int)FurryId.Stray] = NewAvatar(go.transform, cam, "Стрей", new Color(0.95f, 0.8f, 0.2f));
        director.avatars[(int)FurryId.Nova] = NewAvatar(go.transform, cam, "Нова", new Color(0.6f, 0.3f, 0.9f));
        director.avatars[(int)FurryId.Dali] = NewAvatar(go.transform, cam, "Дали", new Color(0.2f, 0.8f, 0.85f));
        director.avatars[(int)FurryId.Doli] = NewAvatar(go.transform, cam, "Доли", new Color(1f, 0.45f, 0.7f));

        // Экран поверх игры: предупреждение, смерть, мог-батл
        var hud = new GameObject("FurryHUD", typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler), typeof(MogBattle),
            typeof(AudioSource));
        hud.transform.SetParent(parent, false);
        var canvas = hud.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;
        var scaler = hud.GetComponent<UnityEngine.UI.CanvasScaler>();
        scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        director.warning = NewText("Warning", hud.transform, 64);
        director.warning.color = new Color(1f, 0.25f, 0.2f);
        var w = director.warning.rectTransform;
        w.anchorMin = w.anchorMax = w.pivot = new Vector2(0.5f, 1f);
        Place(w, new Vector2(0, -40), new Vector2(1200, 100));

        var death = NewImage("DeathPanel", hud.transform, new Color(0.45f, 0f, 0f, 0.92f));
        Stretch(death.rectTransform);
        director.deathPanel = death.gameObject;
        director.deathText = NewText("DeathText", death.transform, 110);
        Stretch(director.deathText.rectTransform);

        var battle = hud.GetComponent<MogBattle>();
        var panel = NewImage("MogBattlePanel", hud.transform, new Color(0.05f, 0.02f, 0.1f, 0.88f));
        Place(panel.rectTransform, new Vector2(0, -80), new Vector2(1200, 460));
        battle.title = NewText("Title", panel.transform, 64);
        battle.title.text = "МОГ-БАТЛ С НОВОЙ";
        battle.title.color = new Color(0.8f, 0.6f, 1f);
        Place(battle.title.rectTransform, new Vector2(0, 160), new Vector2(1100, 90));
        var hint = NewText("Hint", panel.transform, 40);
        hint.text = "ПРОБЕЛ — когда метка в зелёной зоне";
        Place(hint.rectTransform, new Vector2(0, 85), new Vector2(1100, 60));
        var bar = NewImage("Bar", panel.transform, new Color(0.25f, 0.25f, 0.3f));
        Place(bar.rectTransform, new Vector2(0, -10), new Vector2(950, 60));
        var zone = NewImage("Zone", bar.transform, new Color(0.2f, 0.85f, 0.3f));
        Stretch(zone.rectTransform);
        var marker = NewImage("Marker", bar.transform, Color.white);
        marker.rectTransform.sizeDelta = new Vector2(14, 100);
        battle.score = NewText("Score", panel.transform, 44);
        Place(battle.score.rectTransform, new Vector2(0, -130), new Vector2(1100, 70));
        battle.panel = panel.gameObject;
        battle.zone = zone.rectTransform;
        battle.marker = marker.rectTransform;
        panel.gameObject.SetActive(false);

        // Фаза атаки Новы — в правом верхнем углу, под часами смены
        battle.phaseLabel = NewText("MogPhase", hud.transform, 40);
        battle.phaseLabel.alignment = TextAnchor.UpperRight;
        battle.phaseLabel.color = new Color(0.8f, 0.6f, 1f);
        var pl = battle.phaseLabel.rectTransform;
        pl.anchorMin = pl.anchorMax = pl.pivot = Vector2.one;
        pl.anchoredPosition = new Vector2(-40, -110);
        pl.sizeDelta = new Vector2(900, 60);
        battle.phaseLabel.text = "";

        battle.music = hud.GetComponent<AudioSource>();
        battle.music.playOnAwake = false;
        battle.music.spatialBlend = 0f;
        director.battle = battle;

        return director;
    }

    // Панель сложности F1: слева экрана, строка на каждого фурри (− уровень +), пресеты и кнопки для Новы
    static void BuildDifficultyPanel(Transform parent, NightDirector director)
    {
        var go = new GameObject("DifficultyHUD", typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler),
            typeof(UnityEngine.UI.GraphicRaycaster), typeof(DifficultyPanel));
        go.transform.SetParent(parent, false);
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 20;
        var scaler = go.GetComponent<UnityEngine.UI.CanvasScaler>();
        scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        var ui = go.GetComponent<DifficultyPanel>();
        ui.director = director;

        ui.hint = NewText("Hint", go.transform, 30);
        ui.hint.text = "F1 — сложность";
        ui.hint.color = new Color(1f, 1f, 1f, 0.5f);
        ui.hint.alignment = TextAnchor.LowerLeft;
        var h = ui.hint.rectTransform;
        h.anchorMin = h.anchorMax = h.pivot = Vector2.zero;
        Place(h, new Vector2(20, 14), new Vector2(400, 50));

        var panel = NewImage("Panel", go.transform, new Color(0.06f, 0.06f, 0.08f, 0.92f));
        var pr = panel.rectTransform;
        pr.anchorMin = pr.anchorMax = pr.pivot = new Vector2(0f, 0.5f);
        Place(pr, new Vector2(20, 0), new Vector2(560, 640));
        ui.panel = panel.gameObject;

        var title = NewText("Title", panel.transform, 40);
        title.text = "СЛОЖНОСТЬ (F1)";
        Place(title.rectTransform, new Vector2(0, 280), new Vector2(520, 60));

        string[] names = { "Дир", "Стрей", "Нова", "Дали", "Доли" };
        ui.values = new UnityEngine.UI.Text[5];
        ui.minus = new UnityEngine.UI.Button[5];
        ui.plus = new UnityEngine.UI.Button[5];
        for (int i = 0; i < 5; i++)
        {
            float y = 200 - i * 70;
            var name = NewText("Name" + i, panel.transform, 36);
            name.text = names[i];
            name.alignment = TextAnchor.MiddleLeft;
            Place(name.rectTransform, new Vector2(-150, y), new Vector2(200, 60));
            ui.minus[i] = PanelButton(panel.transform, "Minus" + i, "−", new Vector2(20, y), new Vector2(64, 56));
            ui.values[i] = NewText("Value" + i, panel.transform, 34);
            Place(ui.values[i].rectTransform, new Vector2(120, y), new Vector2(130, 60));
            ui.plus[i] = PanelButton(panel.transform, "Plus" + i, "+", new Vector2(220, y), new Vector2(64, 56));
        }

        ui.easy = PanelButton(panel.transform, "Easy", "Easy", new Vector2(-170, -175), new Vector2(160, 56));
        ui.normal = PanelButton(panel.transform, "Normal", "Normal", new Vector2(0, -175), new Vector2(160, 56));
        ui.hard = PanelButton(panel.transform, "Hard", "Hard", new Vector2(170, -175), new Vector2(160, 56));
        ui.onlyNova = PanelButton(panel.transform, "OnlyNova", "Только Нова", new Vector2(-130, -245), new Vector2(240, 56));
        ui.novaBattle = PanelButton(panel.transform, "NovaBattle", "Нова → батл", new Vector2(130, -245), new Vector2(240, 56));

        ui.info = NewText("Info", panel.transform, 26);
        ui.info.color = new Color(0.75f, 0.75f, 0.8f);
        Place(ui.info.rectTransform, new Vector2(0, -298), new Vector2(520, 50));
        panel.gameObject.SetActive(false);
    }

    static UnityEngine.UI.Button PanelButton(Transform parent, string name, string label, Vector2 pos, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.Button));
        go.transform.SetParent(parent, false);
        Place((RectTransform)go.transform, pos, size);
        var btn = go.GetComponent<UnityEngine.UI.Button>();
        var colors = btn.colors;
        colors.normalColor = new Color(0.22f, 0.22f, 0.28f);
        colors.highlightedColor = new Color(0.35f, 0.35f, 0.45f);
        colors.pressedColor = new Color(0.5f, 0.4f, 0.7f);
        colors.selectedColor = colors.normalColor;
        btn.colors = colors;
        var text = NewText("Label", go.transform, 30);
        text.text = label;
        Stretch(text.rectTransform);
        return btn;
    }

    static Transform NewSpot(string name, Transform parent, Vector3 position, float yaw)
    {
        var t = new GameObject(name).transform;
        t.SetParent(parent, false);
        t.position = position;
        t.rotation = Quaternion.Euler(0f, yaw, 0f);
        return t;
    }

    // Капсула ростом 1.8 м и табличка с именем над ней. Без коллайдера, чтобы не мешать кликам
    static FurryAvatar NewAvatar(Transform parent, Camera cam, string name, Color color)
    {
        var go = new GameObject("Furry " + name, typeof(FurryAvatar));
        go.transform.SetParent(parent, false);
        var body = Make(PrimitiveType.Capsule, "FurryBody" + System.Array.IndexOf(new[] { "Стрей", "Нова", "Дали", "Доли" }, name),
            go.transform, new Vector3(0f, 0.9f, 0f), new Vector3(0.7f, 0.9f, 0.7f), color);
        Object.DestroyImmediate(body.GetComponent<Collider>());

        var label = NewCanvasAt("Label", go.transform, cam, new Vector2(300, 80), new Vector3(0f, 2.05f, 0f), scale: 0.002f);
        var text = NewText("Name", label.transform, 60);
        text.text = name;
        text.color = color;
        Stretch(text.rectTransform);

        var avatar = go.GetComponent<FurryAvatar>();
        avatar.label = label.transform;
        avatar.body = body.transform;
        go.SetActive(false);
        return avatar;
    }

    // Круглая кнопка на столе: тёмное основание и цветная шляпка, которая утапливается (PressControl).
    // Возвращает Renderer шляпки.
    static Renderer BuildDeskButton(Transform parent, string name, Vector3 pos, Color color, Deterrent target)
    {
        var go = new GameObject(name, typeof(PressControl));
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        Make(PrimitiveType.Cylinder, name + "Base", go.transform,
            new Vector3(0f, 0.01f, 0f), new Vector3(0.18f, 0.01f, 0.18f), new Color(0.15f, 0.15f, 0.15f));
        var cap = Make(PrimitiveType.Cylinder, name + "Cap", go.transform,
            new Vector3(0f, 0.03f, 0f), new Vector3(0.13f, 0.015f, 0.13f), color);
        var control = go.GetComponent<PressControl>();
        control.target = target;
        control.cap = cap.transform;
        return cap.GetComponent<Renderer>();
    }

    // Дверь в правом проёме: створка уезжает в стену, рычаг на стене справа от проёма (если смотреть
    // из офиса), над рычагом табличка с состоянием и обратным отсчётом
    static Door BuildDoor(Transform parent, Camera cam, Generator generator)
    {
        var go = new GameObject("Door", typeof(Door));
        go.transform.SetParent(parent, false);
        var door = go.GetComponent<Door>();
        door.generator = generator;

        door.slabClosed = new Vector3(3.5f, 1.1f, 0f);   // закрывает проём 1.2 × 2.2
        door.slabOpen = new Vector3(3.5f, 1.1f, 1.25f);  // спрятана в стене рядом с проёмом
        door.slab = Make(PrimitiveType.Cube, "DoorSlab", go.transform, door.slabOpen,
            new Vector3(0.08f, 2.2f, 1.2f), new Color(0.35f, 0.4f, 0.45f)).transform;

        Make(PrimitiveType.Cube, "DoorLeverPlate", go.transform, new Vector3(3.38f, 1.3f, -1.15f),
            new Vector3(0.04f, 0.35f, 0.2f), new Color(0.25f, 0.25f, 0.27f));
        var lever = new GameObject("DoorLever", typeof(PressControl));
        lever.transform.SetParent(go.transform, false);
        lever.transform.localPosition = new Vector3(3.34f, 1.3f, -1.15f);
        lever.transform.localRotation = Quaternion.Euler(0f, 0f, 30f); // открыто: рычаг вверх
        lever.GetComponent<PressControl>().target = door;
        door.lever = lever.transform;
        Make(PrimitiveType.Cylinder, "DoorLeverArm", lever.transform,
            new Vector3(0f, 0.15f, 0f), new Vector3(0.04f, 0.15f, 0.04f), new Color(0.15f, 0.15f, 0.15f));
        Make(PrimitiveType.Sphere, "DoorLeverKnob", lever.transform,
            new Vector3(0f, 0.3f, 0f), Vector3.one * 0.09f, new Color(0.85f, 0.1f, 0.1f));

        // Табличка висит на стене (параллельно ей), поэтому смотрит строго по +X
        var panelPos = new Vector3(3.37f, 1.85f, -1.15f);
        var panel = NewCanvasAt("DoorPanel", go.transform, cam, new Vector2(520, 130), panelPos,
            scale: 0.0018f, lookFrom: panelPos - Vector3.right);
        Stretch(NewImage("Background", panel.transform, new Color(0.08f, 0.08f, 0.1f)).rectTransform);
        door.lamp = NewImage("Lamp", panel.transform, Color.green);
        Place(door.lamp.rectTransform, new Vector2(-205, 0), new Vector2(70, 70));
        door.label = NewText("State", panel.transform, 60);
        door.label.alignment = TextAnchor.MiddleLeft;
        Place(door.label.rectTransform, new Vector2(45, 0), new Vector2(400, 110));
        return door;
    }

    // Полоска 720×56: тёмный фон и цветная заливка. Ширину заливки задаёт скрипт через anchorMax.x
    static RectTransform NewBar(string name, Transform parent, Vector2 pos, Color color)
    {
        var bg = NewImage(name, parent, new Color(0.22f, 0.22f, 0.25f));
        Place(bg.rectTransform, pos, new Vector2(720, 56));
        var fill = NewImage("Fill", bg.transform, color);
        Stretch(fill.rectTransform);
        return fill.rectTransform;
    }

    static void Place(RectTransform r, Vector2 pos, Vector2 size)
    {
        r.anchoredPosition = pos;
        r.sizeDelta = size;
    }

    // Смена: таймер и часы в правом верхнем углу экрана.
    static void BuildShift()
    {
        var go = new GameObject(ShiftName, typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler));
        go.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = go.GetComponent<UnityEngine.UI.CanvasScaler>();
        scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        var clock = NewText("Clock", go.transform, 56);
        clock.alignment = TextAnchor.UpperRight;
        var rt = clock.rectTransform;
        rt.anchorMin = rt.anchorMax = rt.pivot = Vector2.one;
        rt.anchoredPosition = new Vector2(-40, -30);
        rt.sizeDelta = new Vector2(400, 80);

        go.AddComponent<ShiftTimer>().clock = clock;
    }

    static readonly Vector3 Eye = new Vector3(0, 1.6f, 0);

    // Мировой канвас на расстоянии 1.4 м от глаз, повёрнутый лицом к игроку.
    // pitch — вниз от горизонта, yaw — вправо от направления «вперёд» (в градусах).
    static Canvas NewCanvas(string name, Transform parent, Camera cam, Vector2 pixels, float pitch, float yaw)
    {
        var dir = Quaternion.Euler(pitch, yaw, 0) * Vector3.forward;
        return NewCanvasAt(name, parent, cam, pixels, Eye + dir * 1.4f);
    }

    // Мировой канвас в точке position, повёрнутый лицом к глазам игрока (lookFrom, по умолчанию — глаза в офисе).
    // scale — метров на пиксель: 0.001 значит 1000 px = 1 м.
    static Canvas NewCanvasAt(string name, Transform parent, Camera cam, Vector2 pixels, Vector3 position,
        float scale = 0.001f, Vector3? lookFrom = null)
    {
        var go = new GameObject(name, typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
        go.transform.SetParent(parent, false);
        go.transform.position = position;
        go.transform.rotation = Quaternion.LookRotation(position - (lookFrom ?? Eye));
        go.transform.localScale = Vector3.one * scale;

        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = cam;
        go.GetComponent<RectTransform>().sizeDelta = pixels;
        return canvas;
    }

    internal static UnityEngine.UI.Text NewText(string name, Transform parent, int fontSize)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(UnityEngine.UI.Text));
        go.transform.SetParent(parent, false);
        var t = go.GetComponent<UnityEngine.UI.Text>();
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.fontSize = fontSize;
        t.alignment = TextAnchor.MiddleCenter;
        t.color = Color.white;
        t.raycastTarget = false;
        t.verticalOverflow = VerticalWrapMode.Overflow; // иначе строка, не влезшая по высоте, молча пропадает
        return t;
    }

    internal static UnityEngine.UI.Image NewImage(string name, Transform parent, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(UnityEngine.UI.Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<UnityEngine.UI.Image>();
        img.color = color;
        return img;
    }

    internal static void Stretch(RectTransform r)
    {
        r.anchorMin = Vector2.zero;
        r.anchorMax = Vector2.one;
        r.offsetMin = r.offsetMax = Vector2.zero;
    }

    // Стена вдоль оси Z на координате x, 8 м длиной и 3 м высотой, с дверным проёмом 1.2 × 2.2 м посередине.
    // Три куска: по бокам от проёма и перемычка над ним.
    static void BuildWallWithDoorway(Transform parent, string name, float x)
    {
        const float length = 8f, height = 3f, thick = 0.2f, doorWidth = 1.2f, doorHeight = 2.2f;
        var color = new Color(0.5f, 0.47f, 0.44f);
        var wall = new GameObject(name).transform;
        wall.SetParent(parent, false);

        float side = (length - doorWidth) / 2f;     // длина куска сбоку от проёма
        float sideZ = (doorWidth + side) / 2f;      // его центр
        Make(PrimitiveType.Cube, name + "Back", wall, new Vector3(x, height / 2f, -sideZ), new Vector3(thick, height, side), color);
        Make(PrimitiveType.Cube, name + "Front", wall, new Vector3(x, height / 2f, sideZ), new Vector3(thick, height, side), color);
        Make(PrimitiveType.Cube, name + "Lintel", wall, new Vector3(x, (doorHeight + height) / 2f, 0f),
            new Vector3(thick, height - doorHeight, doorWidth), color);
    }

    static void BuildArm(Transform parent, string name, Vector3 dir, Color color, PrimitiveType? prop)
    {
        // Длина дорожки 10, ширина 3; начинается от края центральной платформы (4 от центра)
        const float length = 10f, width = 3f, start = 4f;
        var along = dir * (start + length / 2f);
        var scale = dir.x != 0 ? new Vector3(length, 0.5f, width) : new Vector3(width, 0.5f, length);

        Make(PrimitiveType.Cube, name + "Path", parent, along + Vector3.down * 0.25f, scale, color * 0.7f);

        // Объект на конце дорожки
        if (prop == null) return;
        var end = dir * (start + length - 1.5f);
        var obj = Make(prop.Value, name + "Prop", parent, end + Vector3.up * 1f, Vector3.one * 1.5f, color);
        obj.transform.rotation = Quaternion.Euler(0, 20, 0);
    }

    static GameObject Make(PrimitiveType type, string name, Transform parent, Vector3 pos, Vector3 scale, Color color)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localScale = scale;

        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = color };
        var dir = "Assets/Materials";
        if (!AssetDatabase.IsValidFolder(dir)) AssetDatabase.CreateFolder("Assets", "Materials");
        var path = $"{dir}/{name}.mat";
        AssetDatabase.CreateAsset(mat, path);
        go.GetComponent<Renderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(path);
        return go;
    }
}
