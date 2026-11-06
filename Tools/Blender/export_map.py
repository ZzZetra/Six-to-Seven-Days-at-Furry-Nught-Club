# Выгрузка карты клуба из Blender в Unity: статичные меши в один FBX, текстуры и описание материалов.
#   blender -b "Art/Map/map 67dfn wip.blend" --python export_map.py -- --out "Assets/Models/Map"
# Запускает export_map.bat.
#
# Что делает:
#   - берёт все меши, которые видны в рендере (и скрытые глазком во вьюпорте), кроме камеры, плана здания (Building Plan), форм костей wdg_* и генератора
#     (в игре свой генератор с анимациями — он встаёт на место генератора из карты, см. маркер ниже);
#   - модификаторы, риги и геометрические ноды применяются: в FBX уходят обычные меши на своих местах;
#   - дверь в генераторную открывается (поворот вокруг петли), если в .blend она закрыта: к генератору ходят через неё;
#   - дэнс пол режется на отдельные плитки DanceTile_000…: игра красит их по одной;
#   - шторка рольставня отдельным объектом ShutterCurtain: это дверь вправо, игра поднимает и опускает её;
#   - кнопки, рычаг и телефон на столе получают имена DanceButton(Cap), SoundButton(Cap), DoorLeverHandle, DeskPhone
#     (см. RENAME): по ним игра делает их настоящими кнопками, рычагом двери и телефоном;
#   - каждая грань потолка смотрит вниз, пола — вверх, шторка двусторонняя (в Unity у плоскостей видна
#     одна сторона); открытые «листовые» меши (воздуховод, трубы, метла… — где открытых рёбер ≥ OPEN_SHEET) тоже
#     двусторонние: Blender рисует и изнанку, а Unity — нет, и в открытый конец было видно насквозь;
#     многоугольники больше четырёх углов режутся на треугольники;
#   - очень тяжёлые меши (клавиши клавиатуры, 235 тыс. треугольников) упрощаются;
#   - маркеры (пустые объекты): Marker_Generator — где стоит генератор, Marker_Seat — кресло игрока;
#   - где экран монитора на столе (центр, нормаль, размер) — в map_materials.json → monitorScreen;
#   - генератор автора: куда смотрит его пульт и какого он размера — в map_materials.json → generator;
#   - материалы получают имена Map_…, их описание (цвет, текстуры, металл, гладкость, свечение, тайлинг)
#     пишется в map_materials.json — по нему Unity собирает материалы URP;
#   - текстуры: вшитые в .blend, найденные по пути из .blend или лежащие рядом с .blend (в папке textures)
#     копируются в Textures; металл и шероховатость склеиваются в <материал>_MS.png (металл в RGB,
#     гладкость в альфе, как у URP Lit). Ненайденные перечисляются в map_materials.json → missing.
import argparse
import hashlib
import json
import math
import os
import re
import shutil
import sys

import bmesh
import bpy
import numpy as np
from mathutils import Matrix, Vector

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
ap = argparse.ArgumentParser()
ap.add_argument("--out", required=True)
args = ap.parse_args(argv)

OUT = os.path.abspath(args.out)
TEX = os.path.join(OUT, "Textures")
os.makedirs(TEX, exist_ok=True)
for _old in os.listdir(TEX):  # папка целиком собирается заново (.meta оставляем — Unity сам уберёт лишние)
    if not _old.endswith(".meta"):
        os.remove(os.path.join(TEX, _old))
BLEND_DIR = os.path.dirname(bpy.data.filepath)

SKIP_NAMES = {"Building Plan", "portable_generator"}
SKIP_PREFIX = ("wdg_",)
GENERATOR_EMPTY = "Portable Generator"
SEAT = "mid_century_lounge_chair"
DISCO = "disco floor"
DOOR_PARTS = ("Gen Door", "Gen Door Handle")
DOOR_RIG = "Gen Door Rig"
DOOR_OPEN_ANGLE = -90.0          # градусов вокруг петли: створка уходит внутрь генераторной, вдоль стены
SHUTTER = "rollershutter_window_02 .001"
RENAME = {SHUTTER: "ShutterCurtain", "rollershutter_window_02 ": "ShutterFrame",
          # органы управления на столе ресепшена (имя в .blend → в игре; переименовал в Blender — поправь тут):
          # розовая кнопка — дэнс пол, красная — звук (корпус и шляпка), рычаг под столом — дверь, телефон
          "Cylinder.001": "DanceButton", "Cylinder.002": "DanceButtonCap",
          "Cylinder": "SoundButton", "Cylinder.003": "SoundButtonCap",
          "Lever": "DoorLeverHandle", "Lever.001": "DoorLeverBase",
          "Phone": "DeskPhone", "Phone.001": "DeskPhoneButton"}
MONITOR = "Monitor"                   # монитор на столе ресепшена: на его экран игра кладёт монитор камер
MONITOR_SCREEN = "Material.005"       # материал экрана (стекло) у этого монитора
MAX_TRIS = 20000                 # тяжелее — упрощаем (Decimate): клавиатуры, кресло-мешок, телефон, node_0
MAX_TEXTURE = 1024               # текстуры больше — уменьшаются (Poly Haven скачаны в 1K, вшитые 4K — до 1K)
OPEN_SHEET = 0.08                # доля открытых рёбер (у ребра одна грань), с которой меш делается двусторонним

deps = bpy.context.evaluated_depsgraph_get()
col = bpy.data.collections.new("MapExport")
bpy.context.scene.collection.children.link(col)


def skipped(o):
    if o.name in SKIP_NAMES or o.name.startswith(SKIP_PREFIX) or o.hide_render:
        return True
    p = o
    while p is not None:
        if p.name == GENERATOR_EMPTY:
            return True
        p = p.parent
    # скрытое глазком во вьюпорте (потолок, улица) выгружаем: в рендере оно есть
    return o.name not in bpy.context.view_layer.objects


def safe_name(name):
    s = re.sub(r"[^A-Za-z0-9_]+", "_", name.strip()).strip("_")
    return s if s and not s[0].isdigit() else "Mat_" + s


def world_bounds(o):
    pts = [o.matrix_world @ Vector(c) for c in o.bound_box]
    return (Vector((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts))),
            Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts))))


# ── Меши: всё запекаем в отдельные объекты ──
baked = []
for o in list(bpy.data.objects):
    if o.type != "MESH" or skipped(o):
        continue
    ev = o.evaluated_get(deps)
    me = bpy.data.meshes.new_from_object(ev, preserve_all_data_layers=True, depsgraph=deps)
    if len(me.polygons) == 0:
        bpy.data.meshes.remove(me)
        continue
    for i, slot in enumerate(o.material_slots):  # материалы, привязанные к объекту, а не к мешу
        if slot.link == "OBJECT" and slot.material and i < len(me.materials):
            me.materials[i] = slot.material
    src_name = o.name
    o.name = "__src_" + src_name  # иначе новый объект с тем же именем получит приписку .001
    nob = bpy.data.objects.new(RENAME.get(src_name, src_name.strip()), me)
    nob.matrix_world = o.matrix_world.copy()
    col.objects.link(nob)
    baked.append((src_name, nob))
print(f"MESHES {len(baked)}")

by_src = {src: ob for src, ob in baked}

# Дверь в генераторную — открыть, если она закрыта (створка в плоскости проёма, тонкая по x).
# С версии карты 2026-11-05 автор открыл её сам — тогда не трогаем
rig = bpy.data.objects.get("__src_" + DOOR_RIG) or bpy.data.objects.get(DOOR_RIG)
leaf = by_src.get(DOOR_PARTS[0])
closed = leaf is not None and (lambda b: b[1].x - b[0].x < 0.2)(world_bounds(leaf))
print("DOOR", "closed — opening" if closed else "already open")
if rig is not None and closed:
    h = rig.matrix_world.translation
    turn = Matrix.Translation(h) @ Matrix.Rotation(math.radians(DOOR_OPEN_ANGLE), 4, "Z") @ Matrix.Translation(-h)
    for part in DOOR_PARTS:
        if part in by_src:
            by_src[part].matrix_world = turn @ by_src[part].matrix_world
    print("DOOR opened around", tuple(round(v, 2) for v in h))


def face_all(ob, want_up):
    """каждую грань плоскости — вверх (пол) или вниз (потолок): у потолка карты грани смотрят в разные стороны"""
    bm = bmesh.new()
    bm.from_mesh(ob.data)
    rot = ob.matrix_world.to_3x3()
    wrong = [f for f in bm.faces if ((rot @ f.normal).z > 0) != want_up]
    if wrong:
        bmesh.ops.reverse_faces(bm, faces=wrong)
        bm.to_mesh(ob.data)
        print("FLIPPED", ob.name, f"{len(wrong)}/{len(bm.faces)}")
    bm.free()


def triangulate_ngons(ob):
    """многоугольники больше четырёх углов — на треугольники здесь: Unity режет вогнутые неправильно"""
    bm = bmesh.new()
    bm.from_mesh(ob.data)
    ngons = [f for f in bm.faces if len(f.verts) > 4]
    if ngons:
        bmesh.ops.triangulate(bm, faces=ngons, quad_method="BEAUTY", ngon_method="BEAUTY")
        bm.to_mesh(ob.data)
        print("TRIANGULATED", ob.name, len(ngons))
    bm.free()


def open_share(ob):
    """Доля открытых рёбер (у ребра одна грань): у закрытого меша 0, у листа жести — много"""
    edges = ob.data.edges
    if len(edges) == 0:
        return 0.0
    counts = {}
    for poly in ob.data.polygons:
        for key in poly.edge_keys:
            counts[key] = counts.get(key, 0) + 1
    return sum(1 for c in counts.values() if c == 1) / len(edges)


doubled = set()  # двусторонние: при упрощении лимит треугольников — на одну сторону


def double_sided(ob):
    doubled.add(ob.name)
    bm = bmesh.new()
    bm.from_mesh(ob.data)
    dup = bmesh.ops.duplicate(bm, geom=bm.faces[:] + bm.edges[:] + bm.verts[:])
    bmesh.ops.reverse_faces(bm, faces=[g for g in dup["geom"] if isinstance(g, bmesh.types.BMFace)])
    bm.to_mesh(ob.data)
    bm.free()
    print("DOUBLE-SIDED", ob.name)


for src, ob in baked:
    lo, hi = world_bounds(ob)
    size = hi - lo
    if size.z < 0.001 and size.x * size.y > 1.0:  # пол или потолок (мелкие значки не трогаем)
        face_all(ob, want_up=lo.z < 2.5)
    elif min(size.x, size.y) < 0.001:  # вертикальная плоскость — видна с обеих сторон
        double_sided(ob)
    elif open_share(ob) >= OPEN_SHEET:  # открытая «жесть»: изнанка видна в открытый конец
        double_sided(ob)
    triangulate_ngons(ob)

# Дэнс пол — на плитки
if DISCO in by_src:
    disco = by_src[DISCO]
    me = disco.data
    bm = bmesh.new()
    bm.from_mesh(me)
    bm.faces.ensure_lookup_table()
    islands, seen = [], set()
    for f in bm.faces:
        if f.index in seen:
            continue
        stack, island = [f], []
        while stack:
            g = stack.pop()
            if g.index in seen:
                continue
            seen.add(g.index)
            island.append(g.index)
            for e in g.edges:
                stack.extend(h for h in e.link_faces if h.index not in seen)
        islands.append(island)
    tile_mat = next((i for i, m in enumerate(me.materials) if m and m.name == "DiscoTile"), 0)
    tiles = []
    for island in islands:
        keep = set(island)
        part = bmesh.new()
        part.from_mesh(me)
        part.faces.ensure_lookup_table()
        is_tile = all(part.faces[i].material_index == tile_mat for i in island)
        bmesh.ops.delete(part, geom=[f for f in part.faces if f.index not in keep], context="FACES")
        pm = bpy.data.meshes.new("disco_part")
        part.to_mesh(pm)
        part.free()
        for m in me.materials:
            pm.materials.append(m)
        center = disco.matrix_world @ (sum((v.co for v in pm.vertices), Vector()) / len(pm.vertices))
        tiles.append((is_tile, center, pm))
    bm.free()
    # порядок плиток: от офиса вглубь (y убывает), в ряду — слева направо для игрока (x убывает)
    floor_tiles = sorted([t for t in tiles if t[0]], key=lambda t: (-round(t[1].y, 2), -round(t[1].x, 2)))
    for k, (_, _, pm) in enumerate(floor_tiles):
        ob = bpy.data.objects.new(f"DanceTile_{k:03d}", pm)
        ob.matrix_world = disco.matrix_world.copy()
        col.objects.link(ob)
    for _, _, pm in (t for t in tiles if not t[0]):
        ob = bpy.data.objects.new("DiscoFloorBase", pm)
        ob.matrix_world = disco.matrix_world.copy()
        col.objects.link(ob)
    bpy.data.objects.remove(disco)
    print(f"DISCO tiles {len(floor_tiles)}")

# Тяжёлые меши упростить
for ob in list(col.objects):
    tris = sum(len(p.vertices) - 2 for p in ob.data.polygons)
    limit = MAX_TRIS * (2 if ob.name in doubled else 1)
    if tris > limit:
        d = ob.modifiers.new("Decimate", "DECIMATE")
        d.ratio = limit / tris
        print(f"DECIMATE {ob.name} {tris} → ~{limit}")

# Экран монитора: центр, нормаль (от экрана к зрителю), верх, ширина и высота — в координатах Blender
screen = None
mon = by_src.get(MONITOR)
if mon is not None:
    idx = next((i for i, m in enumerate(mon.data.materials) if m and m.name == MONITOR_SCREEN), None)
    faces = [p for p in mon.data.polygons if p.material_index == idx] if idx is not None else []
    if faces:
        mw, rot = mon.matrix_world, mon.matrix_world.to_3x3()
        n = sum(((rot @ p.normal) * p.area for p in faces), Vector()).normalized()
        right = Vector((0, 0, 1)).cross(n).normalized()  # вправо для того, кто смотрит на экран
        up = n.cross(right).normalized()
        pts = [mw @ mon.data.vertices[v].co for p in faces for v in p.vertices]
        c0 = pts[0]
        us, vs, ds = ([(q - c0).dot(a) for q in pts] for a in (right, up, n))
        center = c0 + right * (min(us) + max(us)) / 2 + up * (min(vs) + max(vs)) / 2 + n * max(ds)
        screen = {"center": list(center), "normal": list(n), "up": list(up),
                  "width": max(us) - min(us), "height": max(vs) - min(vs)}
        print("SCREEN", tuple(round(c, 3) for c in center), "size", round(screen["width"], 3), round(screen["height"], 3))

# Генератор автора (сам он не выгружается — в игре свой, с анимациями): центр, куда смотрит пульт (стекло прибора
# относительно центра, по полу) и размер — игра ставит свой генератор так же
generator = None
src_gen = bpy.data.objects.get("portable_generator")
if src_gen is not None:
    ev = src_gen.evaluated_get(deps)
    me = ev.to_mesh()
    mw = src_gen.matrix_world
    pts = [mw @ v.co for v in me.vertices]
    lo = Vector((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts)))
    hi = Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts)))
    glass, area = Vector(), 0.0
    for p in me.polygons:
        mat = src_gen.material_slots[p.material_index].material if p.material_index < len(src_gen.material_slots) else None
        if mat is not None and "glass" in mat.name.lower():
            glass += (mw @ p.center) * p.area
            area += p.area
    ev.to_mesh_clear()
    if area > 0:
        center = (lo + hi) / 2
        front = glass / area - center
        front.z = 0
        generator = {"center": list(center), "front": list(front.normalized()), "size": list(hi - lo)}
        print("GENERATOR front", tuple(round(c, 2) for c in front.normalized()), "size", tuple(round(c, 3) for c in hi - lo))

# Маркеры
def marker(name, loc, rot_z=0.0):
    e = bpy.data.objects.new(name, None)
    e.location = loc
    e.rotation_euler = (0.0, 0.0, rot_z)
    col.objects.link(e)


gen = bpy.data.objects.get(GENERATOR_EMPTY)
if gen is not None:
    m = gen.matrix_world
    marker("Marker_Generator", m.translation, m.to_euler().z)
seat = bpy.data.objects.get(SEAT)
if seat is not None:
    lo, hi = world_bounds(seat.evaluated_get(deps))
    marker("Marker_Seat", Vector(((lo.x + hi.x) / 2, (lo.y + hi.y) / 2, 0.0)))

# ── Материалы ──
used = []
for ob in col.objects:
    if ob.type == "MESH":
        for m in ob.data.materials:
            if m is not None and m not in used:
                used.append(m)

names = set()
for m in used:
    n = "Map_" + safe_name(m.name)
    while n in names:
        n += "_"
    names.add(n)
    m["map_src"] = m.name
    m.name = n

written = {}   # md5 → имя файла
missing = set()


def tex_stem(name):
    """имя без разрешения и расширения: painted_plaster_wall_diff_2k.exr → painted_plaster_wall_diff"""
    return re.sub(r"_\d+k$", "", os.path.splitext(os.path.basename(name))[0].lower())


# Art/Map/textures: недостающие картинки (скачанные с Poly Haven в 1K) — ищутся по имени без разрешения
# и расширения, потому что в .blend могут быть _2k.exr, а рядом лежать _1k.jpg
LOCAL = {}
_tex_dir = os.path.join(BLEND_DIR, "textures")
if os.path.isdir(_tex_dir):
    for _f in sorted(os.listdir(_tex_dir)):
        LOCAL.setdefault(tex_stem(_f), os.path.join(_tex_dir, _f))


def find_file(img):
    path = os.path.normpath(bpy.path.abspath(img.filepath, library=img.library))
    base = os.path.basename(path)
    for cand in (path, os.path.join(BLEND_DIR, base), os.path.join(_tex_dir, base), LOCAL.get(tex_stem(base))):
        if cand and os.path.isfile(cand):
            return cand
    return None


def source(img):
    """(байты, расширение) вшитой или найденной картинки, иначе None"""
    if img.packed_file is not None:
        ext = os.path.splitext(img.filepath or img.name)[1].lower() or "." + img.file_format.lower()
        return img.packed_file.data, ext
    found = find_file(img)
    if found is not None:
        with open(found, "rb") as f:
            return f.read(), os.path.splitext(found)[1].lower()
    missing.add(os.path.basename(bpy.path.abspath(img.filepath)))
    return None


def loaded(img):
    """картинка, у которой можно читать пиксели (вшитая или найденная рядом)"""
    if img.packed_file is None:
        found = find_file(img)
        if found is None:
            source(img)  # запомнить как недостающую
            return None
        if os.path.normpath(bpy.path.abspath(img.filepath)) != found:  # путь в .blend не совпал — взять найденный
            img.filepath = found
            img.reload()
    if img.size[0] == 0:
        return None
    return img


def copy_image(img, stem):
    """копирует картинку как есть (jpg/png) или переписывает в png (exr и прочее). Возвращает имя файла"""
    src = source(img)
    if src is None:
        return None
    data, ext = src
    key = hashlib.md5(data).hexdigest()
    if key in written:
        return written[key]
    im = loaded(img)
    if ext in (".png", ".jpg", ".jpeg") and (im is None or max(im.size) <= MAX_TEXTURE):
        name = stem + ext
        with open(os.path.join(TEX, name), "wb") as f:
            f.write(data)
    else:  # exr и прочее, или больше MAX_TEXTURE — переписать в png нужного размера
        if im is None:
            return None
        name = save_png(stem + ".png", pixels(im))
        written[key] = name
        return name
    written[key] = name
    print("TEXTURE", name)
    return name


def pixels(im):
    if max(im.size) > MAX_TEXTURE:
        im.scale(min(im.size[0], MAX_TEXTURE), min(im.size[1], MAX_TEXTURE))
    w, h = im.size
    return np.array(im.pixels[:], dtype=np.float32).reshape(h, w, 4)


def save_png(name, rgba):
    """пишет png и возвращает имя файла; такая же картинка уже записана — её имя (у копий материалов
    карты, например 18 штукатурок, склейки одинаковые)"""
    data = np.clip(rgba, 0.0, 1.0).astype(np.float32)
    key = hashlib.md5(data.tobytes()).hexdigest()
    if key in written:
        return written[key]
    h, w, _ = data.shape
    out = bpy.data.images.new(name, w, h, alpha=True)
    out.colorspace_settings.name = "Non-Color"
    out.pixels.foreach_set(data.ravel())
    out.filepath_raw = os.path.join(TEX, name)
    out.file_format = "PNG"
    out.save()
    bpy.data.images.remove(out)
    written[key] = name
    print("TEXTURE", name)
    return name


def upstream(sock, depth=0):
    """ближайший узел картинки выше по цепочке (через Mix, Normal Map, Reroute и т.п.)"""
    if not sock.is_linked or depth > 12:
        return None
    node = sock.links[0].from_node
    if node.type == "TEX_IMAGE":
        return node if node.image else None
    for inp in node.inputs:
        r = upstream(inp, depth + 1)
        if r is not None:
            return r
    return None


def tiling(tex_node):
    """масштаб и сдвиг из узла Mapping перед картинкой; координаты (UV / Generated / Object)"""
    vec = tex_node.inputs.get("Vector")
    if vec is None or not vec.is_linked:
        return [1.0, 1.0], [0.0, 0.0], "UV"
    node = vec.links[0].from_node
    coords = "UV"
    if node.type == "MAPPING":
        sc = node.inputs["Scale"].default_value
        lo = node.inputs["Location"].default_value
        src = node.inputs["Vector"]
        if src.is_linked:
            coords = src.links[0].from_socket.name
        return [sc[0], sc[1]], [lo[0], lo[1]], coords
    if node.type == "TEX_COORD":
        coords = vec.links[0].from_socket.name
    return [1.0, 1.0], [0.0, 0.0], coords


def value(sock, default):
    if sock is None:
        return default
    v = sock.default_value
    return list(v) if hasattr(v, "__len__") else float(v)


materials = []
for m in used:
    entry = {"name": m.name, "source": m.get("map_src", m.name), "color": list(m.diffuse_color), "baseMap": "",
             "normalMap": "", "normalScale": 1.0, "msMap": "", "metallic": 0.0, "smoothness": 0.5,
             "emission": [0.0, 0.0, 0.0], "tiling": [1.0, 1.0], "offset": [0.0, 0.0], "coords": "UV",
             "transparent": False}
    nodes = m.node_tree.nodes if m.use_nodes and m.node_tree else []
    bsdf = next((n for n in nodes if n.type == "BSDF_PRINCIPLED"), None)
    emit = next((n for n in nodes if n.type == "EMISSION"), None)
    stem = m.name
    if bsdf is not None:
        inp = bsdf.inputs
        entry["color"] = value(inp["Base Color"], entry["color"])
        entry["metallic"] = value(inp["Metallic"], 0.0)
        entry["smoothness"] = 1.0 - value(inp["Roughness"], 0.5)
        alpha = value(inp["Alpha"], 1.0)

        base = upstream(inp["Base Color"])
        if base is not None:
            entry["tiling"], entry["offset"], entry["coords"] = tiling(base)
        alpha_node = upstream(inp["Alpha"])
        if alpha_node is not None and base is not None and alpha_node.image != base.image:
            b, a = loaded(base.image), loaded(alpha_node.image)
            if b is not None and a is not None:
                rgba = pixels(b)
                ap_ = pixels(a)
                if ap_.shape == rgba.shape:
                    rgba[..., 3] = ap_[..., 0]
                    entry["baseMap"] = save_png(stem + "_BaseAlpha.png", rgba)
            entry["transparent"] = True
        elif alpha_node is not None or alpha < 0.999:
            entry["transparent"] = True
            entry["color"][3] = alpha if alpha_node is None else 1.0
        if base is not None and not entry["baseMap"]:
            entry["baseMap"] = copy_image(base.image, stem + "_Base") or ""
            if entry["baseMap"]:
                entry["color"] = [1.0, 1.0, 1.0, entry["color"][3]]  # цвет даёт текстура

        nrm = upstream(inp["Normal"])
        if nrm is not None:
            entry["normalMap"] = copy_image(nrm.image, stem + "_Normal") or ""
            nm = inp["Normal"].links[0].from_node
            if nm.type == "NORMAL_MAP":
                entry["normalScale"] = value(nm.inputs["Strength"], 1.0)

        met, rough = upstream(inp["Metallic"]), upstream(inp["Roughness"])
        if met is not None or rough is not None:
            mi = loaded(met.image) if met is not None else None
            ri = loaded(rough.image) if rough is not None else None
            if mi is not None or ri is not None:
                ref = pixels(mi if mi is not None else ri)
                ms = np.empty_like(ref)
                ms[..., 0:3] = ref[..., 0:1] if mi is not None else entry["metallic"]
                if ri is not None:
                    r = pixels(ri)
                    ms[..., 3] = 1.0 - (r[..., 0] if r.shape == ref.shape else float(np.mean(r[..., 0])))
                else:
                    ms[..., 3] = entry["smoothness"]
                entry["msMap"] = save_png(stem + "_MS.png", ms)

        ec = value(inp.get("Emission Color"), [0, 0, 0, 1])
        es = value(inp.get("Emission Strength"), 0.0)
        if es > 0 and max(ec[:3]) > 0:
            entry["emission"] = [c * es for c in ec[:3]]
    elif emit is not None:
        c = value(emit.inputs["Color"], [1, 1, 1, 1])
        s = value(emit.inputs["Strength"], 1.0)
        entry["color"] = list(c)
        entry["emission"] = [x * min(s, 4.0) for x in c[:3]]
    materials.append(entry)

with open(os.path.join(OUT, "map_materials.json"), "w", encoding="utf-8") as f:
    json.dump({"materials": materials, "missing": sorted(missing), "monitorScreen": screen, "generator": generator},
              f, ensure_ascii=False, indent=1)
print(f"MATERIALS {len(materials)}, missing textures {len(missing)}")

# ── FBX ──
for o in bpy.context.view_layer.objects:
    o.select_set(False)
for o in col.objects:
    o.select_set(True)
bpy.context.view_layer.objects.active = next(iter(col.objects))
fbx = os.path.join(OUT, "Map.fbx")
bpy.ops.export_scene.fbx(
    filepath=fbx,
    use_selection=True,
    object_types={"MESH", "EMPTY"},
    apply_scale_options="FBX_SCALE_ALL",
    axis_forward="-Z",
    axis_up="Y",
    use_mesh_modifiers=True,   # остался только Decimate у тяжёлых мешей
    mesh_smooth_type="OFF",
    bake_anim=False,
    path_mode="STRIP",
    embed_textures=False,
)
tris = sum(sum(len(p.vertices) - 2 for p in o.data.polygons) for o in col.objects if o.type == "MESH")
print(f"EXPORT {fbx}: {len(col.objects)} objects, ~{tris} triangles before decimate")
