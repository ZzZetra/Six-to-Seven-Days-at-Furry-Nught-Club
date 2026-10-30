# Выгрузка Новы из Blender в Unity: модель со скелетом (риг «Blocky Rig») в FBX и, если есть, текстура тела.
#   blender -b "nova wip.blend" --python export_nova.py -- --fbx "Assets/Models/Nova/Nova.fbx" [--textures "папка"]
# Запускает export_nova.bat.
#
# Что делает:
#   - выгружает риг и только видимые меши под ним (скрытые запасные головы Man Head / Woman Head и формы
#     костей из коллекции Shapes не попадают);
#   - модификаторы (сглаживание Subdivision, нормали) применяются, скелет остаётся — модель можно будет
#     анимировать;
#   - материалы переименовывает: NovaBody (тело, текстура Furrypetfurry2_diff), NovaFur (мех головы),
#     NovaBlack и NovaRed (детали лица) — по этим именам Unity подставляет свои материалы;
#   - с --textures сохраняет текстуру тела как Nova_Body.png, если она вшита в .blend или лежит рядом
#     (File → External Data → Pack Resources в Blender, чтобы вшить).
import argparse
import os
import sys

import bpy

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
ap = argparse.ArgumentParser()
ap.add_argument("--fbx", required=True)
ap.add_argument("--textures")
args = ap.parse_args(argv)

RIG = "Blocky Rig"
MATERIAL_NAMES = {"Starter Blocky Rig": "NovaBody", "Material": "NovaFur",
                  "Material.005": "NovaBlack", "Material.006": "NovaRed"}

for m in bpy.data.materials:
    if m.name in MATERIAL_NAMES:
        m.name = MATERIAL_NAMES[m.name]

rig = bpy.data.objects[RIG]
export = {rig} | {o for o in bpy.data.objects if o.parent == rig and o.type == "MESH" and o.visible_get()}
for o in bpy.context.view_layer.objects:
    o.select_set(o in export)
bpy.context.view_layer.objects.active = rig

os.makedirs(os.path.dirname(os.path.abspath(args.fbx)), exist_ok=True)
bpy.ops.export_scene.fbx(
    filepath=os.path.abspath(args.fbx),
    use_selection=True,
    object_types={"ARMATURE", "MESH"},
    apply_scale_options="FBX_SCALE_ALL",
    axis_forward="-Z",
    axis_up="Y",
    use_mesh_modifiers=True,
    mesh_smooth_type="OFF",
    add_leaf_bones=False,
    primary_bone_axis="Y",
    secondary_bone_axis="X",
    use_armature_deform_only=False,
    bake_anim=False,
    path_mode="STRIP",
    embed_textures=False,
)
print(f"EXPORT {args.fbx} objects {sorted(o.name for o in export)}")

if args.textures:
    body = bpy.data.images.get("Furrypetfurry2_diff.png")
    if body is not None and (body.packed_file or os.path.exists(bpy.path.abspath(body.filepath))):
        os.makedirs(args.textures, exist_ok=True)
        path = os.path.join(args.textures, "Nova_Body.png")
        if body.packed_file:
            with open(path, "wb") as f:
                f.write(body.packed_file.data)
        else:
            body.save_render(path)
        print("TEXTURE Nova_Body.png")
    else:
        print("NO_TEXTURE Furrypetfurry2_diff.png не вшита в .blend и не найдена — тело будет одним цветом")
