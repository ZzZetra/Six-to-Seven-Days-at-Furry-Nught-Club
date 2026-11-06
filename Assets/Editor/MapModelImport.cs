using UnityEditor;
using UnityEngine;

// Импорт карты клуба из Assets/Models/Map (выгружает Tools/Blender/export_map.bat):
//   - оси Blender → Unity запекаются в модель (Bake Axis Conversion), камер, света и анимаций нет;
//   - коллайдеров нет: стены не должны перехватывать клики по кнопкам и рычагу;
//   - материалы собирает сборщик офиса (MapBuilder) по map_materials.json, из FBX они не нужны;
//   - текстуры не больше 2048; нормали — Normal map, металл и гладкость (_MS) — без sRGB.
class MapModelImport : AssetPostprocessor
{
    public const string Folder = "Assets/Models/Map/";

    public override uint GetVersion() => 1;

    bool Ours => assetPath.StartsWith(Folder);

    void OnPreprocessModel()
    {
        if (!Ours) return;
        var mi = (ModelImporter)assetImporter;
        mi.bakeAxisConversion = true;
        mi.importCameras = false;
        mi.importLights = false;
        mi.importAnimation = false;
        mi.animationType = ModelImporterAnimationType.None;
        mi.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription; // имена слотов нужны сборщику
        mi.addCollider = false;
        mi.importVisibility = false;
        mi.preserveHierarchy = true;
        mi.indexFormat = ModelImporterIndexFormat.Auto;
    }

    void OnPreprocessTexture()
    {
        if (!Ours) return;
        var ti = (TextureImporter)assetImporter;
        ti.maxTextureSize = 2048;
        if (assetPath.Contains("_Normal")) ti.textureType = TextureImporterType.NormalMap;
        if (assetPath.Contains("_MS")) ti.sRGBTexture = false;
    }
}
