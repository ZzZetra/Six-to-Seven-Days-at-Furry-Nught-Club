using System.IO;
using UnityEditor;
using UnityEngine;

// Импорт генератора из Assets/Models/Generator (выгружает Tools/Blender/export_generator.bat):
//   - оси Blender → Unity запекаются в модель (Bake Axis Conversion), камер и света нет;
//   - анимации Legacy: их проигрывает компонент Animation через GeneratorModel;
//   - в файлах-анимациях (Generator@*.fbx) материалы не нужны;
//   - из клипов убираются чужие каналы, иначе один клип перебивает другой:
//       Generator.fbx (рывки стартера и запуск) — всё, кроме крышки бака;
//       Generator@Idle (дрожь работающего)      — только наклон всего рига (generator_rig; Unity склеивает
//                                                 его с корнем FBX, поэтому путь у него пустой);
//       Generator@CapOpen / CapClose            — только кость крышки;
//   - текстуры: нормали — Normal map, металл и гладкость — без sRGB.
class GeneratorModelImport : AssetPostprocessor
{
    public const string Folder = "Assets/Models/Generator/";
    const string Rig = "generator_rig";
    const string CapBone = "root/fuel_cap";

    public override uint GetVersion() => 2;

    bool Ours => assetPath.StartsWith(Folder);
    string FileName => Path.GetFileNameWithoutExtension(assetPath);

    void OnPreprocessModel()
    {
        if (!Ours) return;
        var mi = (ModelImporter)assetImporter;
        mi.bakeAxisConversion = true;
        mi.importCameras = false;
        mi.importLights = false;
        mi.importAnimation = true;
        mi.animationType = ModelImporterAnimationType.Legacy;
        if (FileName.Contains("@")) mi.materialImportMode = ModelImporterMaterialImportMode.None;
    }

    void OnPostprocessAnimation(GameObject root, AnimationClip clip)
    {
        if (!Ours) return;
        string file = FileName;
        foreach (var binding in AnimationUtility.GetCurveBindings(clip))
        {
            string path = binding.path;
            bool rig = path == "" || path == Rig;
            bool cap = path == CapBone || path.EndsWith("/" + CapBone);
            bool capMesh = path.EndsWith("fuel_cap/fuel_cap");
            bool keep = file.EndsWith("@Idle") ? rig
                      : file.Contains("@Cap") ? cap
                      : !(cap || capMesh);
            if (!keep) AnimationUtility.SetEditorCurve(clip, binding, null);
        }
    }

    void OnPreprocessTexture()
    {
        if (!Ours) return;
        var ti = (TextureImporter)assetImporter;
        if (assetPath.EndsWith("_Normal.png")) ti.textureType = TextureImporterType.NormalMap;
        if (assetPath.EndsWith("_MetallicSmoothness.png")) ti.sRGBTexture = false;
        if (assetPath.EndsWith("_GlassBase.png")) ti.alphaIsTransparency = true;
    }
}
