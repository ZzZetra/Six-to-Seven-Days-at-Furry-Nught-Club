"""Проверка примеров на слух без ушей: что слышит в них Vosk (свободная речь, английская модель).

  .venv\\Scripts\\python check_listen.py data/listen/val
"""
import json
import os
import sys
import wave

from vosk import KaldiRecognizer, Model, SetLogLevel

HERE = os.path.dirname(os.path.abspath(__file__))
SetLogLevel(-1)
model = Model(os.path.join(HERE, "..", "SpeechTest", "models", "vosk-model-small-en-us-0.15"))
folder = sys.argv[1] if len(sys.argv) > 1 else os.path.join(HERE, "data", "listen", "val")
for f in sorted(os.listdir(folder)):
    with wave.open(os.path.join(folder, f), "rb") as w:
        data = w.readframes(w.getnframes())
    rec = KaldiRecognizer(model, 16000)
    rec.AcceptWaveform(data)
    print(f"{f:22s} → «{json.loads(rec.FinalResult()).get('text', '')}»")
