"""Кадры камер (картинки и короткие видео) из «Загрузок» → Assets/StreamingAssets/Cameras/<камера>/<кто в кадре>.

  python import_cameras.py [папка «Загрузки»]

Камера — как на кнопках монитора: 2–7. Кто в кадре — имена фурри через « и »
(«Стрей и Доли.jpg»), пустая комната — «пусто.jpg». Игра (CameraFeeds) берёт кадр, если в комнате ровно эти фурри;
нет кадра — на мониторе текст, как раньше. Картинки — jpg/png, видео — mp4 (крутится по кругу); mkv (так Telegram
сохраняет гифки) перегоняется в mp4 через Blender (convert_video.py). Рендеры автора (png 1920×1080, папка
«Renders 67FDN») пережимаются в jpg 1280×720, как остальные кадры. Новые кадры можно класть в папку и руками.
"""
import os
import shutil
import subprocess
import sys

from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
SRC = sys.argv[1] if len(sys.argv) > 1 else os.path.join(os.path.expanduser("~"), "Downloads")
OUT = os.path.join(HERE, "..", "..", "Assets", "StreamingAssets", "Cameras")
BLENDER = r"C:\Program Files (x86)\Steam\steamapps\common\Blender\blender.exe"

# файл (в «Загрузках» или «Загрузки/Telegram Desktop») → камера, кто в кадре (подписал пользователь, 2026-11-06)
FILES = [
    ("коридор к двери стрей.jpg", "2", "Стрей"),
    ("концерт зал доли.jpg", "3", "Доли"),
    ("концерт зал стрей.jpg", "3", "Стрей"),
    ("концерт зал стрей и доли.jpg", "3", "Стрей и Доли"),
    ("комната отдыха- стрей.jpg", "4", "Стрей"),
    ("5коридор стрей.jpg", "5", "Стрей"),
    ("серверная Дали.jpg", "6", "Дали"),
    (os.path.join("Telegram Desktop", "Nova 4 alone 1.mkv"), "4", "Нова"),
    (os.path.join("Telegram Desktop", "Nova and Stray 4.mkv"), "4", "Нова и Стрей"),
]
# Рендеры автора (папка «Renders 67FDN», 2026-11-06) — те, которых ещё не было. Уже стоящие кадры не трогаем
# (Dalie Servers, Dollie Alone Concert, Stray 4 Alone, Stray Alone Corridor 2, Stray Concert Alone,
# Stray and Dollie Concert, гифки Новы). Не взят «Stray Alone Corridor» (не подписан номер коридора; Стрей в коридорах
# 2 и 5 уже есть). «Corridor 5 Full house» — это Стрей, Нова и Доли (пользователь), «Actual full house» — все четверо
RENDERS = "Renders 67FDN"
FILES += [(os.path.join(RENDERS, src), cam, who) for src, cam, who in [
    ("Corridor 2 Empty.png", "2", "пусто"),
    ("Dollie Alone Corridor 2.png", "2", "Доли"),
    ("Dollie and Stray Corridor 2.png", "2", "Стрей и Доли"),
    ("Concert Empty.png", "3", "пусто"),
    ("Concert Nova Alone.png", "3", "Нова"),
    ("Concert Nova and Dollie.png", "3", "Нова и Доли"),
    ("Concert Nova and Dollie and Stray.png", "3", "Стрей, Нова и Доли"),
    ("4 Room Empty.png", "4", "пусто"),
    ("Corridor 5 Empty.png", "5", "пусто"),
    ("Dolie And Stray Corridor 5.png", "5", "Стрей и Доли"),
    ("Stray Nova Corridor 5.png", "5", "Нова и Стрей"),
    ("Corridor 5 Dallie Dollie and Stray.png", "5", "Стрей, Дали и Доли"),
    ("Corridor 5 Full house.png", "5", "Стрей, Нова и Доли"),
    ("Actual full house Corridor 5.png", "5", "Стрей, Нова, Дали и Доли"),
    ("Servers Empty.png", "6", "пусто"),
]]
# прислал отдельно; «Corridor 2» в имени — ошибка, это коридор 5 (пользователь). Доли — вместо рендера из папки
FILES += [(os.path.join("Telegram Desktop", "Dallie Alone Corridor 2.png"), "5", "Дали"),
          (os.path.join("Telegram Desktop", "Dollie Alone Corridor 5.png"), "5", "Доли")]
FRAME = (1280, 720)  # рендеры — в размер остальных кадров


def main():
    for src, cam, who in FILES:
        path = os.path.join(SRC, src)
        if not os.path.isfile(path):
            print("нет файла:", src)
            continue
        folder = os.path.join(OUT, cam)
        os.makedirs(folder, exist_ok=True)
        ext = os.path.splitext(src)[1].lower()
        if ext == ".png":  # рендеры автора (1920×1080) — в jpg размера остальных кадров
            dst = os.path.join(folder, who + ".jpg")
            Image.open(path).convert("RGB").resize(FRAME, Image.LANCZOS).save(dst, quality=90)
        elif ext in (".jpg", ".jpeg", ".png", ".mp4"):
            dst = os.path.join(folder, who + ext.replace(".jpeg", ".jpg"))
            shutil.copyfile(path, dst)
        else:  # mkv и прочее — в mp4
            dst = os.path.join(folder, who + ".mp4")
            subprocess.run([BLENDER, "-b", "--python", os.path.join(HERE, "convert_video.py"), "--",
                            "convert", path, dst], check=True, capture_output=True)
        print(f"{cam}/{os.path.basename(dst):24s} ← {src}")
    print("→", os.path.normpath(OUT))


if __name__ == "__main__":
    sys.exit(main())
