"""Скачать то, что нужно для обучения: голоса Piper (синтез примеров) и модели признаков openWakeWord.

  .venv\\Scripts\\python download.py

Голоса — huggingface.co/rhasspy/piper-voices, в voices/. Модели признаков (melspectrogram, embedding) —
релиз openWakeWord на GitHub, в features/: их же потом возьмёт игра. Уже скачанное пропускается.
"""
import os
import sys
import urllib.request

HERE = os.path.dirname(os.path.abspath(__file__))
VOICES = os.path.join(HERE, "voices")
FEATURES = os.path.join(HERE, "features")

PIPER = "https://huggingface.co/rhasspy/piper-voices/resolve/main"
# (путь в репозитории, зачем)
VOICE_LIST = [
    ("en/en_US/libritts_r/medium/en_US-libritts_r-medium", "904 английских голоса — основа"),
    ("en/en_US/l2arctic/medium/en_US-l2arctic-medium", "английский с акцентом (неносители языка)"),
    ("ru/ru_RU/irina/medium/ru_RU-irina-medium", "русский женский"),
    ("ru/ru_RU/denis/medium/ru_RU-denis-medium", "русский мужской"),
    ("ru/ru_RU/dmitri/medium/ru_RU-dmitri-medium", "русский мужской"),
    ("ru/ru_RU/ruslan/medium/ru_RU-ruslan-medium", "русский мужской"),
]
OWW = "https://github.com/dscripka/openWakeWord/releases/download/v0.5.1"
FEATURE_LIST = ["melspectrogram.onnx", "embedding_model.onnx"]
UA = {"User-Agent": "SSDFNC-speech-train/1.0"}


def fetch(url, path):
    if os.path.isfile(path) and os.path.getsize(path) > 0:
        print("есть  ", os.path.basename(path))
        return
    os.makedirs(os.path.dirname(path), exist_ok=True)
    tmp = path + ".part"
    with urllib.request.urlopen(urllib.request.Request(url, headers=UA), timeout=120) as r, open(tmp, "wb") as f:
        while True:
            chunk = r.read(1 << 20)
            if not chunk:
                break
            f.write(chunk)
    os.replace(tmp, path)
    print("скачан", os.path.basename(path), f"{os.path.getsize(path) / 1e6:.1f} МБ")


def main():
    for rel, why in VOICE_LIST:
        name = rel.rsplit("/", 1)[1]
        for ext in (".onnx", ".onnx.json"):
            fetch(f"{PIPER}/{rel}{ext}?download=true", os.path.join(VOICES, name + ext))
    for name in FEATURE_LIST:
        fetch(f"{OWW}/{name}", os.path.join(FEATURES, name))


if __name__ == "__main__":
    sys.exit(main())
