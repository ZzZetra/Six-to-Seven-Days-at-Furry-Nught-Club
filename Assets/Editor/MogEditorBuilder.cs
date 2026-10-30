using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// Редактор раскладок скилл-чеков — отдельная программа (в игру не входит).
//   SSDFNC → Chart Editor → Open in Play Mode — собрать сцену и запустить её прямо в Unity;
//   SSDFNC → Chart Editor → Build App (.exe) — собрать программу в Builds/ChartEditor.
// Сцена (Assets/Scenes/MogEditor.unity): камера, MogChartEditor с AudioSource для музыки и окно мог-батла
// (MogBattle) на своём канвасе — превью чека и «Проверить».
public static class MogEditorBuilder
{
    public const string ScenePath = "Assets/Scenes/MogEditor.unity";
    public const string AppName = "SSDFNC Chart Editor";
    const string AppFolder = "Builds/ChartEditor";

    [MenuItem("SSDFNC/Chart Editor/Open in Play Mode")]
    static void OpenInPlayMode()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        BuildScene();
        EditorSceneManager.playModeStartScene = null; // эту сцену, не меню (PlayFromMenu её и так пропускает)
        EditorApplication.EnterPlaymode();
    }

    [MenuItem("SSDFNC/Chart Editor/Build App (.exe)")]
    static void BuildAppFromMenu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        string scene = EditorSceneManager.GetActiveScene().path;
        bool ok = BuildApp(AppFolder, Path.Combine(Application.streamingAssetsPath, "MogCharts"));
        if (!string.IsNullOrEmpty(scene) && File.Exists(scene)) EditorSceneManager.OpenScene(scene);
        if (ok) EditorUtility.RevealInFinder(Path.Combine(AppFolder, AppName + ".exe"));
    }

    // Новая сцена, сохраняется в ScenePath (открытой остаётся она)
    public static void BuildScene()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // Фон редактора — цвет камеры: IMGUI рисует поверх, а окно скилл-чека посередине не закрыто
        var cam = new GameObject("Main Camera", typeof(Camera)).GetComponent<Camera>();
        cam.tag = "MainCamera";
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.08f, 0.06f, 0.11f);

        var go = new GameObject("MogChartEditor", typeof(MogChartEditor), typeof(AudioSource));
        var editor = go.GetComponent<MogChartEditor>();
        editor.music = go.GetComponent<AudioSource>();
        editor.music.playOnAwake = false;
        editor.music.spatialBlend = 0f;

        // Окно скилл-чеков — то же, что в атаке Новы
        var hud = new GameObject("MogPreviewHUD", typeof(Canvas), typeof(CanvasScaler), typeof(MogBattle), typeof(AudioSource));
        var canvas = hud.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = hud.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        editor.battle = MogBattleBuilder.Build(hud);

        EditorSceneManager.SaveScene(scene, ScenePath);
        Debug.Log("[SSDFNC] Chart editor scene built.");
    }

    // Программа: только сцена редактора, своё имя, окно 1600×900, которое можно тянуть. Настройки игры
    // (имя, окно) на время сборки подменяются и возвращаются. chartsFolder — папка раскладок, которую
    // откроет программа: путь записывается в её StreamingAssets (MogChartEditor.FolderFile)
    public static bool BuildApp(string outDir, string chartsFolder)
    {
        BuildScene();
        string productName = PlayerSettings.productName;
        var fullScreenMode = PlayerSettings.fullScreenMode;
        int width = PlayerSettings.defaultScreenWidth, height = PlayerSettings.defaultScreenHeight;
        bool resizable = PlayerSettings.resizableWindow;
        try
        {
            PlayerSettings.productName = AppName;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.defaultScreenWidth = 1600;
            PlayerSettings.defaultScreenHeight = 900;
            PlayerSettings.resizableWindow = true;
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = Path.Combine(outDir, AppName + ".exe"),
                target = BuildTarget.StandaloneWindows64,
            });
            if (report.summary.result != BuildResult.Succeeded)
            {
                Debug.LogError($"[SSDFNC] Chart editor app: {report.summary.result}, ошибок {report.summary.totalErrors}");
                return false;
            }
            string data = Path.Combine(outDir, AppName + "_Data", "StreamingAssets");
            Directory.CreateDirectory(data);
            File.WriteAllText(Path.Combine(data, MogChartEditor.FolderFile), Path.GetFullPath(chartsFolder));
            Debug.Log($"[SSDFNC] Chart editor app built: {Path.GetFullPath(outDir)}, раскладки — {Path.GetFullPath(chartsFolder)}");
            return true;
        }
        finally
        {
            PlayerSettings.productName = productName;
            PlayerSettings.fullScreenMode = fullScreenMode;
            PlayerSettings.defaultScreenWidth = width;
            PlayerSettings.defaultScreenHeight = height;
            PlayerSettings.resizableWindow = resizable;
        }
    }
}
