# Выгрузка генератора из Blender в Unity: модель с ригом + одна анимация в FBX, по желанию — текстуры.
#   blender -b "файл.blend" --python export_generator.py -- --fbx "Assets/Models/Generator/Generator.fbx" [--textures "папка"]
# Запускает export_generator.bat для всех четырёх файлов из Art/Generator.
#
# Что делает:
#   - выгружает риг generator_rig и всё, что к нему привязано (без камеры, света и форм костей WGT_*);
#   - анимацию «запекает» по кадрам: ограничения костей (трос тянется за ручкой, крышка) превращаются
#     в обычные ключи, которые понимает Unity;
#   - материалы переименовывает в Generator, GeneratorGlass, StarterRope — по этим именам Unity
#     подставляет свои материалы;
#   - с --textures достаёт вшитые текстуры и собирает их под URP Lit:
#       Generator_BaseMap.jpg            цвет (как есть, без пережатия)
#       Generator_GlassBase.png          цвет + прозрачность стекла в альфе
#       Generator_MetallicSmoothness.png металл в RGB, гладкость (1 − шероховатость) в альфе
#       Generator_Normal.png             нормали (OpenGL, как в Unity)
import argparse
import os
import sys

import bpy
import numpy as np

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
ap = argparse.ArgumentParser()
ap.add_argument("--fbx", required=True)
ap.add_argument("--textures")
args = ap.parse_args(argv)

RIG = "generator_rig"
MATERIAL_NAMES = {"portable_generator": "Generator", "portable_generator_glass": "GeneratorGlass", "starter_rope": "StarterRope"}

# ── Материалы: понятные имена ──
for m in bpy.data.materials:
    base = m.name.split(".")[0]
    if base in MATERIAL_NAMES:
        m.name = MATERIAL_NAMES[base]

# ── Что выгружать: риг и всё под ним ──
rig = bpy.data.objects[RIG]
export = {rig}
stack = [rig]
while stack:
    o = stack.pop()
    for c in o.children:
        export.add(c)
        stack.append(c)
for o in bpy.context.view_layer.objects:
    o.select_set(o in export)
bpy.context.view_layer.objects.active = rig

os.makedirs(os.path.dirname(os.path.abspath(args.fbx)), exist_ok=True)
sc = bpy.context.scene
bpy.ops.export_scene.fbx(
    filepath=os.path.abspath(args.fbx),
    use_selection=True,
    object_types={"ARMATURE", "MESH", "EMPTY"},
    apply_scale_options="FBX_SCALE_ALL",
    axis_forward="-Z",
    axis_up="Y",
    use_mesh_modifiers=True,
    mesh_smooth_type="OFF",
    add_leaf_bones=False,
    primary_bone_axis="Y",
    secondary_bone_axis="X",
    use_armature_deform_only=False,
    bake_anim=True,
    bake_anim_use_all_bones=True,
    bake_anim_use_nla_strips=False,
    bake_anim_use_all_actions=False,
    bake_anim_force_startend_keying=True,
    bake_anim_step=1.0,
    bake_anim_simplify_factor=0.0,  # дрожь генератора — сотые доли радиана, упрощение её съест
    path_mode="STRIP",
    embed_textures=False,
)
print(f"EXPORT {args.fbx} frames {sc.frame_start}..{sc.frame_end} @ {sc.render.fps} fps, objects {sorted(o.name for o in export)}")

# ── Текстуры ──
if args.textures:
    os.makedirs(args.textures, exist_ok=True)

    def image(key):
        return next(im for im in bpy.data.images if im.name.startswith(key) and im.packed_file)

    def pixels(im):
        w, h = im.size
        return np.array(im.pixels[:], dtype=np.float32).reshape(h, w, 4)

    def save(name, rgba):
        h, w, _ = rgba.shape
        out = bpy.data.images.new(name, w, h, alpha=True)
        out.colorspace_settings.name = "Non-Color"  # без пересчёта цвета: что посчитали, то и записали
        out.pixels.foreach_set(np.clip(rgba, 0.0, 1.0).astype(np.float32).ravel())
        out.filepath_raw = os.path.join(args.textures, name)
        out.file_format = "PNG"
        out.save()
        print("TEXTURE", name)

    diff = image("portable_generator_diff")
    with open(os.path.join(args.textures, "Generator_BaseMap.jpg"), "wb") as f:
        f.write(diff.packed_file.data)  # вшитый JPEG как есть
    print("TEXTURE Generator_BaseMap.jpg")

    base = pixels(diff)
    alpha = pixels(image("portable_generator_alpha"))
    glass = base.copy()
    glass[..., 3] = alpha[..., 0]
    save("Generator_GlassBase.png", glass)

    metal = pixels(image("portable_generator_metal"))
    rough = pixels(image("portable_generator_rough"))
    ms = np.empty_like(metal)
    ms[..., 0:3] = metal[..., 0:1]
    ms[..., 3] = 1.0 - rough[..., 0]
    save("Generator_MetallicSmoothness.png", ms)

    normal = pixels(image("portable_generator_nor_gl"))
    normal[..., 3] = 1.0
    save("Generator_Normal.png", normal)
