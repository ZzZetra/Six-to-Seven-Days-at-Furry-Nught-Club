"""Сгенерировать примеры для обучения и посчитать их признаки.

  .venv\\Scripts\\python generate.py [--per-word 3000] [--workers 6]
  .venv\\Scripts\\python generate.py --add late,cut   — догенерировать только эти виды и дописать к data/*.npz

Что делает:
  1. Piper говорит каждое из трёх слов (texts.TARGETS) много раз: разные голоса, скорость, интонация. Иногда
     перед словом — другое слово из тройки (так модель учится узнавать слова и подряд).
  2. «Не команда»: похожие слова-ловушки (texts.TRAPS), обычные фразы по-английски и по-русски, шум, тишина.
     И само слово, но не вовремя: сказано давно (late, кончилось 0.55–1.5 с назад) или ещё не досказано (cut,
     оборвано на середине) — так модель «отпускает» слово сразу после него, и два одинаковых подряд различимы.
  3. Искажения: шум разного цвета, эхо комнаты, «плохой микрофон» (срез низов и верхов), громкость.
  4. Признаки — как в openWakeWord: мел-спектр → эмбеддинги по 96 чисел на каждые 80 мс. Пример — последние
     16 эмбеддингов (≈2 с звука); слово заканчивается за 0.05–0.4 с до конца примера.
  5. Часть голосов откладывается для проверки (их нет в обучении): data/train.npz и data/val.npz.

Звук примеров не хранится (только признаки), примеры для прослушивания — в data/listen.
"""
import argparse
import os
import random
import sys
import time
import wave
from concurrent.futures import ProcessPoolExecutor

import numpy as np
from scipy.signal import butter, fftconvolve, resample_poly, sosfilt

import texts
from embed import CLIP, FRAMES, Features  # noqa: F401 (CLIP, FRAMES — и для place())

HERE = os.path.dirname(os.path.abspath(__file__))
VOICES = os.path.join(HERE, "voices")
FEATURES = os.path.join(HERE, "features")
DATA = os.path.join(HERE, "data")
RATE = 16000

# Голоса: файл, язык, сколько у него дикторов, какие дикторы отложены для проверки
VOICE_SPECS = [
    ("en_US-libritts_r-medium", "en", 904, lambda s: s >= 860),
    ("en_US-l2arctic-medium", "en", 24, lambda s: s >= 21),
    ("ru_RU-irina-medium", "ru", 1, lambda s: False),
    ("ru_RU-denis-medium", "ru", 1, lambda s: False),
    ("ru_RU-ruslan-medium", "ru", 1, lambda s: False),
    ("ru_RU-dmitri-medium", "ru", 1, lambda s: True),   # весь — для проверки
]
VOICE_WEIGHTS = [0.62, 0.18, 0.05, 0.05, 0.05, 0.05]

_voices = {}


def voice(name):
    if name not in _voices:
        from piper import PiperVoice
        _voices[name] = PiperVoice.load(os.path.join(VOICES, name + ".onnx"))
    return _voices[name]


def synth(name, text, speaker, rng):
    """Текст → float32 16 кГц, тишина по краям обрезана"""
    from piper import SynthesisConfig
    v = voice(name)
    cfg = SynthesisConfig(speaker_id=speaker if v.config.num_speakers > 1 else None,
                          length_scale=rng.uniform(0.8, 1.3), noise_scale=rng.uniform(0.4, 0.9),
                          noise_w_scale=rng.uniform(0.5, 1.0))
    audio = np.concatenate([c.audio_float_array for c in v.synthesize(text, syn_config=cfg)])
    audio = resample_poly(audio, RATE, v.config.sample_rate).astype(np.float32)
    return trim(audio)


def trim(x, thr=0.02):
    idx = np.nonzero(np.abs(x) > thr * max(1e-6, np.abs(x).max()))[0]
    return x[max(0, idx[0] - 400):idx[-1] + 800] if len(idx) else x


# ── Искажения ─────────────────────────────────────────────────────────────────

def colored_noise(n, rng):
    white = rng.standard_normal(n).astype(np.float32)
    kind = rng.random()
    if kind < 0.33:
        return white
    spec = np.fft.rfft(white)
    f = np.maximum(np.arange(len(spec)), 1)
    spec /= np.sqrt(f) if kind < 0.66 else f  # розовый или коричневый
    x = np.fft.irfft(spec, n).astype(np.float32)
    return x / (np.abs(x).max() + 1e-6)


def room(x, rng):
    rt60 = rng.uniform(0.15, 0.7)
    n = int(rt60 * RATE)
    t = np.arange(n) / RATE
    ir = rng.standard_normal(n) * np.exp(-6.9 * t / rt60)
    ir[0] = 1.0
    y = fftconvolve(x, ir.astype(np.float32))[:len(x)]
    return y / (np.abs(y).max() + 1e-6) * np.abs(x).max()


def mic(x, rng):
    if rng.random() < 0.5:
        x = sosfilt(butter(2, rng.uniform(80, 350), "highpass", fs=RATE, output="sos"), x)
    if rng.random() < 0.5:
        x = sosfilt(butter(2, rng.uniform(3000, 7500), "lowpass", fs=RATE, output="sos"), x)
    return x.astype(np.float32)


def augment(x, rng):
    if rng.random() < 0.4:
        x = room(x, rng)
    x = mic(x, rng)
    peak = 10 ** (rng.uniform(-24, -1) / 20)
    x = x / (np.abs(x).max() + 1e-6) * peak
    if rng.random() < 0.75:
        snr = rng.uniform(3, 35)
        noise = colored_noise(len(x), rng)
        p_sig = np.mean(x ** 2) + 1e-9
        noise *= np.sqrt(p_sig / (10 ** (snr / 10)) / (np.mean(noise ** 2) + 1e-9))
        x = x + noise
    return np.clip(x, -1, 1).astype(np.float32)


def place(parts, rng, end_gap):
    """Склеить куски речи с паузами и поставить так, чтобы последний кончался за end_gap с до конца клипа"""
    gaps = [np.zeros(int(rng.uniform(0.0, 0.25) * RATE), np.float32) for _ in parts]
    speech = np.concatenate([p for pair in zip(gaps, parts) for p in pair])
    tail = np.zeros(int(end_gap * RATE), np.float32)
    x = np.concatenate([speech, tail])[-CLIP:]
    return np.concatenate([np.zeros(CLIP - len(x), np.float32), x])


def pick_voice(rng, val):
    """Голос и диктор: для обучения — из обучающих дикторов, для проверки — из отложенных"""
    for _ in range(1000):
        i = rng.choices(range(len(VOICE_SPECS)), VOICE_WEIGHTS)[0]
        name, lang, n, held = VOICE_SPECS[i]
        s = rng.randrange(n)
        if held(s) == val:
            return name, lang, s
    raise RuntimeError("нет дикторов")


# ── Один пример ───────────────────────────────────────────────────────────────

def make_example(task):
    kind, label, seed, val = task
    rng = random.Random(seed)
    nprng = np.random.default_rng(seed)
    name, lang, spk = pick_voice(rng, val)
    if kind == "word":
        word = texts.WORDS[label - 1]
        parts = [synth(name, rng.choice(texts.TARGETS[word][lang]), spk, rng)]
        if rng.random() < 0.5:  # перед ним 1–2 других слова тройки (тот же голос)
            for _ in range(rng.choice([1, 1, 2])):
                w = rng.choice(texts.WORDS)
                parts.insert(0, synth(name, rng.choice(texts.TARGETS[w][lang]), spk, rng))
        x = place(parts, rng, rng.uniform(0.05, 0.4))
    elif kind == "trap":
        parts = [synth(name, rng.choice(texts.TRAPS[lang]), spk, rng)]
        if rng.random() < 0.3:
            w = rng.choice(texts.WORDS)
            parts.insert(0, synth(name, rng.choice(texts.TARGETS[w][lang]), spk, rng))  # слово, потом ловушка
        x = place(parts, rng, rng.uniform(0.05, 0.5))
    elif kind == "speech":
        s = synth(name, texts.sentence(lang, rng), spk, rng)
        start = rng.randrange(max(1, len(s) - CLIP // 2))  # любой кусок фразы
        x = place([s[start:start + CLIP]], rng, rng.uniform(0.0, 0.6))
    elif kind == "late":  # слово кончилось давно: после него тишина или кусок обычной речи
        parts = [synth(name, rng.choice(texts.TARGETS[w][lang]), spk, rng) for w in rng.sample(texts.WORDS, rng.choice([1, 1, 2]))]
        gap = rng.uniform(0.55, 1.5)
        if rng.random() < 0.4:
            s = synth(name, texts.sentence(lang, rng), spk, rng)
            piece = s[:int(rng.uniform(0.15, gap - 0.1) * RATE)]
            parts.append(np.concatenate([np.zeros(int((gap - len(piece) / RATE) * RATE), np.float32), piece]))
            gap = 0.0
        x = place(parts, rng, gap)
    elif kind == "cut":  # слово ещё не досказано (часто — после другого слова тройки)
        w = rng.choice(texts.WORDS)
        word = synth(name, rng.choice(texts.TARGETS[w][lang]), spk, rng)
        parts = [word[:int(len(word) * rng.uniform(0.35, 0.75))]]
        if rng.random() < 0.6:
            parts.insert(0, synth(name, rng.choice(texts.TARGETS[rng.choice(texts.WORDS)][lang]), spk, rng))
        x = place(parts, rng, 0.0)
    else:  # шум или тишина
        x = np.zeros(CLIP, np.float32) + 1e-4 * nprng.standard_normal(CLIP).astype(np.float32)
    x = augment(x, nprng)
    return (np.clip(x * 32767, -32768, 32767).astype(np.int16), label, kind)


def save_listen(examples, folder, count=12):
    os.makedirs(folder, exist_ok=True)
    for i, (x, label, kind) in enumerate(examples[:count]):
        name = texts.WORDS[label - 1] if label else kind
        with wave.open(os.path.join(folder, f"{i:02d}_{name}.wav"), "wb") as w:
            w.setnchannels(1); w.setsampwidth(2); w.setframerate(RATE); w.writeframes(x.tobytes())


def build(tasks, workers, feats, label):
    t0 = time.time()
    examples = []
    with ProcessPoolExecutor(workers) as pool:
        for e in pool.map(make_example, tasks, chunksize=16):
            examples.append(e)
            if len(examples) % 1000 == 0:
                print(f"{label}: {len(examples)} / {len(tasks)} за {time.time() - t0:.0f} с", flush=True)
    print(f"{label}: синтез {len(examples)} примеров за {time.time() - t0:.0f} с", flush=True)
    t0 = time.time()
    X = []
    for i in range(0, len(examples), 256):
        X.append(feats.embed(np.stack([e[0] for e in examples[i:i + 256]])))
    X = np.concatenate(X).astype(np.float16)
    y = np.array([e[1] for e in examples], np.int64)
    kinds = np.array([e[2] for e in examples])
    print(f"{label}: признаки {X.shape} за {time.time() - t0:.0f} с", flush=True)
    return X, y, kinds, examples


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--per-word", type=int, default=3000, help="примеров каждого слова (обучение)")
    ap.add_argument("--workers", type=int, default=max(1, (os.cpu_count() or 2) - 2))
    ap.add_argument("--seed", type=int, default=1)
    ap.add_argument("--add", help="только эти виды через запятую (late,cut…), дописать к готовым data/*.npz")
    args = ap.parse_args()
    os.makedirs(DATA, exist_ok=True)
    feats = Features()

    def tasks(n_word, val, base):
        t = []
        for label in (1, 2, 3):
            t += [("word", label, base + label * 1_000_000 + i, val) for i in range(n_word)]
        t += [("trap", 0, base + 5_000_000 + i, val) for i in range(n_word)]
        t += [("speech", 0, base + 6_000_000 + i, val) for i in range(n_word * 3)]
        t += [("noise", 0, base + 7_000_000 + i, val) for i in range(n_word // 4)]
        t += [("late", 0, base + 8_000_000 + i, val) for i in range(n_word)]
        t += [("cut", 0, base + 9_000_000 + i, val) for i in range(n_word)]
        return t if not args.add else [x for x in t if x[0] in args.add.split(",")]

    for split, n, val in (("train", args.per_word, False), ("val", max(200, args.per_word // 6), True)):
        X, y, kinds, ex = build(tasks(n, val, args.seed * 10_000_000 + (1 if val else 0) * 100_000_000), args.workers, feats, split)
        path = os.path.join(DATA, f"{split}.npz")
        if args.add:
            old = np.load(path)
            X, y, kinds = (np.concatenate([old["X"], X]), np.concatenate([old["y"], y]),
                           np.concatenate([old["kinds"], kinds]))
            split += "_add"
        np.savez_compressed(path, X=X, y=y, kinds=kinds)
        random.Random(0).shuffle(ex)
        save_listen(ex, os.path.join(DATA, "listen", split))
        print(f"{split}: сохранено, классы {np.bincount(y, minlength=4).tolist()}")


if __name__ == "__main__":
    sys.exit(main())
