using UnityEditor;

// Импорт Новы из Assets/Models/Nova (выгружает Tools/Blender/export_nova.bat): оси Blender → Unity
// запекаются в модель, камер и света нет. Анимаций пока нет — модель стоит в позе из Blender; скелет
// сохранён, чтобы потом добавить анимации. Текстура тела, если появится, — обычная цветная (sRGB).
class NovaModelImport : AssetPostprocessor
{
    public const string Folder = "Assets/Models/Nova/";

    public override uint GetVersion() => 2;

    void OnPreprocessModel()
    {
        if (!assetPath.StartsWith(Folder)) return;
        var mi = (ModelImporter)assetImporter;
        mi.bakeAxisConversion = true;
        mi.importCameras = false;
        mi.importLights = false;
        mi.importVisibility = false; // иначе голова приходит выключенной: выгружаются и так только видимые части
        mi.importAnimation = false;
        mi.animationType = ModelImporterAnimationType.None;
    }
}
