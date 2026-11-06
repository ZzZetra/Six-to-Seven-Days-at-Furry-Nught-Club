using System.Linq;
using UnityEditor;

// Импорт моделей фурри из Assets/Models/Nova, Stray, Doli, Dali (выгружают Tools/Blender/export_nova.bat и
// export_furries.bat): оси Blender → Unity запекаются в модель, камер и света нет. Анимаций пока нет — модели стоят
// в позе из Blender; скелет сохранён, чтобы потом добавить анимации. Текстуры — обычные цветные (sRGB).
class FurryModelImport : AssetPostprocessor
{
    public static readonly string[] Folders =
        { "Assets/Models/Nova/", "Assets/Models/Stray/", "Assets/Models/Doli/", "Assets/Models/Dali/" };

    public override uint GetVersion() => 3;

    void OnPreprocessModel()
    {
        if (!Folders.Any(assetPath.StartsWith)) return;
        var mi = (ModelImporter)assetImporter;
        mi.bakeAxisConversion = true;
        mi.importCameras = false;
        mi.importLights = false;
        mi.importVisibility = false; // иначе голова приходит выключенной: выгружаются и так только видимые части
        mi.importAnimation = false;
        mi.animationType = ModelImporterAnimationType.None;
    }
}
