using UnityEditor;
using UnityEditor.SceneManagement;

// Play в редакторе всегда запускает игру с главного меню, какая бы сцена ни была открыта.
// Исключение — сцена редактора раскладок (MogEditor): это отдельная программа, она запускается сама.
// Выключить или включить: галочка SSDFNC → Play From Main Menu.
[InitializeOnLoad]
public static class PlayFromMenu
{
    const string MenuScenePath = "Assets/Scenes/MainMenu.unity";
    const string MenuItemPath = "SSDFNC/Play From Main Menu";
    const string PrefKey = "SSDFNC.PlayFromMenu";

    // delayCall: при запуске редактора база ассетов может быть ещё не готова.
    // Открыли, сохранили или сменили сцену — проверить ещё раз, не редактор ли раскладок это
    static PlayFromMenu()
    {
        EditorApplication.delayCall += Apply;
        EditorSceneManager.sceneOpened += (s, m) => Apply();
        EditorSceneManager.sceneSaved += s => Apply();
        EditorSceneManager.activeSceneChangedInEditMode += (a, b) => Apply();
        EditorApplication.playModeStateChanged += s =>
        {
            if (s == PlayModeStateChange.ExitingEditMode) Apply();
        };
    }

    static bool Enabled
    {
        get => EditorPrefs.GetBool(PrefKey, true);
        set => EditorPrefs.SetBool(PrefKey, value);
    }

    [MenuItem(MenuItemPath)]
    static void Toggle()
    {
        Enabled = !Enabled;
        Apply();
    }

    [MenuItem(MenuItemPath, true)]
    static bool ToggleValidate()
    {
        Menu.SetChecked(MenuItemPath, Enabled);
        return true;
    }

    static void Apply()
    {
        bool chartEditor = EditorSceneManager.GetActiveScene().path == MogEditorBuilder.ScenePath;
        EditorSceneManager.playModeStartScene = Enabled && !chartEditor
            ? AssetDatabase.LoadAssetAtPath<SceneAsset>(MenuScenePath)
            : null;
    }
}
