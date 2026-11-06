"""Распознавание трёх слов своей моделью в реальном времени (так же будет и в игре).

Звук 16 кГц кусками по 80 мс (1280 отсчётов) → последние 2 с звука → признаки (embed.py: мел-спектр + эмбеддинг,
ровно как при обучении) → своя модель на 16 эмбеддингах → вероятности «не команда / verity / lovety / moggaty». Слово засчитывается, когда его
вероятность выше порога; ещё раз — только когда она упадёт ниже release и пройдёт repeat_gap (модель «отпускает» слово сразу после
него, так два одинаковых подряд различимы). Другое слово — не раньше other_cooldown после засчитанного.

  .venv\\Scripts\\python detect.py файл.wav     — что услышано в записи
"""
import os
import sys
import wave

import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
MODEL = os.path.join(HERE, "models", "ssdfnc_words.onnx")
WORDS = ["VERITY", "LOVETY", "MOGGATY"]
CHUNK = 1280  # 80 мс


class WordDetector:
    def __init__(self, threshold=0.8, release=0.3, repeat_gap=0.35, other_cooldown=0.35, model=MODEL):
        import onnxruntime as ort
        from embed import CLIP, Features
        self.features = Features(threads=2)
        self.window = np.zeros(CLIP, np.int16)  # последние 2 с звука
        self.model = ort.InferenceSession(model, providers=["CPUExecutionProvider"])
        self.threshold = threshold
        self.release = release                # то же слово снова — после того, как вероятность упадёт ниже
        self.repeat_gap = repeat_gap          # с: и не раньше (на длинном moggaty вероятность проседает посреди слова)
        self.other_cooldown = other_cooldown  # с: другое слово не раньше (на стыке слов вероятности дрожат)
        self.t = 0.0
        self.pending = np.zeros(0, np.int16)
        self.armed = [True] * 3               # слово можно засчитать: с прошлого раза его вероятность падала
        self.last = [-9.0] * 3                # когда слово засчитано
        self.last_any = -9.0
        self.probs = np.zeros(4, np.float32)  # последние вероятности (для индикатора)

    def feed(self, audio):
        """int16 любой длины → [(СЛОВО, время с начала, вероятность)]"""
        audio = np.concatenate([self.pending, np.asarray(audio, np.int16)])
        n = len(audio) // CHUNK * CHUNK
        self.pending = audio[n:]
        found = []
        for i in range(0, n, CHUNK):
            self.window = np.concatenate([self.window[CHUNK:], audio[i:i + CHUNK]])
            self.t += CHUNK / 16000
            x = self.features.embed(self.window[None])
            self.probs = self.model.run(None, {"embeddings": x})[0][0]
            for k in range(3):
                if self.probs[k + 1] < self.release and self.t - self.last[k] >= self.repeat_gap:
                    self.armed[k] = True
            c = int(self.probs[1:].argmax())
            p = float(self.probs[c + 1])
            if p >= self.threshold and self.armed[c] and self.t - self.last_any >= self.other_cooldown:
                self.armed[c] = False
                self.last[c] = self.t
                self.last_any = self.t
                found.append((WORDS[c], self.t, p))
        return found


def read_wav(path):
    with wave.open(path, "rb") as w:
        if w.getnchannels() != 1 or w.getsampwidth() != 2 or w.getframerate() != 16000:
            sys.exit("Нужен WAV 16 кГц, моно, 16 бит")
        return np.frombuffer(w.readframes(w.getnframes()), np.int16)


def detect_file(path, **kw):
    det = WordDetector(**kw)
    audio = read_wav(path)
    found = det.feed(audio)
    found += det.feed(np.zeros(CHUNK * 12, np.int16))  # хвост тишины: последнему слову есть где «доиграть»
    return found


if __name__ == "__main__":
    for word, t, p in detect_file(sys.argv[1]):
        print(f"{t:6.2f} с  {word}  {p:.2f}")
