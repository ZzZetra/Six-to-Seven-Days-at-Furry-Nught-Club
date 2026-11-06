# Выгрузка фурри (Стрей, Доли, Дали) из Blender в Unity: один риг «Blocky Rig» со всеми видимыми мешами в FBX
# и текстуры его материалов.
#   blender -b "Strey.blend" --python export_furry.py -- --rig "Blocky Rig (calico full)" --name Stray
#       --fbx "Assets/Models/Stray/Stray.fbx" --textures "Assets/Models/Stray/Textures"
# Запускает export_furries.bat (там же — какой риг из файла брать).
#
# Что делает:
#   - в файлах автора по два-три варианта персонажа рядом (риги «… alt», «… (calico full)»…) — выгружает один, --rig;
#   - имена мешей без хвоста варианта: «Head (Dolie alt)» → «Head». По ним Unity находит голову (Head, Hair,
#     Piercing — для экрана смерти) и руку (Right Arm, Fingers.R — Доли в вентиляции);
#   - модификаторы (Subdivision, Bevel, нормали) применяются, скелет остаётся;
#   - материалы переименовывает в <name>Body (тело: торс, руки, ноги, пальцы), <name>Head, <name>Hair,
#     <name>Tail, <name>Piercing — по мешу, на котором материал; по этим именам Unity подставляет свои материалы;
#   - с --textures сохраняет цветную текстуру каждого материала как <материал>.png: вшитую в .blend
#     (File → External Data → Pack Resources), по пути из .blend или с тем же именем файла в Art/Furries/textures/<name>
#     (у Доли и Дали картинки называются одинаково — Handle1_diff.png…), в Art/Furries/textures или рядом с .blend.
import argparse
import os
import re
import sys

import bpy

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
ap = argparse.ArgumentParser()
ap.add_argument("--rig", required=True)
ap.add_argument("--name", required=True)
ap.add_argument("--fbx", required=True)
ap.add_argument("--textures")
args = ap.parse_args(argv)

BODY_PARTS = ("Torso", "Left Arm", "Right Arm", "Left Leg", "Right Leg", "Fingers.L", "Fingers.R")
ROLE_ORDER = ("Head", "Hair", "Tail", "Piercing")  # материал на нескольких мешах — по первому из списка
# Голова у Стрея — меш с длинным именем из генератора (tripo_node_…) — тоже «Head»
HEAD_PREFIXES = ("tripo_node",)

rig = bpy.data.objects[args.rig]
meshes = [o for o in bpy.data.objects if o.parent == rig and o.type == "MESH" and not o.hide_render]


def base_name(o):
    name = re.sub(r"\s*\(.*\)$", "", o.name)
    if name.startswith(HEAD_PREFIXES): return "Head"
    return name


renamed = {}
for o in meshes:
    renamed[o] = base_name(o)
# имена свободны: другие варианты в файле переименовываем, чтобы «Head» достался выгружаемому
for o in bpy.data.objects:
    if o not in renamed and base_name(o) in renamed.values():
        o.name = o.name + " (не выгружается)"
for o, name in renamed.items():
    o.name = name


def role_of(material):
    users = [renamed[o] for o in meshes if any(s.material == material for s in o.material_slots)]
    if any(u in BODY_PARTS for u in users): return "Body"
    for role in ROLE_ORDER:
        if role in users: return role
    return users[0].replace(" ", "").replace(".", "") if users else "Other"


materials = {s.material for o in meshes for s in o.material_slots if s.material}
names = {}
for m in sorted(materials, key=lambda m: m.name):
    new = args.name + role_of(m)
    while new in names.values(): new += "2"
    names[m] = new
for m, new in names.items():
    m.name = new

# Картинки материалов — до выгрузки FBX: с path_mode="STRIP" она портит пути картинок (у Стрея пропадали)
def base_color_image(material):
    if not material.use_nodes: return None
    for n in material.node_tree.nodes:
        if n.type == "BSDF_PRINCIPLED":
            link = next(iter(n.inputs["Base Color"].links), None)
            if link and link.from_node.type == "TEX_IMAGE": return link.from_node.image
    return next((n.image for n in material.node_tree.nodes if n.type == "TEX_IMAGE" and n.image), None)


images = {}
for m, name in names.items():
    image = base_color_image(m)
    if image is not None:
        # имя файла — после последнего слэша: os.path.basename("//calico body color.png") на Windows — «сетевой путь», пусто
        images[name] = (image, bpy.path.abspath(image.filepath), image.filepath.replace("\\", "/").split("/")[-1])

export = {rig} | set(meshes)
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
print(f"EXPORT {args.fbx} rig {rig.name!r} meshes {sorted(renamed.values())} materials {sorted(names.values())}")


if args.textures:
    here = os.path.dirname(bpy.data.filepath)
    for name in sorted(names.values()):
        if name not in images:
            print(f"NO_TEXTURE {name}: в материале нет картинки")
            continue
        image, original, file = images[name]
        path = os.path.join(args.textures, name + ".png")
        candidates = [original, os.path.join(here, "textures", args.name, file),
                      os.path.join(here, "textures", file), os.path.join(here, file)]
        source = next((f for f in candidates if f and os.path.isfile(f)), None)
        if not image.packed_file and source is None:
            print(f"NO_TEXTURE {name}: «{file}» не вшита в .blend и не лежит рядом — будет одним цветом")
            continue
        if not image.packed_file:
            image = bpy.data.images.load(source)
        os.makedirs(args.textures, exist_ok=True)
        image.pixels[0]                # Blender грузит картинку лениво — прочитать до сохранения
        image.file_format = "PNG"
        image.save(filepath=path)      # save(), не save_render(): без цветокоррекции сцены
        print(f"TEXTURE {name}.png ← {'вшитая' if image.packed_file else source}")
