using System.Collections.Generic;
using System.Linq;
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
    const int Version = 42;
    const string VersionNote = "рука Дали в вентиляции (проход Г), без надписи «Дир голоден», свет над генератором, ночь светлее, модели Стрея, Доли и Дали без имён, рука Доли в вентиляции, скример на смерти, время на мониторе, камеры 1–7, свет, неон (F4)";

    // Ночной свет на карте (SetupLighting): отражения неба слабые, виньетка, Bloom для неона. Фон — OfficeLights.AmbientOn
    // (в игре его меняет OfficeLights: без генератора почти темно)
    const float MapReflections = 0.15f, MapVignette = 0.32f, MapBloom = 0.6f;

    // Модель генератора (выгружает Tools/Blender/export_generator.bat). Нет файла — генератор из примитивов
    const string GeneratorModelFolder = "Assets/Models/Generator/";
    const float GeneratorModelScale = 1.4f; // модель ~0.8 м в длину — чуть крупнее, чтобы читалась
    const float GeneratorModelYaw = 25f;    // доворот от «пульт к игроку», чтобы был виден бок с ручкой стартера
    // Слабый свет над генератором (всегда горит): высота над полом, дальность, яркость
    const float GeneratorLightHeight = 1.2f, GeneratorLightRange = 2.5f, GeneratorLightIntensity = 0.6f;

    // Модели фурри: Assets/Models/<Nova|Stray|Doli|Dali>/<имя>.fbx (выгружают Tools/Blender/export_nova.bat и
    // export_furries.bat). Нет файла — капсула. После импорта модель смотрит вдоль +Z — к игроку её поворачивает
    // FurryAvatar. Имён над фурри нет (пользователь, 2026-11-06)
    const string NovaModelFolder = "Assets/Models/Nova/";
    const float FurryModelHeight = 1.8f; // м, с ушами — как капсула
    static readonly Color FurryNoTexture = new Color(0.82f, 0.8f, 0.78f); // цвет материалов, пока автор не прислал текстуры
    // Стрей у двери выглядывает из-за края проёма рольставня, ближнего к линии взгляда: стоит за стеной сбоку от
    // проёма и наклоняется в него — видно голову и плечо
    const float StrayPeekDepth = 0.55f;   // м за рольставнем (дальше от офиса)
    const float StrayPeekBehind = 0.5f;   // м от края проёма за стену
    const float StrayPeekLean = 25f;      // градусов наклона в проём
    // Дали в проходе Г — рука из вентиляции в генераторной (короб из прохода Г): Blender, точка в открытом конце
    // короба, где рука выходит наружу; рука смотрит из короба (Blender +Y) и вниз на HandDroop
    // Проход Г на камерах не показывают — его видно «физически», по вентиляции (пользователь, 2026-11-06)
    const string MapVentDuct = "modular_airduct_rectangular_01_tripple_01";
    static readonly Vector3 MapHandVent = new Vector3(5.37f, -0.32f, 1.6f);
    const float HandOut = 0.42f;          // м: на сколько кончики пальцев высовываются из короба
    const float HandDroop = 30f;          // градусов вниз
    // Скример на экране смерти (DeathScreamer): головы на своей сцене под картой, своя камера
    const float DeathStageDepth = 60f;    // м под полом офиса
    const float DeathHeadFov = 30f;       // градусов
    const float DeathHeadFill = 0.95f;    // какую долю кадра занимает голова (по большей стороне)
    const float DeathCameraZ = 3f;        // м: камера сцены — на +Z от голов, смотрит на них

    // Офис на карте клуба (Assets/Models/Map/Map.fbx, MapBuilder). Точки «Blender» — координаты
    // из «map 67dfn wip.blend» (x, y — по полу), их переводит MapBuilder.Layout.FromBlender.
    // Остальные — в сцене: игрок в (0, 1.6, 0), x — вправо, z — вперёд.
    const float MapGeneratorSpot = 1.7f;   // м от генератора до глаз у генератора: сразу за дверью генераторной
    const float ShutterLift = 2.5f;        // м, на сколько поднимается шторка рольставня, когда дверь открыта
    const float MapDoorWallX = -1.53f;     // Blender: правая стена офиса (сторона офиса), в ней рольставень
    const float MapDoorControlsY = -1.05f; // Blender: рычаг и табличка двери — на этой стене перед рольставнем
    const float MapFrontWallY = -1.72f;    // Blender: передняя стена офиса, над проходом к дэнс полу
    const float MapDeskRim = 0.64f;        // м, высота переднего бортика стола ресепшена
    // Табличка расписания — на передней стене в простенке слева от прохода (справа — монитор): Blender x от края
    // прохода до левой стены офиса, отступ от краёв, высота центра
    const float MapBoardWallFrom = 2.36f, MapBoardWallTo = 3.63f, MapBoardMargin = 0.04f, MapBoardY = 1.65f;
    static readonly Vector2 BoardPixels = new Vector2(1500, 900);
    const float MonitorPixels = 760f;      // ширина монитора камер на экране монитора карты, px канваса
    static readonly Vector3 MapDanceButton = new Vector3(-0.45f, MapDeskRim, 0.93f); // на бортике стола
    static readonly Vector3 MapSoundButton = new Vector3(-0.15f, MapDeskRim, 0.97f);
    const float MapButtonClick = 0.035f;   // м, ширина области клика у кнопок карты (сами 3 см, стоят вплотную)
    const float MapButtonClickHeight = 0.06f, MapButtonClickDepth = 0.07f;
    const float MapButtonPress = 0.004f;   // м, насколько утапливается шляпка кнопки карты
    const float MapLeverAngle = 35f;       // градусов вниз — рычаг под столом в положении «закрыто»
    const float MapLeverReach = 0.05f;     // м, область клика у рычага выступает к игроку (он под столешницей)
    const float MapLeverClickWidth = 0.07f;
    // Рычаг двери — под краем столешницы справа, у правого подлокотника (край там в 0.50 м от кресла): кончик ручки
    // выступает на 1 см, чтобы его было видно; ручка смотрит на кресло (поворот на 90° от того, как в карте)
    static readonly Vector3 MapLeverTip = new Vector3(0.49f, 0f, 0.05f);
    const float MapLeverYaw = 90f;
    const float MapDoorPanelY = 1.45f;     // м, табличка двери у рольставня на карте — ниже: игрок сидит
    static readonly Vector3 MapPhone = new Vector3(-0.7f, MapDeskRim, 0.76f);
    // Blender: где фурри видно из офиса (второй и следующий встают в ряд, см. NewSpot)
    static readonly Vector2 MapFurriesAtDoor = new Vector2(-2.6f, 0.3f);       // коридор 2 за рольставнем
    static readonly Vector2 MapFurriesOnDanceFloor = new Vector2(1.65f, -5.0f);
    static readonly Vector2 MapFurriesInPassage = new Vector2(7.2f, -1.2f);    // проход Г: голову видно в окошко генераторной
    static readonly Vector2 MapFurriesAtGenerator = new Vector2(6.0f, 1.9f);   // в глубине генераторной
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

        // Карта клуба из Blender (Assets/Models/Map): кресло ресепшена — под игроком. Нет её — старый полигон
        var map = MapBuilder.Build(root.transform);
        if (map == null) BuildPrimitiveOffice(root.transform);
        Eye = new Vector3(0f, map != null ? CameraLook.SeatedEyeHeight : 1.6f, 0f);

        // Камера в центре платформы, поворачивается на три стороны
        var cam = Camera.main;
        if (cam != null)
        {
            cam.transform.position = Eye;
            cam.transform.rotation = Quaternion.identity;
            if (cam.GetComponent<CameraLook>() == null) cam.gameObject.AddComponent<CameraLook>();
            // Чтобы мышью можно было кликать по 3D-объектам (ручка генератора)
            if (cam.GetComponent<UnityEngine.EventSystems.PhysicsRaycaster>() == null)
                cam.gameObject.AddComponent<UnityEngine.EventSystems.PhysicsRaycaster>();
        }

        var generator = BuildGenerator(root.transform, cam, map);
        SetupLighting(map, generator);
        var monitor = BuildMonitor(root.transform, cam, map);
        monitor.generator = generator;

        // Отпугивающие факторы: стол с кнопками дэнс пола и звука, дэнс пол впереди, дверь с рычагом справа
        var (danceFloor, sound) = BuildDesk(root.transform, cam, generator, map);
        BuildDeskFan(map);
        var door = BuildDoor(root.transform, cam, generator, map);

        // Пробел — действие в ту сторону, куда смотришь
        var space = new GameObject("SpaceAction", typeof(SpaceAction)).GetComponent<SpaceAction>();
        space.transform.SetParent(root.transform, false);
        space.look = cam != null ? cam.GetComponent<CameraLook>() : null;
        space.danceFloor = danceFloor;
        space.door = door;

        var oldShift = GameObject.Find(ShiftName);
        if (oldShift != null) Object.DestroyImmediate(oldShift);
        var shift = BuildShift(monitor.clock);

        // Фурри: директор ночи, фигуры в офисе, предупреждения, экран смерти, мог-батл
        var director = BuildFurries(root.transform, cam, generator, door, danceFloor, sound, space, shift, map);
        monitor.director = director;
        var difficulty = BuildDifficultyPanel(root.transform, director);

        // Звонки клиентов: телефон на столе, табличка с расписанием над столом, окно разговора
        difficulty.calls = BuildCalls(root.transform, cam, director, map);

        // Пауза (Esc) и экран «Ночь окончена»
        shift.nightEnd = BuildPauseHUD(root.transform, director, difficulty.calls);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        EditorPrefs.SetInt(VersionKey, Version);
        Debug.Log("[SSDFNC] Test platform built.");
    }

    // Монитор камер: экран (название камеры, кто в комнате, на камере 7 — «ПОКОРМИТЬ») и ряд кнопок выбора.
    // На карте всё это — на экране монитора, что стоит на столе ресепшена, кнопки внизу экрана.
    // Без карты экран и кнопки висят справа внизу в поле зрения «вперёд», при повороте вправо не видны.
    static CameraMonitor BuildMonitor(Transform parent, Camera cam, MapBuilder.Layout map)
    {
        var oldEs = Object.FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>();
        if (oldEs != null) Object.DestroyImmediate(oldEs.gameObject);
        new GameObject("EventSystem",
            typeof(UnityEngine.EventSystems.EventSystem),
            typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));

        bool onDesk = map != null && map.hasScreen;
        Canvas screenCanvas;
        if (onDesk)
        {
            // Канвас во весь экран монитора: ширина MonitorPixels, высота — по пропорциям экрана
            var pixels = new Vector2(MonitorPixels, MonitorPixels * map.screenHeight / map.screenWidth);
            var pos = map.screenCenter + map.screenNormal * 0.004f; // чуть перед стеклом
            screenCanvas = NewCanvasAt("MonitorPanel", parent, cam, pixels, pos, scale: map.screenWidth / MonitorPixels);
            screenCanvas.transform.rotation = Quaternion.LookRotation(-map.screenNormal, map.screenUp);
        }
        else screenCanvas = NewCanvas("MonitorPanel", parent, cam, new Vector2(640, 380), pitch: 12f, yaw: 28f);

        var screen = NewImage("Screen", screenCanvas.transform, new Color(0.04f, 0.1f, 0.06f));
        Stretch(screen.rectTransform);
        // Кадр камеры — над фоном экрана, под надписями и кнопками (CameraFeeds); видео играет VideoPlayer монитора
        var feedImage = new GameObject("Feed", typeof(RectTransform), typeof(UnityEngine.UI.RawImage))
            .GetComponent<UnityEngine.UI.RawImage>();
        feedImage.transform.SetParent(screenCanvas.transform, false);
        Stretch(feedImage.rectTransform);
        feedImage.raycastTarget = false;
        feedImage.enabled = false;
        var player = screenCanvas.gameObject.AddComponent<UnityEngine.Video.VideoPlayer>();
        player.playOnAwake = false;
        player.source = UnityEngine.Video.VideoSource.Url;
        player.renderMode = UnityEngine.Video.VideoRenderMode.RenderTexture;
        player.audioOutputMode = UnityEngine.Video.VideoAudioOutputMode.None;
        player.isLooping = true;
        player.timeUpdateMode = UnityEngine.Video.VideoTimeUpdateMode.GameTime; // на паузе стоит
        player.skipOnDrop = true;
        // Помехи поверх кадра (MonitorStatic): строки развёртки, «снег», ползущая полоса, полосы сбоя, тёмные края
        var staticGo = new GameObject("Static", typeof(RectTransform));
        staticGo.transform.SetParent(screenCanvas.transform, false);
        Stretch((RectTransform)staticGo.transform);
        var noise = staticGo.AddComponent<MonitorStatic>();
        UnityEngine.UI.RawImage Layer(string name)
        {
            var layer = new GameObject(name, typeof(RectTransform), typeof(UnityEngine.UI.RawImage)).GetComponent<UnityEngine.UI.RawImage>();
            layer.transform.SetParent(staticGo.transform, false);
            Stretch(layer.rectTransform);
            layer.raycastTarget = false;
            return layer;
        }
        noise.scanlines = Layer("Scanlines");
        noise.noise = Layer("Snow");
        noise.bar = Layer("RollBar");
        noise.lines = new UnityEngine.UI.Image[6];
        for (int i = 0; i < noise.lines.Length; i++)
        {
            noise.lines[i] = NewImage("GlitchLine" + i, staticGo.transform, Color.white);
            Stretch(noise.lines[i].rectTransform);
            noise.lines[i].raycastTarget = false;
        }
        noise.vignette = Layer("Vignette");
        noise.feed = feedImage;
        var title = NewText("Title", screenCanvas.transform, onDesk ? 36 : 40);
        title.alignment = TextAnchor.UpperLeft;
        Place(title.rectTransform, onDesk ? new Vector2(0, 172) : new Vector2(0, 150),
            onDesk ? new Vector2(720, 50) : new Vector2(600, 60));
        var body = NewText("Body", screenCanvas.transform, onDesk ? 30 : 38);
        body.alignment = TextAnchor.UpperLeft;
        body.color = new Color(0.7f, 1f, 0.75f);
        Place(body.rectTransform, onDesk ? new Vector2(0, 62) : new Vector2(0, 10),
            onDesk ? new Vector2(720, 160) : new Vector2(600, 220));

        var feedGo = new GameObject("FeedButton", typeof(RectTransform), typeof(UnityEngine.UI.Image),
            typeof(UnityEngine.UI.Button), typeof(HoldButton));
        feedGo.transform.SetParent(screenCanvas.transform, false);
        Place((RectTransform)feedGo.transform, onDesk ? new Vector2(0, -72) : new Vector2(0, -145),
            onDesk ? new Vector2(420, 58) : new Vector2(440, 70));
        var feedBtn = feedGo.GetComponent<UnityEngine.UI.Button>();
        var feedColors = feedBtn.colors;
        feedColors.normalColor = new Color(0.15f, 0.45f, 0.2f);
        feedColors.highlightedColor = new Color(0.25f, 0.6f, 0.3f);
        feedColors.pressedColor = new Color(0.4f, 0.8f, 0.45f);
        feedColors.selectedColor = feedColors.normalColor;
        feedBtn.colors = feedColors;
        var feedLabel = NewText("Label", feedGo.transform, onDesk ? 28 : 34);
        feedLabel.text = "ПОКОРМИТЬ (держать)";
        Stretch(feedLabel.rectTransform);

        // Время смены — в правом нижнем углу экрана, правее ряда кнопок (пишет ShiftTimer)
        var clock = NewText("Clock", screenCanvas.transform, onDesk ? 32 : 36);
        clock.alignment = TextAnchor.LowerRight;
        clock.color = new Color(0.7f, 1f, 0.75f);
        var cr = clock.rectTransform;
        cr.anchorMin = cr.anchorMax = cr.pivot = new Vector2(1f, 0f);
        Place(cr, onDesk ? new Vector2(-14, 8) : new Vector2(-16, 10), new Vector2(130, 50));
        var clockShadow = clock.gameObject.AddComponent<UnityEngine.UI.Shadow>(); // читается и поверх кадра
        clockShadow.effectColor = new Color(0f, 0f, 0f, 0.9f);
        clockShadow.effectDistance = new Vector2(2f, -2f);

        // Кнопки камер: комнаты 2–8 и проход Г. На карте — ряд внизу экрана монитора
        var buttonParent = onDesk ? screenCanvas.transform
            : NewCanvas("CameraButtons", parent, cam, new Vector2(500, 80), pitch: 24f, yaw: 28f).transform;
        int count = Rooms.Cameras.Length;
        var buttons = new UnityEngine.UI.Button[count];
        float size = onDesk ? 56 : 54, gap = onDesk ? 10 : 8, rowY = onDesk ? -165 : 0;
        for (int i = 0; i < count; i++)
        {
            string camLabel = Rooms.CameraLabel(Rooms.Cameras[i]);
            var go = new GameObject($"CamButton{camLabel}", typeof(RectTransform), typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.Button));
            go.transform.SetParent(buttonParent, false);
            Place((RectTransform)go.transform, new Vector2((i - (count - 1) / 2f) * (size + gap), rowY), new Vector2(size, size));
            var btn = go.GetComponent<UnityEngine.UI.Button>();
            btn.transition = UnityEngine.UI.Selectable.Transition.None;
            buttons[i] = btn;

            var num = NewText("Number", go.transform, onDesk ? 32 : 34);
            num.text = camLabel;
            Stretch(num.rectTransform);
        }

        var monitor = screenCanvas.gameObject.AddComponent<CameraMonitor>();
        monitor.feed = feedImage;
        monitor.video = player;
        monitor.noise = noise;
        var shadow = title.gameObject.AddComponent<UnityEngine.UI.Shadow>(); // название читается и поверх кадра
        shadow.effectColor = new Color(0f, 0f, 0f, 0.9f);
        shadow.effectDistance = new Vector2(2f, -2f);
        monitor.screen = screen;
        monitor.title = title;
        monitor.body = body;
        monitor.buttons = buttons;
        monitor.feedButton = feedGo.GetComponent<HoldButton>();
        monitor.feedLabel = feedLabel;
        monitor.clock = clock;
        return monitor;
    }

    // Генератор за дверным проёмом слева, в 6.5 м от центра офиса: корпус, ручка стартера, табло
    // и точка GeneratorSpot, куда встаёт камера, когда игрок подходит (A, когда уже смотришь влево).
    // Точка самого генератора — центр основания корпуса, от неё считается «достаёт ли игрок».
    // На карте — там, где генератор стоял в Blender (в генераторной за дверью слева).
    static Generator BuildGenerator(Transform parent, Camera cam, MapBuilder.Layout map)
    {
        var go = new GameObject("Generator", typeof(Generator), typeof(GeneratorSound));
        go.transform.SetParent(parent, false);
        go.transform.localPosition = map != null ? map.generator : new Vector3(-6.5f, 0f, 0f);
        var generator = go.GetComponent<Generator>();
        // Слабый тёплый свет над генератором — его видно и в темноте, и без питания (не гаснет с лампами OfficeLights)
        var genLight = new GameObject("GeneratorLight", typeof(Light)).GetComponent<Light>();
        genLight.transform.SetParent(go.transform, false);
        genLight.transform.localPosition = new Vector3(0f, GeneratorLightHeight, 0f);
        genLight.type = LightType.Point;
        genLight.range = GeneratorLightRange;
        genLight.intensity = GeneratorLightIntensity;
        genLight.color = new Color(1f, 0.85f, 0.65f);
        genLight.shadows = LightShadows.None;

        // Камера у генератора: в 1.5 м от передней стенки, смотрит на него и на 20° вниз
        var spot = new GameObject("GeneratorSpot").transform;
        spot.SetParent(go.transform, false);
        spot.localPosition = new Vector3(map != null ? MapGeneratorSpot : 1.85f, 1.6f, 0f);
        spot.localRotation = Quaternion.Euler(20f, -90f, 0f);
        if (cam != null) cam.GetComponent<CameraLook>().generatorSpot = spot;

        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(GeneratorModelFolder + "Generator.fbx");
        var model = asset != null ? BuildGeneratorModel(go.transform, generator, asset, map) : null;
        if (model == null) BuildGeneratorPrimitives(go.transform, generator);

        var panel = BuildGeneratorPanel(go.transform, cam, generator, spot.position);
        if (model != null) model.panel = panel;
        return generator;
    }

    // Модель генератора: материалы URP из её текстур, пульт к игроку, анимации Legacy (GeneratorModel),
    // на ручку стартера — коллайдер побольше и GeneratorHandle.
    // На карте — как у автора (пользователь, 2026-11-05: «поверни как на карте стоит, чтоб так же прям»): пульт смотрит
    // туда же, куда у генератора в .blend, и размер тот же (это одна и та же модель EN2500).
    // Модель лежит внутри пустого объекта GeneratorModel: место, поворот и масштаб — у него, потому что
    // анимации двигают корень самой модели (это риг generator_rig, Unity склеивает его с корнем FBX)
    static GeneratorModel BuildGeneratorModel(Transform parent, Generator generator, GameObject asset, MapBuilder.Layout map)
    {
        bool asMap = map != null && map.generatorFront != Vector3.zero;
        var holder = new GameObject("GeneratorModel").transform;
        holder.SetParent(parent, false);
        holder.localScale = Vector3.one * (asMap ? 1f : GeneratorModelScale);
        var go = (GameObject)PrefabUtility.InstantiatePrefab(asset, holder);

        var materials = GeneratorMaterials();
        foreach (var r in go.GetComponentsInChildren<Renderer>())
        {
            var list = r.sharedMaterials;
            for (int i = 0; i < list.Length; i++)
                if (list[i] != null && materials.TryGetValue(list[i].name, out var m)) list[i] = m;
            r.sharedMaterials = list;
        }

        // Пульт (у него стрелка прибора) — к игроку, на +X; потом доворот, чтобы был виден бок с ручкой
        var dial = go.GetComponentsInChildren<Transform>().First(t => t.name == "portable_generator_dial");
        var handleMesh = go.GetComponentsInChildren<MeshFilter>().First(m => m.name == "starter_handle");
        var center = ModelBounds(go).center;
        var front = dial.position - center;
        if (asMap)
        {
            // Как у автора: пульт — туда же, размер по длинной стороне — как у генератора карты
            holder.Rotate(0f, Vector3.SignedAngle(new Vector3(front.x, 0f, front.z), map.generatorFront, Vector3.up), 0f, Space.World);
            var size = ModelBounds(go).size;
            holder.localScale *= Mathf.Max(map.generatorSize.x, map.generatorSize.z) / Mathf.Max(size.x, size.z);
        }
        else
        {
            holder.Rotate(0f, Vector3.SignedAngle(new Vector3(front.x, 0f, front.z), Vector3.right, Vector3.up), 0f, Space.World);
            var side = handleMesh.transform.position - ModelBounds(go).center;
            holder.Rotate(0f, side.z > 0f ? GeneratorModelYaw : -GeneratorModelYaw, 0f, Space.World);
        }

        // Встать на пол серединой на точку генератора
        var b = ModelBounds(go);
        holder.position += parent.position - new Vector3(b.center.x, b.min.y, b.center.z);

        // Legacy-импорт сам кладёт на модель Animation — добавляем в него свои клипы
        var anim = go.GetComponent<Animation>();
        if (anim == null) anim = go.AddComponent<Animation>();
        anim.playAutomatically = false;
        anim.AddClip(ModelClip("Generator.fbx"), GeneratorModel.StartClip);
        anim.AddClip(ModelClip("Generator@Idle.fbx"), GeneratorModel.IdleClip);
        anim.AddClip(ModelClip("Generator@CapOpen.fbx"), GeneratorModel.CapOpenClip);
        anim.AddClip(ModelClip("Generator@CapClose.fbx"), GeneratorModel.CapCloseClip);
        var model = holder.gameObject.AddComponent<GeneratorModel>();
        model.generator = generator;
        model.anim = anim;

        // Ручка маленькая (7 см) — коллайдер в 2.5 раза больше, чтобы попадать мышкой
        var bounds = handleMesh.sharedMesh.bounds;
        var collider = handleMesh.gameObject.AddComponent<BoxCollider>();
        collider.center = bounds.center;
        collider.size = Vector3.Max(bounds.size * 2.5f, Vector3.one * 0.08f);
        var handle = handleMesh.gameObject.AddComponent<GeneratorHandle>();
        handle.generator = generator;
        handle.model = model;
        return model;
    }

    static Bounds ModelBounds(GameObject go)
    {
        var renderers = go.GetComponentsInChildren<Renderer>();
        var b = renderers[0].bounds;
        foreach (var r in renderers) b.Encapsulate(r.bounds);
        return b;
    }

    static AnimationClip ModelClip(string file) =>
        AssetDatabase.LoadAllAssetsAtPath(GeneratorModelFolder + file).OfType<AnimationClip>()
            .First(c => !c.name.StartsWith("__preview__"));

    // Материалы URP Lit из текстур модели: создаются при первой сборке, потом только обновляются.
    // Ключ — имя материала в FBX (его задаёт export_generator.py)
    static Dictionary<string, Material> GeneratorMaterials()
    {
        Texture2D Tex(string name) => AssetDatabase.LoadAssetAtPath<Texture2D>(GeneratorModelFolder + "Textures/" + name);
        var baseMap = Tex("Generator_BaseMap.jpg");
        var glassMap = Tex("Generator_GlassBase.png");
        var metallic = Tex("Generator_MetallicSmoothness.png");
        var normal = Tex("Generator_Normal.png");
        return new Dictionary<string, Material>
        {
            ["Generator"] = ModelMaterial(GeneratorModelFolder, "Generator", m => Lit(m, baseMap, metallic, normal)),
            ["GeneratorGlass"] = ModelMaterial(GeneratorModelFolder, "GeneratorGlass", m =>
            {
                Lit(m, glassMap, metallic, normal);
                Transparent(m);
            }),
            ["StarterRope"] = ModelMaterial(GeneratorModelFolder, "StarterRope", m =>
            {
                m.SetColor("_BaseColor", new Color(0.19f, 0.19f, 0.19f));
                m.SetFloat("_Smoothness", 0.15f);
            }),
        };
    }

    // Материал модели в <папка модели>/Materials: создаётся при первой сборке, потом только обновляется
    internal static Material ModelMaterial(string modelFolder, string name, System.Action<Material> setup,
        string shader = "Universal Render Pipeline/Lit")
    {
        string folder = modelFolder + "Materials";
        if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder(modelFolder.TrimEnd('/'), "Materials");
        string path = $"{folder}/{name}.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(Shader.Find(shader));
            AssetDatabase.CreateAsset(m, path);
        }
        setup(m);
        EditorUtility.SetDirty(m);
        return m;
    }

    static void Lit(Material m, Texture2D baseMap, Texture2D metallic, Texture2D normal)
    {
        m.SetTexture("_BaseMap", baseMap);
        m.SetColor("_BaseColor", Color.white);
        m.SetTexture("_MetallicGlossMap", metallic); // металл в RGB, гладкость в альфе
        m.EnableKeyword("_METALLICSPECGLOSSMAP");
        m.SetFloat("_Smoothness", 1f);
        m.SetFloat("_SmoothnessTextureChannel", 0f);
        m.SetTexture("_BumpMap", normal);
        m.EnableKeyword("_NORMALMAP");
    }

    internal static void Transparent(Material m)
    {
        m.SetFloat("_Surface", 1f); // Transparent, смешивание Alpha
        m.SetFloat("_Blend", 0f);
        m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        m.SetFloat("_SrcBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One);
        m.SetFloat("_DstBlendAlpha", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        m.SetFloat("_ZWrite", 0f);
        m.SetOverrideTag("RenderType", "Transparent");
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        m.SetShaderPassEnabled("DepthOnly", false);
    }

    // Запасной генератор из примитивов (если модель не выгружена): оранжевый корпус и ручка-цилиндр
    static void BuildGeneratorPrimitives(Transform parent, Generator generator)
    {
        Make(PrimitiveType.Cube, "GeneratorBody", parent,
            new Vector3(0f, 0.45f, 0f), new Vector3(0.7f, 0.9f, 1.2f), new Color(0.75f, 0.45f, 0.1f));

        // Ручка на передней стенке, справа от игрока. Её ось Z смотрит на игрока — туда она и тянется.
        var handle = new GameObject("StarterHandle", typeof(GeneratorHandle));
        handle.transform.SetParent(parent, false);
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
    }

    // Табло 1000×520 px (1.3 × 0.68 м) на корпусе генератора, лицом к глазам игрока у генератора
    static GeneratorPanel BuildGeneratorPanel(Transform parent, Camera cam, Generator generator, Vector3 eye)
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
        return panel;
    }

    // Стол перед игроком (под монитором), на нём кнопки дэнс пола и звука с подписями
    // На карте стол — ресепшен из Blender, кнопки на его переднем бортике, дэнс пол — плитки карты.
    // Ночной свет (с картой): солнца нет, фон слабый (OfficeLights.AmbientOn; без генератора OfficeLights гасит и его),
    // отражения неба слабые; виньетка темнее, Bloom — чтобы светился неон. Без карты — как было (солнце для полигона)
    static void SetupLighting(MapBuilder.Layout map, Generator generator)
    {
        bool night = map != null;
        foreach (var l in Object.FindObjectsByType<Light>(FindObjectsInactive.Include))
            if (l.type == LightType.Directional && l.transform.root.name != "TestPlatform") l.gameObject.SetActive(!night);
        RenderSettings.ambientMode = night ? UnityEngine.Rendering.AmbientMode.Flat : UnityEngine.Rendering.AmbientMode.Skybox;
        RenderSettings.ambientLight = OfficeLights.AmbientOn;
        RenderSettings.reflectionIntensity = night ? MapReflections : 1f;
        if (map?.lights != null) map.lights.generator = generator;

        var volume = Object.FindAnyObjectByType<UnityEngine.Rendering.Volume>();
        var profile = volume != null ? volume.sharedProfile : null;
        if (profile == null) return;
        if (profile.TryGet(out UnityEngine.Rendering.Universal.Vignette vignette))
            vignette.intensity.Override(night ? MapVignette : 0.2f);
        if (profile.TryGet(out UnityEngine.Rendering.Universal.Bloom bloom))
            bloom.intensity.Override(night ? MapBloom : 0.25f);
        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssets();
    }

    // Вентилятор на столе: лопасти из карты («Inner Fan», в Blender — на кости spin) крутятся вокруг своей оси (DeskFan).
    // Точка лопастей в выгрузке — не в центре втулки, поэтому ось считаем по форме: лопасти — плоский диск, ось —
    // направление, вдоль которого он тоньше всего (смотрит от корпуса «SM_Fan.002» — туда дует), центр — среднее вершин
    // (сверено с костью spin в Blender: 0.05 см от оси; середина между краями у трёх лопастей сбита на 1 см)
    static void BuildDeskFan(MapBuilder.Layout map)
    {
        var blades = map?.Find("Inner Fan");
        var filter = blades != null ? blades.GetComponent<MeshFilter>() : null;
        if (filter == null || filter.sharedMesh == null) return;
        var verts = filter.sharedMesh.vertices;
        var world = new Vector3[verts.Length];
        var center = Vector3.zero;
        for (int i = 0; i < verts.Length; i++)
        {
            world[i] = blades.TransformPoint(verts[i]);
            center += world[i];
        }
        center /= verts.Length;
        // Ковариация вершин; самое «тонкое» направление — наибольшее у (след·E − C), степенной метод
        float xx = 0, yy = 0, zz = 0, xy = 0, xz = 0, yz = 0;
        foreach (var w in world)
        {
            var d = w - center;
            xx += d.x * d.x; yy += d.y * d.y; zz += d.z * d.z;
            xy += d.x * d.y; xz += d.x * d.z; yz += d.y * d.z;
        }
        float tr = xx + yy + zz;
        var axis = new Vector3(1f, 0.7f, 0.3f).normalized;
        for (int k = 0; k < 64; k++)
            axis = new Vector3(
                (tr - xx) * axis.x - xy * axis.y - xz * axis.z,
                -xy * axis.x + (tr - yy) * axis.y - yz * axis.z,
                -xz * axis.x - yz * axis.y + (tr - zz) * axis.z).normalized;
        var body = map.Find("SM_Fan.002");
        if (body != null && Vector3.Dot(axis, center - MapBuilder.Layout.BoundsOf(body).center) < 0f) axis = -axis;

        var pivot = new GameObject("DeskFanSpin", typeof(DeskFan)).transform;
        pivot.SetParent(blades.parent, false);
        pivot.SetPositionAndRotation(center, Quaternion.LookRotation(axis));
        blades.SetParent(pivot, true);
        blades.gameObject.isStatic = false;
        Debug.Log($"[SSDFNC] Вентилятор: ось {axis}, центр {center}");
    }

    static (DanceFloor, SoundSignal) BuildDesk(Transform parent, Camera cam, Generator generator, MapBuilder.Layout map)
    {
        if (map == null)
            Make(PrimitiveType.Cube, "Desk", parent, new Vector3(0.1f, 0.45f, 1.3f), new Vector3(1.9f, 0.9f, 1.0f),
                new Color(0.35f, 0.25f, 0.18f));

        // Дэнс пол: плитки на полу коридора впереди (без карты — 3×3, z от 5 до 8) и свет над ними
        var floorGo = new GameObject("DanceFloor", typeof(DanceFloor));
        floorGo.transform.SetParent(parent, false);
        var floor = floorGo.GetComponent<DanceFloor>();
        floor.generator = generator;
        var glowPos = new Vector3(0f, 2.5f, 6.5f);
        if (map != null && map.danceTiles.Length > 0)
        {
            floor.tiles = map.danceTiles;
            glowPos = new Vector3(map.danceFloor.center.x, 2.4f, map.danceFloor.center.z); // под потолком (3 м)
        }
        else
        {
            floor.tiles = new Renderer[9];
            for (int i = 0; i < 9; i++)
            {
                int x = i % 3 - 1, z = i / 3 - 1;
                floor.tiles[i] = Make(PrimitiveType.Cube, $"DanceTile{i + 1}", floorGo.transform,
                    new Vector3(x, 0.01f, 6.5f + z), new Vector3(0.95f, 0.02f, 0.95f), new Color(0.1f, 0.1f, 0.12f))
                    .GetComponent<Renderer>();
            }
        }
        var glow = new GameObject("DanceLight", typeof(Light)).GetComponent<Light>();
        glow.transform.SetParent(floorGo.transform, false);
        glow.transform.localPosition = glowPos;
        glow.type = LightType.Point;
        glow.range = 7f;
        glow.intensity = 4f;
        glow.enabled = false;
        floor.glow = glow;
        // Кнопки: на карте — розовая и красная из Blender (на внутренней стороне стола), иначе свои на бортике
        bool mapButtons = map != null && map.Find("DanceButton") != null && map.Find("DanceButtonCap") != null
                          && map.Find("SoundButton") != null && map.Find("SoundButtonCap") != null;
        if (mapButtons) MapButton(parent, "DanceButton", map.Find("DanceButton"), map.Find("DanceButtonCap"), floor);
        else BuildDeskButton(parent, "DanceButton", map != null ? MapDanceButton : new Vector3(-0.45f, 0.9f, 1.45f),
            new Color(0.9f, 0.2f, 0.8f), floor);

        // Кнопка звука
        var soundGo = new GameObject("SoundSignal", typeof(SoundSignal), typeof(AudioSource));
        soundGo.transform.SetParent(parent, false);
        var signal = soundGo.GetComponent<SoundSignal>();
        signal.generator = generator;
        signal.source = soundGo.GetComponent<AudioSource>();
        signal.source.playOnAwake = false;
        signal.lamp = mapButtons
            ? MapButton(parent, "SoundButton", map.Find("SoundButton"), map.Find("SoundButtonCap"), signal)
            : BuildDeskButton(parent, "SoundButton", map != null ? MapSoundButton : new Vector3(-0.15f, 0.9f, 1.45f),
                new Color(0.85f, 0.1f, 0.1f), signal);
        // Подписей у кнопок, рычага и телефона нет (пользователь, 2026-11-06)
        return (floor, signal);
    }

    // Фурри: директор ночи (NightDirector), места, где фурри видно из офиса, фигуры-заглушки,
    // экран с предупреждением, экран смерти и окно мог-батла
    static NightDirector BuildFurries(Transform parent, Camera cam, Generator generator, Door door, DanceFloor danceFloor,
        SoundSignal sound, SpaceAction space, ShiftTimer shift, MapBuilder.Layout map)
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
        // Звук сбоя монитора: положи файл в Assets/Audio/MonitorReboot.wav (.ogg, .mp3) и пересобери офис
        director.monitorRebootClip = new[] { "wav", "ogg", "mp3" }
            .Select(ext => AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/MonitorReboot." + ext))
            .FirstOrDefault(c => c != null);

        // Где фурри видно из офиса. Второй и следующий встают вдоль оси right места
        if (map != null)
        {
            Vector3 P(Vector2 b) => map.FromBlender(b.x, b.y, 0f);
            director.atDoor = NewSpot("FurriesAtDoor", go.transform, P(MapFurriesAtDoor), 90f);       // ряд — вдоль рольставня
            director.onDanceFloor = NewSpot("FurriesOnDanceFloor", go.transform, P(MapFurriesOnDanceFloor), 0f);
            director.inPassage = NewSpot("FurriesInPassage", go.transform, P(MapFurriesInPassage), -90f); // ряд — вглубь прохода
            director.atGenerator = NewSpot("FurriesAtGenerator", go.transform, P(MapFurriesAtGenerator), 180f);
        }
        else
        {
            director.atDoor = NewSpot("FurriesAtDoor", go.transform, new Vector3(4.4f, 0f, 0f), 0f);          // за правым проёмом
            director.onDanceFloor = NewSpot("FurriesOnDanceFloor", go.transform, new Vector3(0f, 0f, 6.5f), 0f);
            director.inPassage = NewSpot("FurriesInPassage", go.transform, new Vector3(-10.5f, 0f, 0f), 180f); // за генератором
            director.atGenerator = NewSpot("FurriesAtGenerator", go.transform, new Vector3(-6.5f, 0f, -1f), 180f);
        }

        // Фурри: модели из Blender (нет модели — капсула), по FurryId (у Дира нет — его видно только на камере).
        // Стрей у двери выглядывает из-за угла, Дали в проходе Г — рукой из вентиляции
        director.avatars = new FurryAvatar[5];
        director.avatars[(int)FurryId.Stray] = ModelAvatar(go.transform, "Stray", "Стрей", new Color(0.95f, 0.8f, 0.2f));
        director.avatars[(int)FurryId.Nova] = ModelAvatar(go.transform, "Nova", "Нова", new Color(0.6f, 0.3f, 0.9f));
        director.avatars[(int)FurryId.Dali] = ModelAvatar(go.transform, "Dali", "Дали", new Color(0.2f, 0.8f, 0.85f));
        director.avatars[(int)FurryId.Doli] = ModelAvatar(go.transform, "Doli", "Доли", new Color(1f, 0.45f, 0.7f));
        director.strayPeek = StrayPeek(go.transform, map);
        director.daliHand = DaliHand(go.transform, map);

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



        // Окно мог-батла: кружок, кнопка, стрелка, полоска, запас мога и фаза (то же окно — в проверке скилл-чеков)
        var battle = MogBattleBuilder.Build(hud);
        director.battle = battle;

        return director;
    }

    // Оверлей поверх офиса: меню паузы (Esc) и экран «Ночь окончена». Панели на весь экран ловят клики,
    // чтобы они не проходили в сцену. Кнопки — как в главном меню (MainMenuBuilder.NewButton)
    static NightEndScreen BuildPauseHUD(Transform parent, NightDirector director, CallDirector calls)
    {
        var go = new GameObject("PauseHUD", typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler),
            typeof(UnityEngine.UI.GraphicRaycaster), typeof(PauseMenu), typeof(NightEndScreen));
        go.transform.SetParent(parent, false);
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 40;
        var scaler = go.GetComponent<UnityEngine.UI.CanvasScaler>();
        scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        // Смерть: голова фурри (скример) поверх немного затемнённой игры или чёрный экран «СКОНЧАЛСЯ» (Дир), кнопки
        // (их показывает NightDirector через секунду). Цвет фона ставит NightDirector
        var death = NewImage("DeathPanel", go.transform, new Color(0f, 0f, 0f, 0.55f));
        Stretch(death.rectTransform);
        director.deathPanel = death.gameObject;
        director.deathBackground = death;
        var head = new GameObject("Screamer", typeof(RectTransform), typeof(UnityEngine.UI.RawImage)).GetComponent<UnityEngine.UI.RawImage>();
        head.transform.SetParent(death.transform, false);
        Place(head.rectTransform, Vector2.zero, new Vector2(1080, 1080));
        head.raycastTarget = false;
        director.screamer = BuildDeathHeads(parent, head);
        director.deathText = NewText("DeathText", death.transform, 110);
        Place(director.deathText.rectTransform, new Vector2(0, 90), new Vector2(1700, 420));
        var deathButtons = new GameObject("Buttons", typeof(RectTransform));
        deathButtons.transform.SetParent(death.transform, false);
        Stretch((RectTransform)deathButtons.transform);
        director.deathButtons = deathButtons;
        director.deathRestartButton = MainMenuBuilder.NewButton("Restart", deathButtons.transform, "Начать заново",
            new Vector2(-230, -290), new Vector2(420, 100), 44);
        director.deathMenuButton = MainMenuBuilder.NewButton("Menu", deathButtons.transform, "В меню",
            new Vector2(230, -290), new Vector2(420, 100), 44);

        // Пауза: затемнение и карточка с кнопками
        var pause = go.GetComponent<PauseMenu>();
        pause.director = director;
        var dim = NewImage("PausePanel", go.transform, new Color(0f, 0f, 0f, 0.72f));
        Stretch(dim.rectTransform);
        pause.panel = dim.gameObject;
        var card = NewImage("Card", dim.transform, new Color(0.1f, 0.07f, 0.15f, 0.97f));
        Place(card.rectTransform, Vector2.zero, new Vector2(640, 750));
        pause.card = card.gameObject;
        var title = NewText("Title", card.transform, 80);
        title.text = "ПАУЗА";
        title.color = new Color(0.8f, 0.6f, 1f);
        Place(title.rectTransform, new Vector2(0, 285), new Vector2(600, 110));
        pause.resumeButton = MainMenuBuilder.NewButton("Resume", card.transform, "Продолжить", new Vector2(0, 160), new Vector2(500, 86), 40);
        pause.restartButton = MainMenuBuilder.NewButton("Restart", card.transform, "Начать ночь заново", new Vector2(0, 56), new Vector2(500, 86), 40);
        pause.soundsButton = MainMenuBuilder.NewButton("Sounds", card.transform, "Звуки", new Vector2(0, -48), new Vector2(500, 86), 40);
        pause.menuButton = MainMenuBuilder.NewButton("Menu", card.transform, "В главное меню", new Vector2(0, -152), new Vector2(500, 86), 40);
        pause.quitButton = MainMenuBuilder.NewButton("Quit", card.transform, "Выйти из игры", new Vector2(0, -256), new Vector2(500, 86), 40);
        var hint = NewText("Hint", card.transform, 28);
        hint.text = "Esc — продолжить";
        hint.color = new Color(0.6f, 0.55f, 0.7f);
        Place(hint.rectTransform, new Vector2(0, -335), new Vector2(600, 40));
        // «Звуки»: окно строит себя само при первом открытии (SoundSettingsPanel)
        var soundsGo = new GameObject("SoundsPanel", typeof(RectTransform), typeof(SoundSettingsPanel));
        soundsGo.transform.SetParent(dim.transform, false);
        Stretch((RectTransform)soundsGo.transform);
        pause.sounds = soundsGo.GetComponent<SoundSettingsPanel>();

        // Конец ночи: экран на весь кадр, итоги и две кнопки
        var end = go.GetComponent<NightEndScreen>();
        end.director = director;
        end.calls = calls;
        var screen = NewImage("NightEndPanel", go.transform, new Color(0.06f, 0.04f, 0.12f, 1f));
        Stretch(screen.rectTransform);
        end.panel = screen.gameObject;
        end.group = screen.gameObject.AddComponent<CanvasGroup>();
        var endTitle = NewText("Title", screen.transform, 110);
        endTitle.text = "НОЧЬ ОКОНЧЕНА"; // «НОЧЬ N ПРОЙДЕНА» ставит NightEndScreen
        end.title = endTitle;
        endTitle.color = new Color(1f, 0.77f, 0.08f);
        Place(endTitle.rectTransform, new Vector2(0, 280), new Vector2(1600, 150));
        var sub = NewText("Subtitle", screen.transform, 46);
        sub.text = "Вы дожили до конца смены. Клуб цел.";
        Place(sub.rectTransform, new Vector2(0, 170), new Vector2(1600, 70));
        end.stats = NewText("Stats", screen.transform, 38);
        end.stats.color = new Color(0.8f, 0.75f, 0.9f);
        Place(end.stats.rectTransform, new Vector2(0, 20), new Vector2(1400, 260));
        // «Следующая ночь» — главная кнопка, надпись (номер или почему закрыта) ставит NightEndScreen
        end.nextButton = MainMenuBuilder.NewButton("Next", screen.transform, "Следующая ночь", new Vector2(0, -190), new Vector2(640, 100), 44);
        end.nextLabel = end.nextButton.GetComponentInChildren<UnityEngine.UI.Text>();
        end.restartButton = MainMenuBuilder.NewButton("Restart", screen.transform, "Заново", new Vector2(-210, -310), new Vector2(380, 90), 40);
        end.menuButton = MainMenuBuilder.NewButton("Menu", screen.transform, "В меню", new Vector2(210, -310), new Vector2(380, 90), 40);
        return end;
    }

    // Звонки: телефон на столе слева, табличка «Программа на ночь» справа над столом (видна, когда смотришь
    // вперёд), окно разговора внизу экрана и CallDirector, который всем этим управляет
    static CallDirector BuildCalls(Transform parent, Camera cam, NightDirector director, MapBuilder.Layout map)
    {
        // Телефон: на карте — белый телефон из Blender (весь аппарат дрожит, когда звонят, рядом лампа), иначе свой:
        // корпус, трубка сверху (дрожит), лампа спереди
        var phoneGo = new GameObject("Phone", typeof(Phone), typeof(AudioSource));
        phoneGo.transform.SetParent(parent, false);
        var dark = new Color(0.12f, 0.12f, 0.14f);
        var phone = phoneGo.GetComponent<Phone>();
        var mapPhone = map?.Find("DeskPhone");
        if (mapPhone != null)
        {
            var pb = MapBuilder.Layout.BoundsOf(mapPhone);
            phoneGo.transform.position = new Vector3(pb.center.x, pb.min.y, pb.center.z);
            var handset = new GameObject("Handset").transform;
            handset.SetParent(phoneGo.transform, false);
            mapPhone.SetParent(handset, true);
            map.Find("DeskPhoneButton")?.SetParent(handset, true);
            var box = phoneGo.AddComponent<BoxCollider>(); // клик по телефону — ровно по аппарату: под ним кнопки
            box.center = new Vector3(0f, pb.extents.y, 0f);
            box.size = pb.size;
            var lamp = Make(PrimitiveType.Sphere, "PhoneLamp", phoneGo.transform,
                new Vector3(pb.extents.x - 0.03f, pb.size.y + 0.012f, -pb.extents.z + 0.03f), Vector3.one * 0.022f,
                new Color(0.2f, 0.05f, 0.05f));
            phone.handset = handset;
            phone.lamp = lamp.GetComponent<Renderer>();
            phone.lift = 0f; // весь аппарат не подпрыгивает — разговор видно по лампе
        }
        else
        {
            phoneGo.transform.localPosition = map != null ? MapPhone : new Vector3(-0.7f, 0.9f, 1.15f);
            phoneGo.transform.localRotation = Quaternion.Euler(0f, 15f, 0f);
            Make(PrimitiveType.Cube, "PhoneBody", phoneGo.transform, new Vector3(0f, 0.035f, 0f), new Vector3(0.22f, 0.07f, 0.16f), dark);
            var handset = Make(PrimitiveType.Cube, "PhoneHandset", phoneGo.transform,
                new Vector3(0f, 0.09f, 0.01f), new Vector3(0.26f, 0.04f, 0.07f), new Color(0.22f, 0.22f, 0.26f));
            var lamp = Make(PrimitiveType.Sphere, "PhoneLamp", phoneGo.transform,
                new Vector3(0.07f, 0.075f, -0.07f), Vector3.one * 0.03f, new Color(0.2f, 0.05f, 0.05f));
            phone.handset = handset.transform;
            phone.lamp = lamp.GetComponent<Renderer>();
        }
        phone.ring = phoneGo.GetComponent<AudioSource>();
        phone.ring.playOnAwake = false;
        phone.ring.loop = true;
        phone.ring.spatialBlend = 0f;

        // Табличка с расписанием: справа сверху, когда смотришь вперёд (камера тогда отъехала назад).
        // Без карты стоит на столбе лицом к игроку. На карте висит ровно на передней стене офиса, в простенке слева
        // от прохода, и ужата по его ширине: шире — левый край с колонкой времени уходит в боковую стену
        var eye = ForwardEye;
        var boardPos = eye + Quaternion.Euler(-7f, 22f, 0f) * Vector3.forward * 2.9f; // справа сверху
        float boardScale = 0.0009f;
        if (map != null)
        {
            var from = map.FromBlender(MapBoardWallFrom, MapFrontWallY, 0f);
            var to = map.FromBlender(MapBoardWallTo, MapFrontWallY, 0f);
            boardScale = (Mathf.Abs(to.x - from.x) - 2f * MapBoardMargin) / BoardPixels.x;
            boardPos = new Vector3((from.x + to.x) / 2f, MapBoardY, from.z - 0.03f);
        }
        var boardCanvas = NewCanvasAt("ScheduleBoard", parent, cam, BoardPixels, boardPos, scale: boardScale,
            lookFrom: map != null ? boardPos - Vector3.forward : eye);
        var back = NewImage("Back", boardCanvas.transform, new Color(0.08f, 0.06f, 0.11f, 0.95f));
        Stretch(back.rectTransform);
        var boardTitle = NewText("Title", boardCanvas.transform, 56);
        boardTitle.text = "ПРОГРАММА НА НОЧЬ";
        boardTitle.color = new Color(1f, 0.85f, 0.35f);
        Place(boardTitle.rectTransform, new Vector2(0, 395), new Vector2(1400, 80));
        var grid = new GameObject("Grid", typeof(RectTransform));
        grid.transform.SetParent(boardCanvas.transform, false);
        Place((RectTransform)grid.transform, new Vector2(0, -45), new Vector2(1440, 780));
        var board = boardCanvas.gameObject.AddComponent<ScheduleBoard>();
        board.grid = (RectTransform)grid.transform;
        float postHeight = boardPos.y - 0.4f;
        if (map == null)
            Make(PrimitiveType.Cylinder, "ScheduleBoardPost", parent,
                new Vector3(boardPos.x, postHeight / 2f, boardPos.z + 0.05f), new Vector3(0.05f, postHeight / 2f, 0.05f), dark);

        // Окно разговора внизу экрана
        var hud = new GameObject("CallHUD", typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler),
            typeof(UnityEngine.UI.GraphicRaycaster), typeof(CallDialog));
        hud.transform.SetParent(parent, false);
        var canvas = hud.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 5; // под окном мог-батла и экраном смерти
        var scaler = hud.GetComponent<UnityEngine.UI.CanvasScaler>();
        scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        var dialog = hud.GetComponent<CallDialog>();
        var panel = NewImage("CallPanel", hud.transform, new Color(0.05f, 0.04f, 0.08f, 0.92f));
        panel.raycastTarget = false; // клики мимо кнопок проходят в сцену (телефон, кнопки на столе)
        var prt = panel.rectTransform;
        prt.anchorMin = prt.anchorMax = prt.pivot = new Vector2(0.5f, 0f);
        prt.anchoredPosition = new Vector2(0, 20);
        prt.sizeDelta = new Vector2(1500, 260);
        dialog.group = panel.gameObject.AddComponent<CanvasGroup>();
        dialog.caller = NewText("Caller", panel.transform, 34);
        dialog.caller.alignment = TextAnchor.UpperLeft;
        dialog.caller.color = new Color(1f, 0.85f, 0.35f);
        Place(dialog.caller.rectTransform, new Vector2(0, 95), new Vector2(1440, 46));
        dialog.line = NewText("Line", panel.transform, 38);
        dialog.line.alignment = TextAnchor.UpperLeft;
        Place(dialog.line.rectTransform, new Vector2(0, 20), new Vector2(1440, 100));
        dialog.hint = NewText("Hint", panel.transform, 28);
        dialog.hint.alignment = TextAnchor.MiddleRight;
        dialog.hint.color = new Color(0.7f, 0.7f, 0.75f);
        Place(dialog.hint.rectTransform, new Vector2(0, -85), new Vector2(1440, 50));
        dialog.options = new UnityEngine.UI.Button[CallBuilder.MaxOptions];
        for (int i = 0; i < dialog.options.Length; i++)
        {
            var button = MainMenuBuilder.NewButton("Option" + (i + 1), panel.transform, "", new Vector2(0, -80), new Vector2(470, 84), 28);
            var label = button.GetComponentInChildren<UnityEngine.UI.Text>();
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Truncate; // иначе не работает подгонка размера
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = 16;
            label.resizeTextMaxSize = 28;
            label.rectTransform.offsetMin = new Vector2(12, 4);
            label.rectTransform.offsetMax = new Vector2(-12, -4);
            dialog.options[i] = button;
        }

        var calls = new GameObject("CallDirector", typeof(CallDirector)).GetComponent<CallDirector>();
        calls.transform.SetParent(parent, false);
        calls.night = director;
        calls.look = cam != null ? cam.GetComponent<CameraLook>() : null;
        calls.phone = phone;
        calls.dialog = dialog;
        calls.board = board;
        return calls;
    }

    // Панель сложности F1: слева экрана, строка на каждого фурри (− уровень +), пресеты и кнопки для Новы
    static DifficultyPanel BuildDifficultyPanel(Transform parent, NightDirector director)
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
        panel.raycastTarget = false; // клик мимо кнопок панели проходит в сцену: телефон частично под панелью
        var pr = panel.rectTransform;
        pr.anchorMin = pr.anchorMax = pr.pivot = new Vector2(0f, 0.5f);
        Place(pr, new Vector2(20, 0), new Vector2(560, 880));
        ui.panel = panel.gameObject;

        var title = NewText("Title", panel.transform, 40);
        title.text = "СЛОЖНОСТЬ (F1)";
        Place(title.rectTransform, new Vector2(0, 340), new Vector2(520, 60));

        string[] names = { "Дир", "Стрей", "Нова", "Дали", "Доли" };
        ui.values = new UnityEngine.UI.Text[5];
        ui.minus = new UnityEngine.UI.Button[5];
        ui.plus = new UnityEngine.UI.Button[5];
        for (int i = 0; i < 5; i++)
        {
            float y = 260 - i * 70;
            var name = NewText("Name" + i, panel.transform, 36);
            name.text = names[i];
            name.alignment = TextAnchor.MiddleLeft;
            Place(name.rectTransform, new Vector2(-150, y), new Vector2(200, 60));
            ui.minus[i] = PanelButton(panel.transform, "Minus" + i, "−", new Vector2(20, y), new Vector2(64, 56));
            ui.values[i] = NewText("Value" + i, panel.transform, 34);
            Place(ui.values[i].rectTransform, new Vector2(120, y), new Vector2(130, 60));
            ui.plus[i] = PanelButton(panel.transform, "Plus" + i, "+", new Vector2(220, y), new Vector2(64, 56));
        }

        ui.easy = PanelButton(panel.transform, "Easy", "Easy", new Vector2(-170, -115), new Vector2(160, 56));
        ui.normal = PanelButton(panel.transform, "Normal", "Normal", new Vector2(0, -115), new Vector2(160, 56));
        ui.hard = PanelButton(panel.transform, "Hard", "Hard", new Vector2(170, -115), new Vector2(160, 56));
        ui.onlyNova = PanelButton(panel.transform, "OnlyNova", "Только Нова", new Vector2(-130, -185), new Vector2(240, 56));
        ui.novaBattle = PanelButton(panel.transform, "NovaBattle", "Нова → батл", new Vector2(130, -185), new Vector2(240, 56));
        ui.ringCall = PanelButton(panel.transform, "RingCall", "Вызвать звонок", new Vector2(-130, -255), new Vector2(240, 56));
        ui.monitorReboot = PanelButton(panel.transform, "MonitorReboot", "Сбой монитора", new Vector2(130, -255), new Vector2(240, 56));
        ui.bossCall = PanelButton(panel.transform, "BossCall", "Начальство", new Vector2(-130, -325), new Vector2(240, 56));
        ui.triggerCall = PanelButton(panel.transform, "TriggerCall", "По условию", new Vector2(130, -325), new Vector2(240, 56));

        ui.info = NewText("Info", panel.transform, 26);
        ui.info.color = new Color(0.75f, 0.75f, 0.8f);
        Place(ui.info.rectTransform, new Vector2(0, -392), new Vector2(520, 90));
        panel.gameObject.SetActive(false);
        return ui;
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

    // Фурри в офисе: модель из Blender (FurryModel), нет модели — капсула ростом 1.8 м. Без коллайдера, чтобы не мешать
    // кликам, без таблички с именем
    static FurryAvatar ModelAvatar(Transform parent, string model, string name, Color color)
    {
        var go = new GameObject("Furry " + name, typeof(FurryAvatar));
        go.transform.SetParent(parent, false);
        if (FurryModel(model, go.transform) == null)
        {
            var body = Make(PrimitiveType.Capsule, "FurryBody" + System.Array.IndexOf(new[] { "Стрей", "Нова", "Дали", "Доли" }, name),
                go.transform, new Vector3(0f, 0.9f, 0f), new Vector3(0.7f, 0.9f, 0.7f), color);
            Object.DestroyImmediate(body.GetComponent<Collider>());
        }
        go.SetActive(false);
        return go.GetComponent<FurryAvatar>();
    }

    // Модель фурри Assets/Models/<model>/<model>.fbx ростом FurryModelHeight, ноги на полу в точке parent, лицом по +Z.
    // Материалы — свои URP Lit по именам из выгрузки (<model>Body, <model>Head…): текстура Textures/<материал>.png,
    // пока её нет — FurryNoTexture. У Новы — NovaMaterials. Нет FBX — null
    static GameObject FurryModel(string model, Transform parent)
    {
        string folder = $"Assets/Models/{model}/";
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(folder + model + ".fbx");
        if (asset == null) return null;
        var go = (GameObject)PrefabUtility.InstantiatePrefab(asset, parent);
        go.name = model + "Model";
        go.transform.localScale *= FurryModelHeight / ModelBounds(go).size.y;
        var b = ModelBounds(go);
        go.transform.position += parent.position - new Vector3(b.center.x, b.min.y, b.center.z);

        var nova = model == "Nova" ? NovaMaterials() : null;
        foreach (var r in go.GetComponentsInChildren<Renderer>())
        {
            var list = r.sharedMaterials;
            for (int i = 0; i < list.Length; i++)
            {
                if (list[i] == null) continue;
                if (nova != null) { if (nova.TryGetValue(list[i].name, out var m)) list[i] = m; }
                else list[i] = FurryMaterial(folder, list[i].name);
            }
            r.sharedMaterials = list;
        }
        return go;
    }

    static Material FurryMaterial(string folder, string name)
    {
        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>($"{folder}Textures/{name}.png");
        return ModelMaterial(folder, name, m =>
        {
            m.SetTexture("_BaseMap", texture);
            m.SetColor("_BaseColor", texture != null ? Color.white : FurryNoTexture);
            m.SetFloat("_Smoothness", 0.25f);
        });
    }

    // Оставить у модели видимыми только эти меши (по именам из выгрузки), вернуть их общие границы
    static Bounds KeepOnly(GameObject model, params string[] names)
    {
        Bounds? b = null;
        foreach (var r in model.GetComponentsInChildren<Renderer>())
        {
            r.enabled = names.Contains(r.gameObject.name);
            if (!r.enabled) continue;
            if (b == null) b = r.bounds;
            else { var x = b.Value; x.Encapsulate(r.bounds); b = x; }
        }
        return b ?? new Bounds(model.transform.position, Vector3.zero);
    }

    // Стрей у двери выглядывает из-за угла: за стеной сбоку от проёма рольставня (у края, ближнего к линии взгляда
    // из кресла), наклонён в проём, лицом к игроку. Без карты — null (стоит в проёме, как все)
    static Transform StrayPeek(Transform parent, MapBuilder.Layout map)
    {
        var curtain = map?.shutter; // шторка рольставня (сборщик двери уже переложил её в Door)
        if (curtain == null) return null;
        var b = MapBuilder.Layout.BoundsOf(curtain);
        float edge = Mathf.Abs(b.max.z - Eye.z) < Mathf.Abs(b.min.z - Eye.z) ? b.max.z : b.min.z;
        float inward = Mathf.Sign(b.center.z - edge); // от края внутрь проёма
        var pos = new Vector3(b.center.x + StrayPeekDepth * Mathf.Sign(b.center.x - Eye.x), 0f, edge - inward * StrayPeekBehind);
        var toPlayer = Eye - pos;
        toPlayer.y = 0f;
        float a = StrayPeekLean * Mathf.Deg2Rad;
        var up = Vector3.up * Mathf.Cos(a) + new Vector3(0f, 0f, inward) * Mathf.Sin(a);
        var t = NewSpot("StrayPeek", parent, pos, 0f);
        t.rotation = Quaternion.LookRotation(toPlayer.normalized, up);
        return t;
    }

    // Рука Дали в вентиляции генераторной: копия модели Дали, видны только правая рука и пальцы. В позе из Blender рука
    // висит вниз — её поворачивают вдоль «из короба и вниз», кончики пальцев — на HandOut снаружи короба, плечо —
    // в его глубине (короб двусторонний, изнутри руку не видно). Без карты или модели — null
    static FurryAvatar DaliHand(Transform parent, MapBuilder.Layout map)
    {
        if (map == null || map.Find(MapVentDuct) == null) return null;
        var go = new GameObject("Furry Дали (рука в вентиляции)", typeof(FurryAvatar));
        go.transform.SetParent(parent, false);
        var model = FurryModel("Dali", go.transform);
        if (model == null)
        {
            Object.DestroyImmediate(go);
            return null;
        }
        var arm = KeepOnly(model, "Right Arm", "Fingers.R");
        var tip = new GameObject("FingerTips").transform;
        tip.SetParent(go.transform, false);
        tip.position = new Vector3(arm.center.x, arm.min.y, arm.center.z);
        model.transform.SetParent(tip, true);

        var v = MapHandVent;
        var vent = map.FromBlender(v.x, v.y, v.z);
        var outward = map.FromBlender(v.x, v.y + 1f, v.z) - vent;
        outward.y = 0f;
        outward.Normalize();
        float d = HandDroop * Mathf.Deg2Rad;
        var dir = (outward * Mathf.Cos(d) + Vector3.down * Mathf.Sin(d)).normalized;
        tip.rotation = Quaternion.FromToRotation(Vector3.down, dir);
        tip.position = vent + dir * HandOut;
        go.SetActive(false);
        return go.GetComponent<FurryAvatar>();
    }

    // Скример (DeathScreamer): головы фурри (копии моделей, только меши головы) на сцене в DeathStageDepth м под
    // офисом, лицом к своей камере; камера снимает в текстуру, её показывает image на экране смерти. Каждая голова
    // стоит так, чтобы занять DeathHeadFill кадра. Свой свет — тёплый спереди сверху и красный снизу
    static DeathScreamer BuildDeathHeads(Transform parent, UnityEngine.UI.RawImage image)
    {
        var go = new GameObject("DeathHeads", typeof(DeathScreamer));
        go.transform.SetParent(parent, false);
        go.transform.position = Vector3.down * DeathStageDepth;
        var screamer = go.GetComponent<DeathScreamer>();
        screamer.heads = new Transform[5];
        float tan = Mathf.Tan(DeathHeadFov * 0.5f * Mathf.Deg2Rad);
        var parts = new (FurryId id, string model, string[] meshes)[]
        {
            (FurryId.Stray, "Stray", new[] { "Head" }),
            (FurryId.Nova, "Nova", new[] { "Cube.006" }),
            (FurryId.Dali, "Dali", new[] { "Head", "Hair", "Piercing" }),
            (FurryId.Doli, "Doli", new[] { "Head", "Hair" }),
        };
        foreach (var (id, model, meshes) in parts)
        {
            var head = new GameObject("Head " + model).transform;
            head.SetParent(go.transform, false);
            var m = FurryModel(model, head);
            if (m == null)
            {
                Object.DestroyImmediate(head.gameObject);
                continue;
            }
            var hb = KeepOnly(m, meshes);
            var face = KeepOnly(m, meshes[0]).center; // в кадре по центру — сама голова, не причёска над ней
            KeepOnly(m, meshes);
            m.transform.position += head.position - face;
            hb.center = face;
            float size = Mathf.Max(hb.size.x, hb.size.y); // причёска выше головы — уходит за верх кадра, как в скримере и надо
            float distance = size * 0.5f / DeathHeadFill / tan + hb.extents.z;
            head.localPosition = new Vector3(0f, 0f, DeathCameraZ - distance);
            screamer.heads[(int)id] = head;
        }

        var cam = new GameObject("HeadCamera", typeof(Camera)).GetComponent<Camera>();
        cam.transform.SetParent(go.transform, false);
        cam.transform.localPosition = new Vector3(0f, 0f, DeathCameraZ);
        cam.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
        cam.fieldOfView = DeathHeadFov;
        cam.nearClipPlane = 0.05f;
        cam.farClipPlane = DeathCameraZ + 2f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0f, 0f, 0f, 0f); // фон прозрачный — видно затемнённую игру
        cam.enabled = false;
        screamer.headCamera = cam;
        void StageLight(string name, Vector3 at, Color color, float intensity)
        {
            var l = new GameObject(name, typeof(Light)).GetComponent<Light>();
            l.transform.SetParent(go.transform, false);
            l.transform.localPosition = at;
            l.type = LightType.Point;
            l.range = DeathCameraZ + 3f;
            l.color = color;
            l.intensity = intensity;
            l.shadows = LightShadows.None;
        }
        StageLight("Key", new Vector3(0.5f, 0.6f, DeathCameraZ - 0.3f), new Color(1f, 0.92f, 0.85f), 3f);
        StageLight("Under", new Vector3(-0.3f, -0.8f, DeathCameraZ - 1.5f), new Color(1f, 0.15f, 0.1f), 2f);
        screamer.image = image;
        return screamer;
    }

    // Материалы Новы. Тело — текстура Nova_Body.png (выгружает export_nova.bat), без неё — светлый мех
    // как у головы. Детали лица — плоские цвета без света, как в Blender (цвет сразу в выход)
    static Dictionary<string, Material> NovaMaterials()
    {
        var fur = new Color(0.9f, 0.9f, 0.9f); // Blender 0.8 (линейный) ≈ 0.9 на экране
        var body = AssetDatabase.LoadAssetAtPath<Texture2D>(NovaModelFolder + "Textures/Nova_Body.png");
        return new Dictionary<string, Material>
        {
            ["NovaBody"] = ModelMaterial(NovaModelFolder, "NovaBody", m =>
            {
                m.SetTexture("_BaseMap", body);
                m.SetColor("_BaseColor", body != null ? Color.white : fur);
                m.SetFloat("_Smoothness", 0.5f);
            }),
            ["NovaFur"] = ModelMaterial(NovaModelFolder, "NovaFur", m =>
            {
                m.SetColor("_BaseColor", fur);
                m.SetFloat("_Smoothness", 0f);
            }),
            ["NovaBlack"] = ModelMaterial(NovaModelFolder, "NovaBlack", m => m.SetColor("_BaseColor", Color.black),
                "Universal Render Pipeline/Unlit"),
            ["NovaRed"] = ModelMaterial(NovaModelFolder, "NovaRed", m => m.SetColor("_BaseColor", new Color(1f, 0.03f, 0f)),
                "Universal Render Pipeline/Unlit"),
        };
    }

    // Кнопка из карты: объект с PressControl в центре кнопки и коллайдером чуть больше неё (кнопки 3 см и стоят
    // вплотную); шляпка переезжает под этот объект и утапливается в стол — по самой тонкой оси кнопки, от игрока.
    // Возвращает Renderer шляпки (у звука это лампа, она вспыхивает на гудок)
    static Renderer MapButton(Transform parent, string name, Transform body, Transform cap, Deterrent target)
    {
        var b = MapBuilder.Layout.BoundsOf(body);
        var go = new GameObject(name, typeof(PressControl), typeof(BoxCollider));
        go.transform.SetParent(parent, false);
        go.transform.position = b.center;
        var into = b.size.x < b.size.z ? Vector3.right * Mathf.Sign(b.center.x) : Vector3.forward * Mathf.Sign(b.center.z);
        // Область клика: по ширине — не шире MapButtonClick (кнопки вплотную), выше кнопки и ближе к игроку:
        // над кнопками стоит телефон, луч из глаз должен попасть в кнопку раньше, чем в него
        var box = go.GetComponent<BoxCollider>();
        box.size = new Vector3(MapButtonClick, MapButtonClickHeight, MapButtonClickDepth);
        float depth = Mathf.Abs(Vector3.Dot(b.extents, into)); // полтолщины кнопки вдоль «в стол»
        box.center = Vector3.up * (MapButtonClickHeight / 2f - b.extents.y) - into * (MapButtonClickDepth / 2f - depth);
        cap.SetParent(go.transform, true);
        var control = go.GetComponent<PressControl>();
        control.target = target;
        control.cap = cap;
        control.pressDirection = go.transform.InverseTransformDirection(into);
        control.pressDepth = MapButtonPress;
        return cap.GetComponentInChildren<Renderer>();
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
    // из офиса), над рычагом табличка с состоянием и обратным отсчётом.
    // На карте — рольставень из Blender, рычаг и табличка на правой стене впереди от него.
    static Door BuildDoor(Transform parent, Camera cam, Generator generator, MapBuilder.Layout map)
    {
        var go = new GameObject("Door", typeof(Door));
        go.transform.SetParent(parent, false);
        var door = go.GetComponent<Door>();
        door.generator = generator;

        // Стена с рычагом и табличкой (сторона офиса). Без карты — правая стена полигона, за спиной справа
        var wall = new Vector3(3.4f, 0f, -1.15f);
        if (map != null && map.shutter != null)
        {
            // На карте дверь — рольставень: шторка опускается (закрыто) и поднимается под потолок (открыто)
            door.slab = map.shutter;
            door.slab.SetParent(go.transform, true);
            door.slabClosed = door.slab.localPosition;
            door.slabOpen = door.slabClosed + Vector3.up * ShutterLift;
            door.slab.localPosition = door.slabOpen;
            wall = map.FromBlender(MapDoorWallX, MapDoorControlsY, 0f);
        }
        else
        {
            door.slabClosed = new Vector3(3.5f, 1.1f, 0f);   // закрывает проём 1.2 × 2.2
            door.slabOpen = new Vector3(3.5f, 1.1f, 1.25f);  // спрятана в стене рядом с проёмом
            door.slab = Make(PrimitiveType.Cube, "DoorSlab", go.transform, door.slabOpen,
                new Vector3(0.08f, 2.2f, 1.2f), new Color(0.35f, 0.4f, 0.45f)).transform;
        }

        var handle = map?.Find("DoorLeverHandle");
        if (handle != null)
        {
            // Рычаг из карты (рычаг на стене не нужен). Автор поставил его под столешницу спереди, пользователь попросил
            // справа, под правую руку (2026-11-05): крепление и ручка переезжают под край столешницы справа, ручкой
            // к креслу. Ось — у дальнего конца ручки, закрыто — ручка вниз. Ручка в карте смотрит на игрока (−Z)
            var hb = MapBuilder.Layout.BoundsOf(handle);
            var mount = new GameObject("DoorLeverMount").transform; // крепление: не качается
            mount.SetParent(go.transform, false);
            mount.position = new Vector3(hb.center.x, hb.center.y, hb.max.z);
            var pivot = new GameObject("DoorLever", typeof(PressControl), typeof(BoxCollider));
            pivot.transform.SetParent(mount, false);
            handle.SetParent(pivot.transform, true);
            map.Find("DoorLeverBase")?.SetParent(mount, true);
            // Кончик ручки — в MapLeverTip, ручка поворачивается на MapLeverYaw: была к игроку, станет к креслу справа
            var rotation = Quaternion.Euler(0f, MapLeverYaw, 0f);
            var tip = new Vector3(MapLeverTip.x, hb.center.y, MapLeverTip.z);
            mount.SetPositionAndRotation(tip - rotation * (Vector3.back * hb.size.z), rotation);
            var box = pivot.GetComponent<BoxCollider>();
            // Область клика выступает к игроку и чуть выше столешницы: сам рычаг под ней и виден плохо
            box.center = new Vector3(0f, MapLeverClickWidth / 2f - hb.extents.y, -(hb.size.z + MapLeverReach) / 2f);
            box.size = new Vector3(MapLeverClickWidth, MapLeverClickWidth + hb.size.y, hb.size.z + MapLeverReach);
            pivot.GetComponent<PressControl>().target = door;
            door.lever = pivot.transform;
            door.leverOpen = Quaternion.identity;
            door.leverClosed = Quaternion.Euler(-MapLeverAngle, 0f, 0f);
        }
        else BuildWallLever(go.transform, door, wall);

        // Табличка висит на стене (параллельно ей), поэтому смотрит строго по +X
        var panelPos = new Vector3(wall.x - 0.03f, handle != null ? MapDoorPanelY : 1.85f, wall.z);
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

    // Рычаг на стене справа от проёма (без карты): пластина, ось с ручкой и красным шаром
    static void BuildWallLever(Transform parent, Door door, Vector3 wall)
    {
        var go = parent.gameObject;
        Make(PrimitiveType.Cube, "DoorLeverPlate", go.transform, new Vector3(wall.x - 0.02f, 1.3f, wall.z),
            new Vector3(0.04f, 0.35f, 0.2f), new Color(0.25f, 0.25f, 0.27f));
        var lever = new GameObject("DoorLever", typeof(PressControl));
        lever.transform.SetParent(go.transform, false);
        lever.transform.localPosition = new Vector3(wall.x - 0.06f, 1.3f, wall.z);
        lever.transform.localRotation = Quaternion.Euler(0f, 0f, 30f); // открыто: рычаг вверх
        lever.GetComponent<PressControl>().target = door;
        door.lever = lever.transform;
        Make(PrimitiveType.Cylinder, "DoorLeverArm", lever.transform,
            new Vector3(0f, 0.15f, 0f), new Vector3(0.04f, 0.15f, 0.04f), new Color(0.15f, 0.15f, 0.15f));
        Make(PrimitiveType.Sphere, "DoorLeverKnob", lever.transform,
            new Vector3(0f, 0.3f, 0f), Vector3.one * 0.09f, new Color(0.85f, 0.1f, 0.1f));
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

    internal static void Place(RectTransform r, Vector2 pos, Vector2 size)
    {
        r.anchoredPosition = pos;
        r.sizeDelta = size;
    }

    // Смена: таймер. Часы — на мониторе камер (BuildMonitor), в углу экрана ни ночи, ни времени нет
    static ShiftTimer BuildShift(UnityEngine.UI.Text clock)
    {
        var shift = new GameObject(ShiftName).AddComponent<ShiftTimer>();
        shift.clock = clock;
        return shift;
    }

    // Глаза игрока в офисе: на карте — сидя (CameraLook.SeatedEyeHeight), без карты — стоя, как раньше.
    // Ставится в начале Build: от них зависят канвасы, которые смотрят на игрока
    static Vector3 Eye = new Vector3(0, 1.6f, 0);
    static Vector3 ForwardEye => Eye + CameraLook.ForwardLean; // глаза, когда смотришь вперёд

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

    // Старый полигон, если карты клуба нет: платформа, три дорожки и стены с проёмами
    static void BuildPrimitiveOffice(Transform root)
    {
        // Центральная платформа
        Make(PrimitiveType.Cube, "CenterPlatform", root,
            new Vector3(0, -0.25f, 0), new Vector3(8, 0.5f, 8), new Color(0.35f, 0.35f, 0.4f));

        // Три направления: влево (там генератор, поэтому без объекта на конце), вперёд, вправо
        BuildArm(root, "Left",    Vector3.left,    new Color(0.8f, 0.25f, 0.25f), null);
        BuildArm(root, "Forward", Vector3.forward, new Color(0.25f, 0.7f, 0.3f),  PrimitiveType.Sphere);
        BuildArm(root, "Right",   Vector3.right,   new Color(0.25f, 0.4f, 0.85f), PrimitiveType.Cylinder);

        // Стены офиса слева и справа, в каждой дверной проём: слева — к генератору, справа — в коридор 2
        BuildWallWithDoorway(root, "LeftWall", -3.5f);
        BuildWallWithDoorway(root, "RightWall", 3.5f);
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
