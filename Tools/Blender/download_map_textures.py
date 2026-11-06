# Скачать с Poly Haven (polyhaven.com, лицензия CC0) текстуры, на которые ссылается карта, но которых нет в .blend.
#   blender -b "Art/Map/map 67dfn wip.blend" --python download_map_textures.py -- --out "Art/Map/textures"
# Запускает download_map_textures.bat. Потом — export_map.bat: он найдёт картинки в Art/Map/textures.
#
# Ассеты Poly Haven автор карты скачивал в Downloads (папки вида painted_plaster_wall_2k.blend\textures\…),
# в .blend они не вшиты. Здесь берётся то же самое в 1K и jpg (самое лёгкое; не отдаётся — png или exr).
# Карты высот (disp) не качаются: Unity их не использует. Уже скачанные файлы пропускаются.
import argparse
import hashlib
import json
import os
import re
import sys
import urllib.request

import bpy

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
ap = argparse.ArgumentParser()
ap.add_argument("--out", required=True)
ap.add_argument("--res", default="1k")
args = ap.parse_args(argv)

FORMATS = ("jpg", "png", "exr")
SKIP = ("_disp",)
UA = {"User-Agent": "SSDFNC-map-textures/1.0"}  # Poly Haven не отвечает на запросы без своей подписи
os.makedirs(args.out, exist_ok=True)


def stem(name):
    return re.sub(r"_\d+k$", "", os.path.splitext(os.path.basename(name))[0].lower())


def fetch(url, tries=3):
    for _ in range(tries):
        try:
            with urllib.request.urlopen(urllib.request.Request(url, headers=UA), timeout=120) as r:
                return r.read()
        except Exception as ex:  # обрыв, таймаут, ошибка сервера — ещё раз
            print("RETRY", url.rsplit("/", 1)[1], ex)
    return None


def walk(node, found):
    if isinstance(node, dict):
        if isinstance(node.get("url"), str):
            found.append(node)
        for v in node.values():
            walk(v, found)


# Какие файлы каких ассетов нужны: из путей картинок в .blend
refs = {}
for im in bpy.data.images:
    if im.packed_file or not im.filepath:
        continue
    p = os.path.normpath(bpy.path.abspath(im.filepath))
    m = re.search(r"[\\/]([^\\/]+?)_\d+k\.blend(?: \(\d+\))?[\\/]textures[\\/]([^\\/]+)$", p)
    if m:
        refs.setdefault(m.group(1), set()).add(m.group(2))
print(f"ASSETS {len(refs)}")

got, size, failed = 0, 0, []
for asset, files in sorted(refs.items()):
    data = fetch(f"https://api.polyhaven.com/files/{asset}")
    if data is None:
        failed.append(asset)
        continue
    entries = []
    walk(json.loads(data), entries)
    by_stem = {}
    for e in entries:
        name = e["url"].rsplit("/", 1)[1]
        ext = name.rsplit(".", 1)[-1].lower()
        if f"/{args.res}/" in e["url"] and ext in FORMATS:
            by_stem.setdefault(stem(name), {})[ext] = e
    for f in sorted(files):
        s = stem(f)
        if any(x in s for x in SKIP):
            continue
        path = None
        for e in (by_stem.get(s, {}).get(x) for x in FORMATS):
            if e is None:
                continue
            cand = os.path.join(args.out, e["url"].rsplit("/", 1)[1])
            if os.path.isfile(cand) and os.path.getsize(cand) == e.get("size", -1):
                path = cand
                break
            blob = fetch(e["url"])
            if blob is None or (e.get("md5") and hashlib.md5(blob).hexdigest() != e["md5"]):
                continue
            with open(cand, "wb") as out:
                out.write(blob)
            path = cand
            break
        if path is None:
            failed.append(f"{asset}: {f}")
            continue
        got += 1
        size += os.path.getsize(path)

print(f"DONE {got} files, {size / 1e6:.1f} MB in {args.out}")
for f in failed:
    print("FAILED", f)
