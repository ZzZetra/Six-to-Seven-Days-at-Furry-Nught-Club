using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

// Карта клуба в сцене офиса: Assets/Models/Map/Map.fbx (выгружает Tools/Blender/export_map.bat).
//   - модель ставится так, чтобы кресло ресепшена (Marker_Seat) оказалось под игроком;
//   - материалы URP собираются по map_materials.json в Assets/Models/Map/Materials (при каждой сборке
//     заново: правь .blend и выгружай, а не материалы в Unity);
//   - свет: лампы с ролями (Lamps) под OfficeLights — от генератора, мигают, гаснут; фон слабый, углы тёмные;
//     неоновый вариант — светящиеся полосы вдоль всех краёв потолка и маленькие лампы по их длине (Neon…; комнаты
//     ищутся лучами от ламп);
//   - сборщику офиса отдаются точки карты (Layout): где генератор, шторка рольставня, плитки дэнс пола,
//     экран монитора на столе.
// Нет FBX — сборщик офиса строит старый полигон из примитивов.
static class MapBuilder
{
    public const string FbxPath = MapModelImport.Folder + "Map.fbx";
    const string ManifestPath = MapModelImport.Folder + "map_materials.json";
    const string TextureFolder = MapModelImport.Folder + "Textures/";
    const float GlassAlpha = 0.25f; // стекло без текстуры прозрачности

    // Лампы: точка в координатах Blender (x, y, z), дальность, яркость, цвет, кто это (OfficeLights.Role).
    // Дальность небольшая — углы комнат остаются тёмными; коридор с дэнс полом еле виден
    static readonly (Vector3 blender, float range, float intensity, Color color, OfficeLights.Role role)[] Lamps =
    {
        (new Vector3(1.05f, 0.9f, 2.7f), 4.2f, 1.1f, new Color(1f, 0.9f, 0.78f), OfficeLights.Role.Office),
        (new Vector3(5.6f, 1.6f, 2.7f), 3.8f, 1.0f, new Color(1f, 0.82f, 0.6f), OfficeLights.Role.Generator),
        (new Vector3(1.05f, -4.6f, 2.7f), 5f, 0.28f, new Color(0.7f, 0.65f, 1f), OfficeLights.Role.Corridor),
        (new Vector3(-3.1f, 1.0f, 2.7f), 4.5f, 0.3f, new Color(0.75f, 0.8f, 1f), OfficeLights.Role.Corridor2),
        (new Vector3(5.9f, -3.5f, 2.7f), 4f, 0.25f, new Color(0.75f, 0.8f, 1f), OfficeLights.Role.Passage),
    };
    // Неон: в каких комнатах (по лампе из Lamps), цвета полос, яркость свечения и ламп в углах
    static readonly OfficeLights.Role[] NeonRooms = { OfficeLights.Role.Office, OfficeLights.Role.Corridor, OfficeLights.Role.Corridor2 };
    static readonly Color[] NeonColors = { new Color(1f, 0.12f, 0.75f), new Color(0.15f, 0.85f, 1f) }; // розовый, голубой
    const float NeonGlow = 6f;          // во сколько раз светится полоса (Bloom начинает с 1)
    const float NeonStrip = 0.06f;      // м, толщина полосы
    const float NeonLampStep = 0.75f;   // м между лампами вдоль полосы: свет идёт ровно вдоль края потолка
    const float NeonLampRange = 2.2f, NeonLampIntensity = 0.35f;
    const float NeonLampInset = 0.3f;   // м, лампа — от стены и ниже потолка (ближе — на стене пятна)
    const float NeonMaxReach = 8f;      // м, дальше стены не ищем

    public class Layout
    {
        public Transform model;        // модель карты; Unity импортирует точку Blender (x, y, z) как (x, z, y)
        public Vector3 generator;      // где стоял генератор карты, на полу
        public Transform shutter;      // шторка рольставня — дверь вправо
        public Renderer[] danceTiles;  // плитки дэнс пола: от офиса вглубь, в ряду слева направо
        public Bounds danceFloor;

        // Экран монитора на столе: на него ложится монитор камер. Нет — монитор камер висит в воздухе
        public bool hasScreen;
        public Vector3 screenCenter, screenNormal, screenUp; // нормаль — от экрана к зрителю
        public float screenWidth, screenHeight;

        // Генератор автора карты: куда смотрит пульт (по полу) и размер — наш встаёт так же. Нет — свой поворот
        public Vector3 generatorFront, generatorSize;

        // Свет карты (лампы, неон); генератор ему ставит сборщик офиса
        public OfficeLights lights;

        // Часть карты по имени (кнопки, рычаг, телефон на столе — имена из RENAME в export_map.py); нет — null
        public Transform Find(string name) => MapBuilder.Find(model.gameObject, name);

        // Границы части карты в сцене (по всем её мешам)
        public static Bounds BoundsOf(Transform t)
        {
            var renderers = t.GetComponentsInChildren<Renderer>();
            var b = renderers[0].bounds;
            foreach (var r in renderers) b.Encapsulate(r.bounds);
            return b;
        }

        // Точка из Blender (x, y, z) в сцене
        public Vector3 FromBlender(float x, float y, float z) => model.TransformPoint(x, z, y);
        public Vector3 FromBlender(float[] p) => FromBlender(p[0], p[1], p[2]);
        public Vector3 DirectionFromBlender(float[] d) => model.TransformDirection(d[0], d[2], d[1]).normalized;
    }

    [Serializable] class Manifest { public MaterialInfo[] materials; public string[] missing; public ScreenInfo monitorScreen; public GeneratorInfo generator; }

    [Serializable] class GeneratorInfo { public float[] center, front, size; }

    [Serializable] class ScreenInfo { public float[] center, normal, up; public float width, height; }

    [Serializable]
    class MaterialInfo
    {
        public string name, baseMap, normalMap, msMap;
        public float[] color, emission, tiling, offset;
        public float normalScale, metallic, smoothness;
        public bool transparent;
    }

    public static Layout Build(Transform parent)
    {
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath);
        if (asset == null) return null;

        var root = new GameObject("Map").transform;
        root.SetParent(parent, false);
        // Обычная копия, не префаб: шторку сборщик переносит под дверь
        var model = (GameObject)Object.Instantiate(asset, root);
        model.name = "MapModel";

        // В Blender из офиса смотрят на −Y (там дэнс пол), в игре вперёд — +Z: разворот на 180°.
        // Потом сдвиг, чтобы кресло оказалось под игроком
        root.rotation = Quaternion.Euler(0f, 180f, 0f);
        var seat = Find(model, "Marker_Seat");
        if (seat != null) root.position = -new Vector3(seat.position.x, 0f, seat.position.z);

        var manifest = JsonUtility.FromJson<Manifest>(
            AssetDatabase.LoadAssetAtPath<TextAsset>(ManifestPath)?.text ?? "{}");
        var materials = (manifest.materials ?? new MaterialInfo[0]).ToDictionary(i => i.name, MapMaterial);
        var tileMaterial = TestPlatformBuilder.ModelMaterial(MapModelImport.Folder, "Map_DanceTile", m =>
        {
            m.SetColor("_BaseColor", new Color(0.1f, 0.1f, 0.12f)); // цвет даёт DanceFloor
            m.SetFloat("_Smoothness", 0.8f);
        });
        foreach (var r in model.GetComponentsInChildren<Renderer>())
        {
            // Тени с обеих сторон: потолок смотрит вниз, и без этого солнце светит сквозь него
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.TwoSided;
            if (r.name.StartsWith("DanceTile_")) { r.sharedMaterial = tileMaterial; continue; }
            var list = r.sharedMaterials;
            for (int i = 0; i < list.Length; i++)
                if (list[i] != null && materials.TryGetValue(list[i].name, out var m)) list[i] = m;
            r.sharedMaterials = list;
        }

        var layout = new Layout { model = model.transform };
        var gen = Find(model, "Marker_Generator");
        layout.generator = gen != null ? new Vector3(gen.position.x, 0f, gen.position.z) : Vector3.zero;
        layout.shutter = Find(model, "ShutterCurtain");
        layout.danceTiles = model.GetComponentsInChildren<Renderer>()
            .Where(r => r.name.StartsWith("DanceTile_")).OrderBy(r => r.name, StringComparer.Ordinal).ToArray();
        if (layout.danceTiles.Length > 0)
        {
            var b = layout.danceTiles[0].bounds;
            foreach (var t in layout.danceTiles) b.Encapsulate(t.bounds);
            layout.danceFloor = b;
        }
        var gi = manifest.generator;
        if (gi?.front?.Length == 3 && gi.size?.Length == 3)
        {
            layout.generatorFront = layout.DirectionFromBlender(gi.front);
            layout.generatorSize = new Vector3(gi.size[0], gi.size[2], gi.size[1]); // по осям Unity: ширина и длина — как придётся
        }
        var screen = manifest.monitorScreen;
        if (screen?.center?.Length == 3 && screen.normal?.Length == 3 && screen.up?.Length == 3 && screen.width > 0f)
        {
            layout.hasScreen = true;
            layout.screenCenter = layout.FromBlender(screen.center);
            layout.screenNormal = layout.DirectionFromBlender(screen.normal);
            layout.screenUp = layout.DirectionFromBlender(screen.up);
            layout.screenWidth = screen.width;
            layout.screenHeight = screen.height;
        }

        var lamps = new GameObject("MapLamps", typeof(OfficeLights)).transform;
        lamps.SetParent(root, false);
        var lights = new List<Light>();
        var roles = new List<OfficeLights.Role>();
        foreach (var (p, range, intensity, color, role) in Lamps)
        {
            lights.Add(NewLamp(lamps, "Lamp" + role, layout.FromBlender(p.x, p.y, p.z), range, intensity, color));
            roles.Add(role);
        }
        var neon = BuildNeon(model, lamps, layout, lights, roles);
        var office = lamps.GetComponent<OfficeLights>();
        office.lamps = lights.ToArray();
        office.roles = roles.ToArray();
        office.baseIntensity = lights.Select(l => l.intensity).ToArray();
        office.neonRoot = neon;
        // Светящиеся материалы карты (лампа на столе, вывески) гаснут вместе со светом; плитки дэнс пола — свои
        var glowing = new List<(Renderer, int, Color)>();
        foreach (var r in model.GetComponentsInChildren<Renderer>())
        {
            if (r.name.StartsWith("DanceTile_")) continue;
            var list = r.sharedMaterials;
            for (int i = 0; i < list.Length; i++)
                if (list[i] != null && list[i].IsKeywordEnabled("_EMISSION"))
                    glowing.Add((r, i, list[i].GetColor("_EmissionColor")));
        }
        foreach (var r in neon.GetComponentsInChildren<Renderer>(true)) // неоновые полосы — тоже от генератора
            glowing.Add((r, 0, r.sharedMaterial.GetColor("_EmissionColor")));
        office.glowing = glowing.Select(g => g.Item1).ToArray();
        office.glowingMaterial = glowing.Select(g => g.Item2).ToArray();
        office.glowingColor = glowing.Select(g => g.Item3).ToArray();
        layout.lights = office;

        int missing = manifest.missing?.Length ?? 0;
        Debug.Log($"[SSDFNC] Карта: {materials.Count} материалов, плиток дэнс пола {layout.danceTiles.Length}" +
                  (missing > 0 ? $", не хватает текстур: {missing} (список — map_materials.json → missing)" : ""));
        return layout;
    }

    static Light NewLamp(Transform parent, string name, Vector3 pos, float range, float intensity, Color color)
    {
        var light = new GameObject(name, typeof(Light)).GetComponent<Light>();
        light.transform.SetParent(parent, false);
        light.transform.position = pos;
        light.type = LightType.Point;
        light.range = range;
        light.intensity = intensity;
        light.color = color;
        light.shadows = LightShadows.None;
        return light;
    }

    // Неон: в каждой комнате из NeonRooms от её лампы лучами ищем потолок и четыре стены (на время — коллайдеры на
    // меши карты), по краям потолка — светящиеся полосы двух цветов, в углах — цветные лампы (OfficeLights.Role.Neon).
    // Включается в OfficeLights (меню → «Настройки» → «Свет»)
    static GameObject BuildNeon(GameObject model, Transform parent, Layout layout, List<Light> lights, List<OfficeLights.Role> roles)
    {
        var root = new GameObject("Neon").transform;
        root.SetParent(parent, false);
        var colliders = new List<MeshCollider>();
        foreach (var f in model.GetComponentsInChildren<MeshFilter>())
            if (f.sharedMesh != null && !f.name.StartsWith("DanceTile_"))
            {
                var c = f.gameObject.AddComponent<MeshCollider>();
                c.sharedMesh = f.sharedMesh;
                colliders.Add(c);
            }
        bool backfaces = Physics.queriesHitBackfaces;
        Physics.queriesHitBackfaces = true; // стены — одна плоскость, могут смотреть наружу
        Physics.SyncTransforms();
        var mats = NeonColors.Select((c, i) => TestPlatformBuilder.ModelMaterial(MapModelImport.Folder, "Map_Neon" + i, m =>
        {
            m.SetColor("_BaseColor", Color.black);
            m.SetFloat("_Smoothness", 0f);
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", c * NeonGlow);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive; // с None URP сбрасывал _EMISSION — полосы были чёрные
        })).ToArray();
        try
        {
            foreach (var room in NeonRooms)
            {
                int li = roles.IndexOf(room);
                if (li < 0) continue;
                var p = lights[li].transform.position;
                float ceiling = Physics.Raycast(p, Vector3.up, out var up, 3f) ? up.point.y : p.y + 0.3f;
                var at = new Vector3(p.x, ceiling - 0.15f, p.z);
                float Reach(Vector3 dir) => Physics.Raycast(at, dir, out var hit, NeonMaxReach) ? hit.distance : NeonMaxReach;
                float xMin = p.x - Reach(Vector3.left) + 0.04f, xMax = p.x + Reach(Vector3.right) - 0.04f;
                float zMin = p.z - Reach(Vector3.back) + 0.04f, zMax = p.z + Reach(Vector3.forward) - 0.04f;
                float y = ceiling - 0.05f;
                Strip(root, $"Neon{room}_N", new Vector3((xMin + xMax) / 2, y, zMax), new Vector3(xMax - xMin, NeonStrip, NeonStrip), mats[0]);
                Strip(root, $"Neon{room}_S", new Vector3((xMin + xMax) / 2, y, zMin), new Vector3(xMax - xMin, NeonStrip, NeonStrip), mats[0]);
                Strip(root, $"Neon{room}_W", new Vector3(xMin, y, (zMin + zMax) / 2), new Vector3(NeonStrip, NeonStrip, zMax - zMin), mats[1]);
                Strip(root, $"Neon{room}_E", new Vector3(xMax, y, (zMin + zMax) / 2), new Vector3(NeonStrip, NeonStrip, zMax - zMin), mats[1]);
                // Лампы вдоль каждой полосы, её цвета: от неё на стене и потолке — ровная цветная полоса света
                float ly = ceiling - NeonLampInset;
                void Along(Vector3 a, Vector3 b, Vector3 inward, int color, string side)
                {
                    int n = Mathf.Max(1, Mathf.RoundToInt(Vector3.Distance(a, b) / NeonLampStep));
                    for (int i = 0; i < n; i++)
                    {
                        var pos = Vector3.Lerp(a, b, (i + 0.5f) / n) + inward * NeonLampInset;
                        lights.Add(NewLamp(root, $"NeonLamp{room}{side}{i}", new Vector3(pos.x, ly, pos.z), NeonLampRange, NeonLampIntensity, NeonColors[color]));
                        roles.Add(OfficeLights.Role.Neon);
                    }
                }
                Along(new Vector3(xMin, 0, zMax), new Vector3(xMax, 0, zMax), Vector3.back, 0, "N");
                Along(new Vector3(xMin, 0, zMin), new Vector3(xMax, 0, zMin), Vector3.forward, 0, "S");
                Along(new Vector3(xMin, 0, zMin), new Vector3(xMin, 0, zMax), Vector3.right, 1, "W");
                Along(new Vector3(xMax, 0, zMin), new Vector3(xMax, 0, zMax), Vector3.left, 1, "E");
                Debug.Log($"[SSDFNC] Неон {room}: x {xMin:0.0}…{xMax:0.0}, z {zMin:0.0}…{zMax:0.0}, потолок {ceiling:0.00}");
            }
        }
        finally
        {
            Physics.queriesHitBackfaces = backfaces;
            foreach (var c in colliders) Object.DestroyImmediate(c);
        }
        return root.gameObject;
    }

    static void Strip(Transform parent, string name, Vector3 pos, Vector3 size, Material mat)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Object.DestroyImmediate(go.GetComponent<Collider>());
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.position = pos;
        go.transform.localScale = size;
        var r = go.GetComponent<Renderer>();
        r.sharedMaterial = mat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    internal static Transform Find(GameObject go, string name) =>
        go.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == name);

    // Материал URP Lit по описанию из Blender. Цвета в описании линейные — Unity ждёт sRGB
    static Material MapMaterial(MaterialInfo info) => TestPlatformBuilder.ModelMaterial(MapModelImport.Folder, info.name, m =>
    {
        Texture2D Tex(string file) => string.IsNullOrEmpty(file) ? null : AssetDatabase.LoadAssetAtPath<Texture2D>(TextureFolder + file);
        var baseMap = Tex(info.baseMap);
        var normal = Tex(info.normalMap);
        var ms = Tex(info.msMap);

        var c = Rgba(info.color, Color.white);
        var color = new Color(c.r, c.g, c.b).gamma;
        color.a = info.transparent && baseMap == null ? Mathf.Min(c.a, GlassAlpha) : c.a;
        m.SetColor("_BaseColor", color);
        m.SetTexture("_BaseMap", baseMap);
        var tiling = info.tiling?.Length >= 2 ? new Vector2(info.tiling[0], info.tiling[1]) : Vector2.one;
        var offset = info.offset?.Length >= 2 ? new Vector2(info.offset[0], info.offset[1]) : Vector2.zero;
        m.SetTextureScale("_BaseMap", tiling);
        m.SetTextureOffset("_BaseMap", offset);

        m.SetTexture("_BumpMap", normal);
        m.SetFloat("_BumpScale", info.normalScale);
        if (normal != null) m.EnableKeyword("_NORMALMAP"); else m.DisableKeyword("_NORMALMAP");

        m.SetTexture("_MetallicGlossMap", ms); // металл в RGB, гладкость в альфе
        if (ms != null)
        {
            m.EnableKeyword("_METALLICSPECGLOSSMAP");
            m.SetFloat("_Smoothness", 1f);
            m.SetFloat("_SmoothnessTextureChannel", 0f);
        }
        else
        {
            m.DisableKeyword("_METALLICSPECGLOSSMAP");
            m.SetFloat("_Metallic", info.metallic);
            m.SetFloat("_Smoothness", info.smoothness);
        }

        var e = Rgba(info.emission, Color.black);
        if (e.maxColorComponent > 0f)
        {
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", new Color(e.r, e.g, e.b));
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        }
        else m.DisableKeyword("_EMISSION");

        if (info.transparent) TestPlatformBuilder.Transparent(m);
    });

    static Color Rgba(float[] v, Color fallback)
    {
        if (v == null || v.Length < 3) return fallback;
        return new Color(v[0], v[1], v[2], v.Length > 3 ? v[3] : 1f);
    }
}
