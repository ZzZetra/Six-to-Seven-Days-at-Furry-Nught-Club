"""Перенести звуки из «Загрузок» в игру: Assets/StreamingAssets/Sounds (оттуда их берёт GameSounds).

  ..\\SpeechTrain\\.venv\\Scripts\\python import_sounds.py [папка с исходниками]

Файлы копируются как есть, только с понятными именами (имя без расширения видно в паузе → «Звуки»).
«обрезать и звук фурри» — колокольчики: в игру идёт первый взмах (до тихого места на 0.49 с), целиком —
отдельным файлом. Звуки генератора режет generator_sounds.py (запуск, работа, глушение).
Новые звуки можно класть в StreamingAssets/Sounds и руками — игра подхватит их сама.
"""
import os
import shutil
import sys

import numpy as np
import soundfile as sf

HERE = os.path.dirname(os.path.abspath(__file__))
SRC = sys.argv[1] if len(sys.argv) > 1 else os.path.join(os.path.expanduser("~"), "Downloads")
OUT = os.path.join(HERE, "..", "..", "Assets", "StreamingAssets", "Sounds")

# файл в «Загрузках» → имя в игре
COPY = [
    ("эмбиэнт (возможно).mp3", "эмбиэнт.mp3"),
    ("big-metal-door-close_zyhtehe_.mp3", "дверь закрылась.mp3"),
    ("doomsday-door_zykyemnd.mp3", "дверь закрылась 2.mp3"),
    ("удар об дверь.mp3", "удар об дверь.mp3"),
    ("удар об дверь2.mp3", "удар об дверь 2.mp3"),
    ("конец_ночи.mp3", "конец ночи.mp3"),
    ("+звук для конца ночи.mp3", "замок 1.mp3"),         # уходим и запираем за собой — играет после «конца ночи»
    ("+ звук для конца ночи2.mp3", "замок 2.mp3"),
    ("звук в загрузочном перед ночью.mp3", "загрузка перед ночью.mp3"),
    ("правильный ответ звонка.mp3", "правильный ответ звонка.mp3"),
    ("звук кнопки.mp3", "звук кнопки.mp3"),
    ("звук фурри1.mp3", "фурри 1.mp3"),
    ("звук фурри2.mp3", "фурри 2.mp3"),
    ("обрезать и звук фурри.mp3", "колокольчики (целиком).mp3"),
    ("прикольный низкий звук(бааааааам бамммммммммммбаааммммммммм).mp3", "низкий бааам.mp3"),
    ("луп состоящий из низкого воу воу воу воу.mp3", "низкий воу-воу (луп).mp3"),
    ("ууууууууууууууувоп.mp3", "ууууувоп.mp3"),
    ("ribhavagrawal-brittle-beep-sound-effect-230471.mp3", "бип.mp3"),
    ("bodleasons-do-it-to-it-short-2-version-620354.mp3", "дэнс пол.mp3"),  # играет, пока горит дэнс пол
]
BELLS = ("обрезать и звук фурри.mp3", "колокольчики.wav", 0.49, 0.04)  # исходник, имя, до какой секунды, затухание: один взмах
# скример — один «буум» из «низкого бааам» (в файле 14 одинаковых ударов каждые 1.6 с, звучало как петля)
SCREAMER = ("прикольный низкий звук(бааааааам бамммммммммммбаааммммммммм).mp3", "скример.wav", 1.6, 0.3)


def main():
    os.makedirs(OUT, exist_ok=True)
    for src, dst in COPY:
        path = os.path.join(SRC, src)
        if not os.path.isfile(path):
            print(f"нет файла: {src}")
            continue
        shutil.copyfile(path, os.path.join(OUT, dst))
        print(f"{dst:32s} ← {src}")

    for src, dst, end, fade in (BELLS, SCREAMER):
        x, sr = sf.read(os.path.join(SRC, src), dtype="float32", always_2d=True)
        x = x[:int(end * sr)].copy()
        n = int(fade * sr)
        x[-n:] *= np.linspace(1, 0, n, dtype=np.float32)[:, None]
        sf.write(os.path.join(OUT, dst), x, sr, subtype="PCM_16")
        print(f"{dst:32s} ← {src}, 0–{end} с")
    print("→", os.path.normpath(OUT))


if __name__ == "__main__":
    sys.exit(main())
