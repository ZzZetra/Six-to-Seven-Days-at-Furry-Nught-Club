using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

// Редактор раскладок мог-батла — отдельная программа «SSDFNC Chart Editor» (сцена MogEditor; собрать —
// SSDFNC → Chart Editor → Build App, в Unity — SSDFNC → Chart Editor → Open in Play Mode).
// Интерфейс нарисован на IMGUI (OnGUI) — для инструмента так проще всего. Сам скилл-чек — настоящее окно
// мог-батла (MogBattle), поменьше, посередине внизу, и его можно править мышью прямо там.
//   Сверху: файл («Выход», ◄ ► — листать, «Новая», «Открыть…», «Сохранить»), папка, музыка, сдвиг.
//   Ниже: ►/|| (ПРОБЕЛ), перемотка (← →), масштаб, BPM и сетка, вид нового чека, «Отметить бит» (B),
//         «Проверить» — пройти раскладку в окне мог-батла во весь размер.
//   Полоса времени: волна звука, сетка битов, чеки. Клик по чеку — выбрать, тянуть — двигать по времени,
//   клик по пустому месту — перемотать, колесо — масштаб. Delete — удалить выбранный чек, Ctrl+S — сохранить.
//   Внизу: слева список чеков, посередине — сам чек (ЛКМ — место кнопки или зоны, ПКМ — ширина зоны),
//   справа — параметры выбранного.
// Папка раскладок — Assets/StreamingAssets/MogCharts проекта (путь кладёт сборщик программы в FolderFile);
// другую можно выбрать через «Открыть…». Оттуда раскладки берут атака Новы и проверка скилл-чеков.
public class MogChartEditor : MonoBehaviour
{
    const float RefWidth = 1920f, RefHeight = 1080f;
    static readonly Rect Timeline = new Rect(20f, 196f, 1880f, 260f);
    static readonly Rect ListRect = new Rect(20f, 490f, 380f, 580f);
    static readonly Rect PreviewRect = new Rect(420f, 500f, 800f, 380f);
    static readonly Rect InspectorRect = new Rect(1240f, 490f, 660f, 580f);
    const int PeaksPerSecond = 200;
    const int WaveWidth = 1880, WaveHeight = 260;
    const string FolderKey = "SSDFNC.ChartEditor.Folder";       // PlayerPrefs программы: последняя папка
    public const string FolderFile = "chart_editor_folder.txt"; // в StreamingAssets программы: папка проекта

    static readonly string[] TypeIds = { "circle", "button", "arrow", "bar" };
    static readonly string[] TypeNames = { "Кружок", "Кнопка", "Стрелка", "Полоска" };
    static readonly string[] DirIds = { "right", "up", "left", "down" };
    static readonly string[] DirNames = { "→", "↑", "←", "↓" };
    static readonly Color[] TypeColors =
    {
        new Color(0.3f, 0.9f, 0.4f), new Color(0.75f, 0.45f, 1f), new Color(1f, 0.85f, 0.3f), new Color(0.35f, 0.7f, 1f),
    };

    public AudioSource music;
    public MogBattle battle;          // сам скилл-чек: превью и проверка раскладки

    List<string> files = new List<string>();
    int fileIndex = -1;
    string fileName = "new_chart";
    MogChart chart;
    MogCheck selected;
    MogCheck shown;                   // чек в окне посередине
    string status = "";
    string problem, warning;          // считаются раз в кадр (Update)
    string savedJson = "";            // раскладка, как она лежит в файле: отличается — есть несохранённые правки
    bool dirty;
    string confirm;                   // какое действие ждёт второго нажатия (несохранённые правки)

    AudioClip clip;
    float[] peaks;
    List<string> musicFiles = new List<string>();
    bool playing;
    float head;                       // где сейчас время раскладки, с
    float viewStart, viewLength = 20f;
    string newType = "circle";
    float bpm = 120f, firstBeat;
    bool snap = true;
    Texture2D wave;
    float waveStart = -1f, waveLength = -1f, waveOffset;
    bool dragging;
    float dragFrom;
    int previewDrag;                  // 0 — нет, 1 — ЛКМ (место), 2 — ПКМ (ширина зоны)
    Vector2 listScroll, inspectorScroll;
    readonly Dictionary<string, string> edits = new Dictionary<string, string>();

    float MusicLength => clip != null ? clip.length - chart.offset : 0f;
    float Duration => Mathf.Max(30f, MusicLength, chart.checks.Length > 0 ? chart.LastCheckEnd + 5f : 0f);

    void Awake()
    {
        MogCharts.Folder = ChooseFolder();
        Application.targetFrameRate = 60;
    }

    void Start()
    {
        RefreshFiles();
        if (files.Count > 0) Open(0);
        else New();
    }

    // Папка раскладок: в Unity — папка проекта; в программе — последняя выбранная, иначе та, что записал
    // сборщик программы (папка проекта), иначе своя StreamingAssets/MogCharts
    static string ChooseFolder()
    {
        if (Application.isEditor) return null;
        string saved = PlayerPrefs.GetString(FolderKey, "");
        if (saved != "" && Directory.Exists(saved)) return saved;
        string baked = Path.Combine(Application.streamingAssetsPath, FolderFile);
        if (File.Exists(baked))
        {
            string path = File.ReadAllText(baked).Trim();
            if (Directory.Exists(path)) return path;
        }
        return null;
    }

    // ── Файлы ──

    void RefreshFiles()
    {
        Directory.CreateDirectory(MogCharts.Folder);
        files = Directory.GetFiles(MogCharts.Folder, "*.json", SearchOption.AllDirectories)
            .Select(p => Relative(p)).OrderBy(p => p.Contains("/") ? 1 : 0).ThenBy(p => p).ToList();
        musicFiles = Directory.GetFiles(MogCharts.Folder, "*.*", SearchOption.AllDirectories)
            .Where(p => new[] { ".ogg", ".wav", ".mp3" }.Contains(Path.GetExtension(p).ToLowerInvariant()))
            .Select(p => Relative(p)).OrderBy(p => p).ToList();
    }

    static string Root => Path.GetFullPath(MogCharts.Folder).TrimEnd('\\', '/');

    static string Relative(string path) =>
        Path.GetFullPath(path).Substring(Root.Length).TrimStart('\\', '/').Replace('\\', '/');

    void Open(int index)
    {
        fileIndex = index;
        string path = Path.Combine(MogCharts.Folder, files[index]);
        try
        {
            chart = JsonUtility.FromJson<MogChart>(File.ReadAllText(path)) ?? new MogChart();
            if (chart.checks == null) chart.checks = new MogCheck[0];
            foreach (var c in chart.checks)
                if (c.speedKeys == null) c.speedKeys = new MogSpeedKey[0];
            fileName = files[index].Substring(0, files[index].Length - 5);
            status = $"Открыта раскладка {files[index]}";
        }
        catch (System.Exception e)
        {
            New();
            status = $"Не читается {files[index]}: {e.Message}";
        }
        Sort();
        savedJson = chart.ToJson();
        selected = null;
        viewStart = 0f;
        Seek(0f);
        LoadMusic();
    }

    void New()
    {
        chart = new MogChart { name = "Новая раскладка" };
        savedJson = chart.ToJson();
        fileIndex = -1;
        fileName = "new_chart";
        selected = null;
        viewStart = 0f;
        Seek(0f);
        LoadMusic();
        status = "Новая раскладка: выбери музыку и расставляй чеки (B — чек под бит)";
    }

    // Любой json через окно Windows. Не из нынешней папки — она становится папкой раскладок (и запоминается)
    void OpenAny()
    {
        string path = NativeFileDialog.Open("Открыть раскладку", "Раскладки", "json", MogCharts.Folder);
        if (path == null) return;
        path = Path.GetFullPath(path);
        if (!path.StartsWith(Root + Path.DirectorySeparatorChar, System.StringComparison.OrdinalIgnoreCase))
            UseFolder(Path.GetDirectoryName(path));
        RefreshFiles();
        int index = files.IndexOf(Relative(path));
        if (index >= 0) Open(index);
    }

    void UseFolder(string folder)
    {
        MogCharts.Folder = folder;
        PlayerPrefs.SetString(FolderKey, folder);
        PlayerPrefs.Save();
    }

    void Save()
    {
        string error = ValidationError();
        if (error != null)
        {
            status = "Не сохранено: " + error;
            return;
        }
        string name = string.IsNullOrWhiteSpace(fileName) ? "new_chart" : fileName.Trim();
        string path = Path.Combine(MogCharts.Folder, name + ".json");
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        Sort();
        savedJson = chart.ToJson();
        File.WriteAllText(path, savedJson);
        confirm = null;
        RefreshFiles();
        fileIndex = files.IndexOf(name.Replace('\\', '/') + ".json");
        status = name.Contains("/") || name.Contains("\\")
            ? $"Сохранено: {name}.json — видно в проверке скилл-чеков (атака Новы берёт только файлы прямо в MogCharts)"
            : $"Сохранено: {name}.json — раскладка появится в атаке Новы и в проверке скилл-чеков";
    }

    // Есть несохранённые правки: первое нажатие только предупреждает, второе — делает
    bool Confirmed(string action)
    {
        if (!dirty || confirm == action)
        {
            confirm = null;
            return true;
        }
        confirm = action;
        status = "Есть несохранённые правки: нажми ещё раз, чтобы продолжить без сохранения, или «Сохранить»";
        return false;
    }

    static void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    string ValidationError()
    {
        if (chart.checks.Length == 0) return "нет ни одного скилл-чека";
        return new MogChart { checks = chart.checks.ToArray() }.Validate();
    }

    // Не ошибка, но игрок не успеет: на экране один чек, и если в следующий можно попасть раньше бита
    // этого, этот сгорит промахом (MogTimeline.Update)
    string DenseWarning()
    {
        var sorted = chart.checks.OrderBy(c => c.StartTime).ToArray();
        for (int i = 0; i + 1 < sorted.Length; i++)
        {
            var c = sorted[i];
            if (sorted[i + 1].ReadyTime(Balance.MogHitWindow) <= Beat(c))
                return $"чек {i + 1} ({TypeNames[(int)c.Type]}, {Beat(c):0.00} с) не успеть: следующий начинается раньше его бита";
        }
        return null;
    }

    // Когда в чек надо попасть: у полоски — когда метка первый раз в центре зоны
    static float Beat(MogCheck c) => c.Type == MogCheckType.Bar ? ZoneTimes(c).DefaultIfEmpty(c.start).First() : c.hit;

    // ── Музыка ──

    void LoadMusic()
    {
        StopPlaying();
        clip = null;
        peaks = null;
        music.clip = null;
        waveStart = -1f;
        string path = MogCharts.MusicPath(chart);
        if (path == null) return;
        if (!File.Exists(path))
        {
            status = $"Нет файла музыки {chart.music}";
            return;
        }
        StartCoroutine(MogCharts.LoadClip(path, c =>
        {
            if (c == null)
            {
                status = $"Музыка {chart.music} не читается";
                return;
            }
            clip = c;
            music.clip = c;
            BuildPeaks();
        }));
    }

    // Скопировать музыку в папку раскладок (Music/) и поставить её этой раскладке
    void ImportMusic(string src)
    {
        if (!File.Exists(src))
        {
            status = "Нет такого файла: " + src;
            return;
        }
        string ext = Path.GetExtension(src).ToLowerInvariant();
        if (ext != ".ogg" && ext != ".wav" && ext != ".mp3")
        {
            status = "Музыка — только .ogg, .wav или .mp3";
            return;
        }
        string dir = Path.Combine(MogCharts.Folder, "Music");
        Directory.CreateDirectory(dir);
        string dst = Path.Combine(dir, Path.GetFileName(src));
        if (!string.Equals(Path.GetFullPath(src), Path.GetFullPath(dst), System.StringComparison.OrdinalIgnoreCase))
            File.Copy(src, dst, true);
        chart.music = "Music/" + Path.GetFileName(src);
        RefreshFiles();
        LoadMusic();
        status = $"Музыка скопирована в папку раскладок: {chart.music}";
    }

    // Громкость по кусочкам 1/PeaksPerSecond с — для волны на полосе времени
    void BuildPeaks()
    {
        int per = Mathf.Max(1, clip.frequency / PeaksPerSecond), channels = clip.channels, total = clip.samples;
        peaks = new float[total / per + 1];
        const int chunk = 65536;
        var buffer = new float[chunk * channels];
        for (int start = 0; start < total; start += chunk)
        {
            int count = Mathf.Min(chunk, total - start);
            if (count != chunk) buffer = new float[count * channels]; // последний кусок — ровно до конца, иначе GetData пойдёт по кругу
            clip.GetData(buffer, start);
            for (int i = 0; i < count; i++)
            {
                float v = 0f;
                for (int ch = 0; ch < channels; ch++) v = Mathf.Max(v, Mathf.Abs(buffer[i * channels + ch]));
                int p = (start + i) / per;
                if (v > peaks[p]) peaks[p] = v;
            }
        }
        waveStart = -1f;
    }

    // ── Время ──

    void Update()
    {
        problem = ValidationError();
        warning = problem == null ? DenseWarning() : null;
        dirty = chart.ToJson() != savedJson;
        var kb = Keyboard.current;
        if (battle.Running)
        {
            if (kb != null && kb.escapeKey.wasPressedThisFrame)
            {
                battle.Abort();
                status = "Проверка прервана";
            }
            return;
        }
        if (!playing) return;
        if (clip != null)
        {
            head = music.time - chart.offset;
            if (!music.isPlaying) StopPlaying();
        }
        else head += Time.deltaTime;
        if (head >= Duration) StopPlaying();
        if (head > viewStart + viewLength * 0.9f || head < viewStart) viewStart = Mathf.Max(0f, head - viewLength * 0.1f);
    }

    // Окно скилл-чека посередине внизу: что показать и в какой момент
    void LateUpdate()
    {
        if (battle.Running) return;
        battle.FitPanel(ToScreen(PreviewRect));
        float at = 0f;
        shown = ShownCheck(ref at);
        if (shown != null) battle.ShowStill(shown, at);
        else battle.HideStill();
    }

    // Пока играет музыка — чек, который сейчас на экране у игрока. Иначе выбранный: в момент красной линии,
    // если она на нём, или в его бит
    MogCheck ShownCheck(ref float at)
    {
        if (playing)
        {
            at = head;
            float t = head;
            return chart.checks.FirstOrDefault(c => t >= c.StartTime && t <= c.EndTime(Balance.MogHitWindow))
                   ?? chart.checks.FirstOrDefault(c => c.StartTime > t);
        }
        if (selected == null) return null;
        at = head >= selected.StartTime && head <= selected.EndTime(Balance.MogHitWindow) ? head : Beat(selected);
        return selected;
    }

    void TogglePlay()
    {
        if (playing) StopPlaying();
        else
        {
            playing = true;
            if (clip != null)
            {
                music.time = Mathf.Clamp(head + chart.offset, 0f, clip.length - 0.05f);
                music.Play();
            }
        }
    }

    void StopPlaying()
    {
        playing = false;
        if (music != null) music.Pause();
    }

    void Seek(float t)
    {
        head = Mathf.Clamp(t, 0f, chart != null ? Duration : 0f);
        if (clip != null) music.time = Mathf.Clamp(head + chart.offset, 0f, clip.length - 0.05f);
    }

    float Snapped(float t)
    {
        if (!snap || bpm <= 0f) return t;
        float step = 60f / bpm;
        return firstBeat + Mathf.Round((t - firstBeat) / step) * step;
    }

    // ── Чеки ──

    void AddAt(float t)
    {
        t = Mathf.Max(0.1f, Snapped(t));
        MogCheck c;
        switch (newType)
        {
            case "circle": c = new MogCheck { type = "circle", hit = t, pos = 0.5f, zone = 0.2f, speed = 0.6f }; break;
            case "button": c = new MogCheck { type = "button", hit = t, x = 0.5f, y = 0.5f, approach = 1f }; break;
            case "arrow": c = new MogCheck { type = "arrow", hit = t, dir = "right", approach = 1f }; break;
            default: c = new MogCheck { type = "bar", start = Mathf.Max(0f, t - 1f), end = t + 1f, hit = t, pos = 0.5f, zone = 0.2f, speed = 0.5f }; break;
        }
        chart.checks = chart.checks.Append(c).ToArray();
        Sort();
        selected = c;
        status = $"{TypeNames[System.Array.IndexOf(TypeIds, newType)]} на {t:0.00} с";
    }

    void Delete(MogCheck c)
    {
        chart.checks = chart.checks.Where(x => x != c).ToArray();
        if (selected == c) selected = null;
    }

    void Sort() => chart.checks = chart.checks.OrderBy(c => c.StartTime).ToArray();

    // Сдвинуть чек целиком так, чтобы его бит (у полоски — начало) оказался в t
    void MoveTo(MogCheck c, float t)
    {
        if (c.Type == MogCheckType.Bar)
        {
            float d = t - c.start;
            c.end = c.BarEnd + d;
            c.start += d;
            c.hit += d;
            foreach (var k in c.speedKeys) k.at += d;
        }
        else c.hit = t;
    }

    // Пройти раскладку в окне мог-батла во весь размер: с начала или с секунды до красной линии
    void Preview(bool fromStart)
    {
        var copy = JsonUtility.FromJson<MogChart>(chart.ToJson());
        string error = copy.Validate();
        if (error != null)
        {
            status = "Ошибка: " + error;
            return;
        }
        float from = fromStart ? 0f : Mathf.Max(0f, head - 1f);
        copy.checks = copy.checks.Where(c => c.StartTime >= from - 0.001f).ToArray();
        if (copy.checks.Length == 0)
        {
            status = "Дальше этого места чеков нет";
            return;
        }
        StopPlaying();
        status = "Проверка: Esc — назад в редактор";
        battle.BeginTest(copy, won =>
        {
            var t = battle.Timeline;
            status = $"Проверка: {(won ? "пройдено" : $"провал на чеке {t.Index} из {t.Count}")} — попаданий {t.Hits}, промахов {t.Misses}";
        }, from);
    }

    // Правка мышью в окне скилл-чека. ЛКМ: кнопка и стрелка — место, кружок и полоска — где зона; ПКМ — ширина зоны
    void DragPreview(Vector2 mouse)
    {
        var c = selected;
        var s = ToScreen(mouse);
        bool width = previewDrag == 2;
        switch (c.Type)
        {
            case MogCheckType.Circle:
            {
                float turn = battle.CircleTurnAt(s);
                if (width) c.zone = Mathf.Clamp(Round(2f * TurnDistance(turn, c.pos)), 0.03f, 0.5f);
                else c.pos = Mathf.Clamp(Round(turn), 0.02f, 1f);
                break;
            }
            case MogCheckType.Bar:
            {
                float p = battle.BarPosAt(s);
                if (width) c.zone = Mathf.Clamp(Round(2f * Mathf.Abs(p - c.pos)), 0.03f, 0.5f);
                else c.pos = Mathf.Clamp(Round(p), 0.02f, 1f);
                break;
            }
            default:
                if (width) break;
                var at = battle.PlaceAt(s, c.Type);
                c.x = Round(at.x);
                c.y = Round(at.y);
                break;
        }
    }

    static float Round(float v) => Mathf.Round(v * 100f) / 100f;

    static float TurnDistance(float a, float b)
    {
        float d = Mathf.Abs(a - b) % 1f;
        return Mathf.Min(d, 1f - d);
    }

    // Точка и прямоугольник из координат окна редактора (1920×1080, y сверху) в пиксели экрана (y снизу)
    static Vector2 ToScreen(Vector2 gui) =>
        new Vector2(gui.x * Screen.width / RefWidth, Screen.height - gui.y * Screen.height / RefHeight);

    static Rect ToScreen(Rect gui)
    {
        float sx = Screen.width / RefWidth, sy = Screen.height / RefHeight;
        return new Rect(gui.x * sx, Screen.height - gui.yMax * sy, gui.width * sx, gui.height * sy);
    }

    // ── Рисование ──

    void OnGUI()
    {
        GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(Screen.width / RefWidth, Screen.height / RefHeight, 1f));
        GUI.skin.label.fontSize = GUI.skin.button.fontSize = GUI.skin.textField.fontSize = GUI.skin.toggle.fontSize = 18;
        GUI.skin.label.richText = true;
        if (battle.Running)
        {
            GUI.Label(new Rect(20f, 20f, 900f, 30f), "Проверка раскладки — Esc: назад в редактор");
            return;
        }
        HandleKeys();

        GUILayout.BeginArea(new Rect(20f, 8f, 1880f, 182f));
        DrawFileRow();
        DrawMusicRow();
        DrawTransportRow();
        GUILayout.EndArea();

        DrawTimeline();
        DrawPreview();

        GUILayout.BeginArea(ListRect);
        DrawList();
        GUILayout.EndArea();
        GUILayout.BeginArea(InspectorRect);
        DrawInspector();
        GUILayout.EndArea();
    }

    void HandleKeys()
    {
        var e = Event.current;
        if (e.type != EventType.KeyDown || GUI.GetNameOfFocusedControl() != "") return; // печатают в поле
        switch (e.keyCode)
        {
            case KeyCode.S when e.control: Save(); break;
            case KeyCode.Space: TogglePlay(); break;
            case KeyCode.B: AddAt(head); break;
            case KeyCode.Delete: if (selected != null) Delete(selected); break;
            case KeyCode.LeftArrow: Seek(head - (e.shift ? 5f : 1f)); break;
            case KeyCode.RightArrow: Seek(head + (e.shift ? 5f : 1f)); break;
            default: return;
        }
        e.Use();
        Relayout();
    }

    void DrawFileRow()
    {
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Выход", GUILayout.Width(90)) && Confirmed("quit")) Quit();
        GUI.enabled = files.Count > 0;
        if (GUILayout.Button("◄", GUILayout.Width(40)) && Confirmed("prev"))
            Changed(() => Open((fileIndex - 1 + files.Count) % files.Count));
        GUILayout.Label((dirty ? "* " : "") + (fileIndex >= 0 ? $"{files[fileIndex]}  ({fileIndex + 1}/{files.Count})" : "новая, не сохранена"),
            GUILayout.Width(300));
        if (GUILayout.Button("►", GUILayout.Width(40)) && Confirmed("next"))
            Changed(() => Open((fileIndex + 1) % files.Count));
        GUI.enabled = true;
        if (GUILayout.Button("Новая", GUILayout.Width(90)) && Confirmed("new")) Changed(New);
        if (GUILayout.Button("Открыть…", GUILayout.Width(120)) && Confirmed("open")) Changed(OpenAny);
        GUILayout.Space(20);
        GUILayout.Label("Имя:", GUILayout.Width(45));
        GUI.SetNextControlName("chartName");
        chart.name = GUILayout.TextField(chart.name ?? "", GUILayout.Width(300));
        GUILayout.Label("Файл:", GUILayout.Width(55));
        GUI.SetNextControlName("fileName");
        fileName = GUILayout.TextField(fileName, GUILayout.Width(220));
        GUILayout.Label(".json", GUILayout.Width(50));
        if (GUILayout.Button("Сохранить", GUILayout.Width(120))) Save();
        GUILayout.EndHorizontal();

        GUILayout.Label(problem != null && chart.checks.Length > 0 ? $"<color=#ff7060>Ошибка: {problem}</color>   {status}"
            : warning != null ? $"<color=#ffd060>Внимание: {warning}</color>   {status}"
            : $"<color=#c0b0e0>{status}</color>");
    }

    void DrawMusicRow()
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label("Папка:", GUILayout.Width(65));
        GUILayout.Label(Short(Root, 44), GUILayout.Width(430));
        if (GUILayout.Button("Проводник", GUILayout.Width(120)))
        {
            RefreshFiles();
            Application.OpenURL("file:///" + Root.Replace('\\', '/'));
        }
        GUILayout.Space(20);
        GUILayout.Label("Музыка:", GUILayout.Width(80));
        GUILayout.Label(string.IsNullOrEmpty(chart.music) ? "нет" : chart.music + (clip != null ? $"  ({clip.length:0.0} с)" : "  (грузится…)"),
            GUILayout.Width(330));
        if (GUILayout.Button("Импорт…", GUILayout.Width(110)))
        {
            string src = NativeFileDialog.Open("Музыка для раскладки", "Музыка", "ogg,wav,mp3");
            if (src != null) ImportMusic(src);
        }
        GUI.enabled = musicFiles.Count > 0;
        if (GUILayout.Button("Другая", GUILayout.Width(100)))
        {
            int i = musicFiles.IndexOf(chart.music);
            chart.music = musicFiles[(i + 1) % musicFiles.Count];
            LoadMusic();
        }
        GUI.enabled = true;
        if (GUILayout.Button("Без музыки", GUILayout.Width(120)))
        {
            chart.music = "";
            LoadMusic();
        }
        GUILayout.Label("Сдвиг, с:", GUILayout.Width(85));
        chart.offset = Number("offset", chart.offset, 70);
        GUILayout.EndHorizontal();
    }

    static string Short(string text, int max) => text.Length <= max ? text : "…" + text.Substring(text.Length - max + 1);

    void DrawTransportRow()
    {
        GUILayout.BeginHorizontal();
        if (GUILayout.Button(playing ? "|| Пауза" : "► Играть", GUILayout.Width(120))) TogglePlay();
        GUILayout.Label($"{head:0.00} / {Duration:0.00} с", GUILayout.Width(170));
        if (GUILayout.Button("−5", GUILayout.Width(44))) Seek(head - 5f);
        if (GUILayout.Button("−1", GUILayout.Width(44))) Seek(head - 1f);
        if (GUILayout.Button("+1", GUILayout.Width(44))) Seek(head + 1f);
        if (GUILayout.Button("+5", GUILayout.Width(44))) Seek(head + 5f);
        GUILayout.Space(10);
        GUILayout.Label("Масштаб:", GUILayout.Width(90));
        if (GUILayout.Button("−", GUILayout.Width(36))) viewLength = Mathf.Min(120f, viewLength * 1.5f);
        if (GUILayout.Button("+", GUILayout.Width(36))) viewLength = Mathf.Max(2f, viewLength / 1.5f);
        GUILayout.Space(10);
        GUILayout.Label("BPM:", GUILayout.Width(50));
        bpm = Number("bpm", bpm, 60);
        GUILayout.Label("1-й бит, с:", GUILayout.Width(100));
        firstBeat = Number("firstBeat", firstBeat, 60);
        snap = GUILayout.Toggle(snap, "к сетке", GUILayout.Width(95));
        GUILayout.Space(10);
        GUILayout.Label("Новый:", GUILayout.Width(65));
        int t = System.Array.IndexOf(TypeIds, newType);
        newType = TypeIds[GUILayout.Toolbar(t, TypeNames, GUILayout.Width(400))];
        if (GUILayout.Button("Отметить бит (B)", GUILayout.Width(170))) Changed(() => AddAt(head));
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        GUILayout.Label("ПРОБЕЛ — играть/пауза · B — чек туда, где музыка · ← → — перемотка · Delete — удалить чек · Ctrl+S — сохранить",
            GUILayout.Width(1240));
        if (GUILayout.Button("Проверить отсюда", GUILayout.Width(190))) Preview(false);
        if (GUILayout.Button("Проверить с начала", GUILayout.Width(200))) Preview(true);
        GUILayout.EndHorizontal();
    }

    float X(float t) => Timeline.x + (t - viewStart) / viewLength * Timeline.width;
    float T(float x) => viewStart + (x - Timeline.x) / Timeline.width * viewLength;

    static void Fill(Rect r, Color c)
    {
        GUI.DrawTexture(r, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, c, 0f, 0f);
    }

    // Поменялось, сколько всего рисуется (выбрали чек, добавили, удалили…): IMGUI бросает этот проход
    // и рисует заново, иначе он путается в раскладке окна
    static void Relayout() => GUIUtility.ExitGUI();

    static void Changed(System.Action action)
    {
        action();
        GUIUtility.keyboardControl = 0;
        Relayout();
    }

    void DrawTimeline()
    {
        var e = Event.current;
        viewStart = Mathf.Clamp(viewStart, 0f, Mathf.Max(0f, Duration - viewLength * 0.5f));
        Fill(Timeline, new Color(0.03f, 0.03f, 0.05f));

        // Волна звука (перерисовывается, только когда сдвинули или растянули полосу)
        if (peaks != null)
        {
            if (wave == null) wave = new Texture2D(WaveWidth, WaveHeight, TextureFormat.RGBA32, false);
            if (!Mathf.Approximately(waveStart, viewStart) || !Mathf.Approximately(waveLength, viewLength) ||
                !Mathf.Approximately(waveOffset, chart.offset))
                RedrawWave();
            GUI.DrawTexture(Timeline, wave);
        }

        // Сетка битов: каждый 4-й ярче
        if (bpm > 0f)
        {
            float step = 60f / bpm;
            int first = Mathf.CeilToInt((viewStart - firstBeat) / step);
            for (int i = first; firstBeat + i * step <= viewStart + viewLength; i++)
            {
                float x = X(firstBeat + i * step);
                Fill(new Rect(x, Timeline.y, 1f, Timeline.height), new Color(1f, 1f, 1f, i % 4 == 0 ? 0.28f : 0.1f));
            }
        }

        // Секунды подписью сверху
        int every = viewLength > 40f ? 10 : viewLength > 15f ? 5 : 1;
        for (int s = Mathf.CeilToInt(viewStart / every) * every; s <= viewStart + viewLength; s += every)
            GUI.Label(new Rect(X(s) + 2f, Timeline.y - 22f, 60f, 22f), $"<size=14>{s} с</size>");

        // Чеки: полоска — полоса от начала до конца, остальные — линия на бите и метка
        foreach (var c in chart.checks)
        {
            var color = TypeColors[(int)c.Type];
            bool sel = c == selected;
            if (c.Type == MogCheckType.Bar)
            {
                float x0 = X(c.start), x1 = X(c.BarEnd);
                Fill(new Rect(x0, Timeline.y + 150f, Mathf.Max(2f, x1 - x0), 40f), new Color(color.r, color.g, color.b, sel ? 0.7f : 0.4f));
                foreach (float z in ZoneTimes(c)) Fill(new Rect(X(z) - 1f, Timeline.y + 140f, 3f, 60f), Color.white);
            }
            else
            {
                float x0 = X(c.StartTime), x = X(c.hit);
                Fill(new Rect(x0, Timeline.y + 200f, Mathf.Max(1f, x - x0), 6f), new Color(color.r, color.g, color.b, 0.5f));
                Fill(new Rect(x - (sel ? 2f : 1f), Timeline.y + 30f, sel ? 4f : 2f, Timeline.height - 30f), color);
                Fill(new Rect(x - 9f, Timeline.y + 10f, 18f, 18f), sel ? Color.white : color);
            }
        }

        // Где сейчас время
        Fill(new Rect(X(head) - 1f, Timeline.y - 4f, 2f, Timeline.height + 8f), new Color(1f, 0.3f, 0.25f));

        // Полоса прокрутки
        viewStart = GUI.HorizontalScrollbar(new Rect(Timeline.x, Timeline.yMax + 6f, Timeline.width, 20f),
            viewStart, viewLength, 0f, Mathf.Max(viewLength, Duration));

        // Мышь
        if (e.type == EventType.ScrollWheel && Timeline.Contains(e.mousePosition))
        {
            float at = T(e.mousePosition.x);
            viewLength = Mathf.Clamp(viewLength * (e.delta.y > 0f ? 1.25f : 0.8f), 2f, 120f);
            viewStart = Mathf.Max(0f, at - (e.mousePosition.x - Timeline.x) / Timeline.width * viewLength);
            e.Use();
        }
        else if (e.type == EventType.MouseDown && e.button == 0 && Timeline.Contains(e.mousePosition))
        {
            var hit = CheckAt(e.mousePosition);
            if (hit != null)
            {
                dragging = true;
                dragFrom = T(e.mousePosition.x) - (hit.Type == MogCheckType.Bar ? hit.start : hit.hit);
            }
            else Seek(T(e.mousePosition.x));
            e.Use();
            if (hit != selected) Changed(() => selected = hit ?? selected);
            GUIUtility.keyboardControl = 0;
        }
        else if (e.type == EventType.MouseDrag && dragging && selected != null)
        {
            MoveTo(selected, Mathf.Max(0f, Snapped(T(e.mousePosition.x) - dragFrom)));
            e.Use();
        }
        else if (e.type == EventType.MouseUp && dragging)
        {
            dragging = false;
            Sort();
            e.Use();
        }
    }

    MogCheck CheckAt(Vector2 mouse)
    {
        foreach (var c in chart.checks.Reverse())
        {
            if (c.Type == MogCheckType.Bar)
            {
                if (mouse.y >= Timeline.y + 140f && mouse.y <= Timeline.y + 200f && mouse.x >= X(c.start) && mouse.x <= X(c.BarEnd)) return c;
            }
            else if (Mathf.Abs(mouse.x - X(c.hit)) <= 8f && mouse.y < Timeline.y + 140f) return c;
        }
        return null;
    }

    // Полоска: когда метка проходит центр зоны — их удобно ставить на бит
    static List<float> ZoneTimes(MogCheck c)
    {
        var times = new List<float>();
        float prev = c.BarPos(c.start) - c.pos;
        for (float t = c.start + 0.01f; t <= c.BarEnd; t += 0.01f)
        {
            float d = c.BarPos(t) - c.pos;
            if (prev < 0f != d < 0f) times.Add(t);
            prev = d;
        }
        return times;
    }

    void RedrawWave()
    {
        waveStart = viewStart;
        waveLength = viewLength;
        waveOffset = chart.offset;
        var px = new Color32[WaveWidth * WaveHeight];
        var color = new Color32(90, 160, 220, 255);
        int mid = WaveHeight / 2;
        for (int x = 0; x < WaveWidth; x++)
        {
            float t0 = viewStart + (float)x / WaveWidth * viewLength + chart.offset;
            float t1 = viewStart + (float)(x + 1) / WaveWidth * viewLength + chart.offset;
            int i0 = Mathf.Max(0, (int)(t0 * PeaksPerSecond)), i1 = Mathf.Min(peaks.Length - 1, (int)(t1 * PeaksPerSecond));
            float a = 0f;
            for (int i = i0; i <= i1; i++) a = Mathf.Max(a, peaks[i]);
            int h = Mathf.RoundToInt(a * (mid - 4));
            for (int y = mid - h; y <= mid + h; y++) px[y * WaveWidth + x] = color;
        }
        wave.SetPixels32(px);
        wave.Apply();
    }

    // Посередине внизу — само окно скилл-чека (MogBattle на своём канвасе); тут рамка, подсказка и мышь
    void DrawPreview()
    {
        var r = PreviewRect;
        var line = new Color(1f, 1f, 1f, 0.15f);
        Fill(new Rect(r.x, r.y, r.width, 2f), line);
        Fill(new Rect(r.x, r.yMax - 2f, r.width, 2f), line);
        Fill(new Rect(r.x, r.y, 2f, r.height), line);
        Fill(new Rect(r.xMax - 2f, r.y, 2f, r.height), line);
        if (shown == null)
            GUI.Label(new Rect(r.x + 40f, r.center.y - 15f, r.width - 80f, 30f), "Выбери чек — он появится здесь, и его можно будет править мышью");

        bool editable = selected != null && shown == selected && !playing;
        string hint = playing ? "Идёт музыка: здесь то, что сейчас на экране у игрока"
            : !editable ? ""
            : selected.Type == MogCheckType.Circle ? "ЛКМ по кругу — где зона, ПКМ — ширина зоны. Стрелка — где она в момент красной линии"
            : selected.Type == MogCheckType.Bar ? "ЛКМ по полоске — где зона, ПКМ — ширина зоны. Метка — где она в момент красной линии"
            : selected.Type == MogCheckType.Arrow ? "Тяни мышью — где стрелка в окне. Кольцо — сколько времени на взмах осталось"
            : "Тяни мышью — где кнопка в окне. Кольцо — каким оно будет в момент красной линии";
        GUI.Label(new Rect(r.x, r.yMax + 6f, r.width, 60f), $"<color=#c0b0e0>{hint}</color>");

        var e = Event.current;
        if (e.type == EventType.MouseDown && editable && (e.button == 0 || e.button == 1) && r.Contains(e.mousePosition))
        {
            previewDrag = e.button + 1;
            DragPreview(e.mousePosition);
            GUIUtility.keyboardControl = 0;
            e.Use();
        }
        else if (e.type == EventType.MouseDrag && previewDrag > 0 && selected != null)
        {
            DragPreview(e.mousePosition);
            e.Use();
        }
        else if (e.type == EventType.MouseUp && previewDrag > 0)
        {
            previewDrag = 0;
            Sort();
            e.Use();
        }
    }

    void DrawList()
    {
        GUILayout.Label($"<b>Чеки: {chart.checks.Length}</b>   (клик — выбрать)");
        listScroll = GUILayout.BeginScrollView(listScroll, GUILayout.Height(530));
        for (int i = 0; i < chart.checks.Length; i++)
        {
            var c = chart.checks[i];
            var old = GUI.color;
            GUI.color = c == selected ? Color.white : Color.Lerp(TypeColors[(int)c.Type], Color.white, 0.4f);
            string label = $"{i + 1}. {TypeNames[(int)c.Type]}  {(c.Type == MogCheckType.Bar ? $"{c.start:0.00}–{c.BarEnd:0.00}" : $"бит {c.hit:0.00}")} с";
            if (GUILayout.Button(label, GUILayout.Height(30)))
                Changed(() =>
                {
                    selected = c;
                    Seek(Mathf.Max(0f, c.StartTime - 1f));
                    viewStart = Mathf.Max(0f, c.StartTime - viewLength * 0.2f);
                });
            GUI.color = old;
        }
        GUILayout.EndScrollView();
    }

    void DrawInspector()
    {
        if (selected == null)
        {
            GUILayout.Label("<b>Выбери чек</b> на полосе или в списке —");
            GUILayout.Label("или поставь новый: B (там, где сейчас музыка).");
            GUILayout.Space(10);
            GUILayout.Label("<b>Кружок:</b> стрелка бежит по кругу от верха,");
            GUILayout.Label("в бит она в центре зоны — ПРОБЕЛ.");
            GUILayout.Label("<b>Кнопка:</b> кольцо сжимается и в бит смыкается");
            GUILayout.Label("с обводкой кнопки — клик по ней.");
            GUILayout.Label("<b>Стрелка:</b> ждёт взмаха мышью, пока кольцо");
            GUILayout.Label("не сомкнётся; не успел — промах.");
            GUILayout.Label("<b>Полоска:</b> метка едет от левого края, скорость");
            GUILayout.Label("меняется по ходу, у краёв отскакивает.");
            return;
        }
        var c = selected;
        inspectorScroll = GUILayout.BeginScrollView(inspectorScroll, GUILayout.Height(570));
        GUILayout.BeginHorizontal();
        GUILayout.Label("<b>Вид:</b>", GUILayout.Width(60));
        int type = GUILayout.Toolbar((int)c.Type, TypeNames, GUILayout.Width(440));
        if (type != (int)c.Type) Changed(() => ChangeType(c, TypeIds[type]));
        GUILayout.EndHorizontal();
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Копия на +1 бит", GUILayout.Width(170))) Changed(() => Duplicate(c));
        if (GUILayout.Button("Удалить", GUILayout.Width(110))) Changed(() => Delete(c));
        GUILayout.EndHorizontal();

        if (c.Type != MogCheckType.Bar)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(c.Type == MogCheckType.Arrow ? "Махнуть до, с" : "Бит, с", GUILayout.Width(230));
            c.hit = Mathf.Max(0f, Number("hit", c.hit, 90));
            if (GUILayout.Button("сюда (где музыка)", GUILayout.Width(190))) c.hit = Snapped(head);
            GUILayout.EndHorizontal();
        }

        switch (c.Type)
        {
            case MogCheckType.Circle:
                c.pos = Slider("Где зона (доля круга)", "pos", c.pos, 0.05f, 1f);
                c.zone = Slider("Ширина зоны", "zone", c.zone, 0.03f, 0.5f);
                c.speed = Slider("Скорость, оборотов/с", "speed", c.Speed, 0.1f, 3f);
                GUILayout.Label($"Стрелка стартует сверху в {c.StartTime:0.00} с — за {c.hit - c.StartTime:0.00} с до бита.");
                break;
            case MogCheckType.Button:
            case MogCheckType.Arrow:
                if (c.Type == MogCheckType.Arrow)
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.Label("Куда провести", GUILayout.Width(230));
                    int d = GUILayout.Toolbar((int)c.Direction, DirNames, GUILayout.Width(260));
                    c.dir = DirIds[d];
                    GUILayout.EndHorizontal();
                }
                c.approach = Slider(c.Type == MogCheckType.Arrow ? "Время на взмах, с" : "Кольцо сжимается, с",
                    "approach", c.approach, 0.2f, 3f);
                c.x = Slider("Где по горизонтали", "x", c.x, 0f, 1f);
                c.y = Slider("Где по вертикали", "y", c.y, 0f, 1f);
                break;
            default:
                GUILayout.BeginHorizontal();
                GUILayout.Label("Появится, с", GUILayout.Width(230));
                c.start = Mathf.Max(0f, Number("start", c.start, 90));
                if (GUILayout.Button("сюда", GUILayout.Width(80))) c.start = Snapped(head);
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();
                GUILayout.Label("Пропадёт, с", GUILayout.Width(230));
                c.end = Number("end", c.BarEnd, 90);
                if (GUILayout.Button("сюда", GUILayout.Width(80))) c.end = Snapped(head);
                GUILayout.EndHorizontal();
                c.pos = Slider("Центр зоны (0–1)", "pos", c.pos, 0.02f, 1f);
                c.zone = Slider("Ширина зоны", "zone", c.zone, 0.03f, 0.5f);
                c.speed = Slider("Скорость (минус — назад)", "speed", c.Speed, -3f, 3f);
                if (Mathf.Abs(c.speed) < 0.05f) c.speed = 0.05f;
                GUILayout.Label("<b>Смены скорости</b> (с какой секунды — какая скорость):");
                MogSpeedKey remove = null;
                for (int i = 0; i < c.speedKeys.Length; i++)
                {
                    var k = c.speedKeys[i];
                    GUILayout.BeginHorizontal();
                    GUILayout.Label("с", GUILayout.Width(20));
                    k.at = Number("key" + i + "at", k.at, 80);
                    GUILayout.Label("с  скорость", GUILayout.Width(110));
                    k.speed = GUILayout.HorizontalSlider(k.speed, -3f, 3f, GUILayout.Width(220));
                    k.speed = Number("key" + i + "speed", k.speed, 80);
                    if (GUILayout.Button("×", GUILayout.Width(36))) remove = k;
                    GUILayout.EndHorizontal();
                }
                if (remove != null) Changed(() => c.speedKeys = c.speedKeys.Where(k => k != remove).ToArray());
                if (GUILayout.Button("+ смена скорости там, где музыка", GUILayout.Width(330)))
                    Changed(() => c.speedKeys = c.speedKeys
                        .Append(new MogSpeedKey { at = Snapped(head), speed = -c.Speed }).OrderBy(k => k.at).ToArray());
                var zt = ZoneTimes(c);
                GUILayout.Label(zt.Count == 0 ? "<color=#ff7060>Метка ни разу не проходит центр зоны</color>"
                    : "Метка в центре зоны: " + string.Join(", ", zt.Select(t => t.ToString("0.00"))) + " с");
                break;
        }
        GUILayout.EndScrollView();
    }

    void ChangeType(MogCheck c, string type)
    {
        if (c.Type == MogCheckType.Bar) c.hit = c.start + 1f; // у полоски бит — где была середина
        c.type = type;
        if (type == "bar")
        {
            c.start = Mathf.Max(0f, c.hit - 1f);
            c.end = c.hit + 1f;
            if (c.speed == 0f) c.speed = 0.5f;
        }
        if (type == "circle" && c.speed <= 0f) c.speed = 0.6f;
        if (type == "arrow" && string.IsNullOrEmpty(c.dir)) c.dir = "right";
        Sort();
    }

    void Duplicate(MogCheck c)
    {
        var copy = JsonUtility.FromJson<MogCheck>(JsonUtility.ToJson(c));
        float step = bpm > 0f ? 60f / bpm : 1f;
        MoveTo(copy, (copy.Type == MogCheckType.Bar ? copy.start : copy.hit) + step);
        chart.checks = chart.checks.Append(copy).ToArray();
        Sort();
        selected = copy;
    }

    // Ползунок и поле с числом
    float Slider(string label, string id, float value, float min, float max)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label(label, GUILayout.Width(250));
        value = GUILayout.HorizontalSlider(value, min, max, GUILayout.Width(230));
        value = Number(id, value, 80);
        GUILayout.EndHorizontal();
        return value;
    }

    // Поле с числом: пока печатаешь — держит текст как есть, точка или запятая — неважно
    float Number(string id, float value, float width)
    {
        string shownText = value.ToString("0.###", CultureInfo.InvariantCulture);
        bool focused = GUI.GetNameOfFocusedControl() == id;
        if (!focused) edits.Remove(id);
        else if (edits.TryGetValue(id, out var typing) && (!Parse(typing, out var was) || Mathf.Approximately(was, value)))
            shownText = typing; // «-», «0.» и т.п. — ещё печатают
        GUI.SetNextControlName(id);
        string next = GUILayout.TextField(shownText, GUILayout.Width(width));
        if (next == shownText) return value;
        edits[id] = next;
        return Parse(next, out var v) ? v : value;
    }

    static bool Parse(string text, out float value) =>
        float.TryParse(text.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out value);
}
