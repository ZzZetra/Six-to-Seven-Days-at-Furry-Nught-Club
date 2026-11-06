"""Звуки генератора: запуск, работа (бесшовная петля) и глушение из двух записей.

  ..\\SpeechTrain\\.venv\\Scripts\\python generator_sounds.py [папка с исходниками]

Исходники (по умолчанию из «Загрузок»):
  the-generator-turns-on-and-works-15-min.mp3 — отсюда ЗАПУСК: щелчок, стартер, раскрутка (первые ~1.5 с);
  diesel-generator.mp3                       — отсюда РАБОТА (3.5–34.5 с) и ГЛУШЕНИЕ (с 36 с до конца).

Что получается (Assets/StreamingAssets/Sounds — оттуда их берёт игра, GameSounds):
  генератор запуск.wav   — запуск; его хвост плавно переходит в конец петли, поэтому сразу после него петлю
                           можно играть с начала — стык не слышен;
  генератор работа.wav   — работа, бесшовная петля ~31 с (конец переходит в начало через 1 с наплыва);
  генератор глушение.wav — глушение; начинается с той же работы, поэтому петлю в него сводят наплывом ~0.3 с;
  Tools/Audio/preview/generator_preview.wav — для прослушивания: запуск → 10 с работы → глушение (не в игре).

Записи сняты разными микрофонами: у первой больше баса, у второй — верхов. Запуск подгоняется по тембру к
работе (эквалайзер по среднему спектру, не больше ±MATCH_LIMIT дБ), иначе на стыке «меняется мотор».
"""
import os
import sys

import numpy as np
import soundfile as sf
from scipy.signal import fftconvolve

HERE = os.path.dirname(os.path.abspath(__file__))
SRC = sys.argv[1] if len(sys.argv) > 1 else os.path.join(os.path.expanduser("~"), "Downloads")
OUT = os.path.join(HERE, "..", "..", "Assets", "StreamingAssets", "Sounds")
PREVIEW = os.path.join(HERE, "preview")
START_FILE = "the-generator-turns-on-and-works-15-min.mp3"
RUN_FILE = "diesel-generator.mp3"

START_END = 1.5        # с: здесь запуск уже ровно работает
START_FADE = 1.0       # с: наплыв запуска в работу
LOOP_FROM = 3.5        # с в diesel-generator: ровная работа
LOOP_TO = 34.5         # с: конец петли (уточняется по совпадению волны ±LOOP_SEARCH)
LOOP_SEARCH = 0.1
LOOP_FADE = 1.0        # с: наплыв конца петли в начало
STOP_FROM = 36.0       # с: глушение (обороты падают с ~37 с)
MATCH_LIMIT = 9.0      # дБ: предел подгонки тембра запуска
PREVIEW_RUN = 10.0     # с работы в прослушке
PREVIEW_FADE = 0.3


def load(name):
    """mp3 → float32 (n, 2); у этих файлов битый последний кадр — читаем, пока читается"""
    f = sf.SoundFile(os.path.join(SRC, name))
    blocks = []
    try:
        while True:
            b = f.read(f.samplerate // 4, dtype="float32", always_2d=True)
            if not len(b):
                break
            blocks.append(b)
    except sf.LibsndfileError:
        pass
    x = np.concatenate(blocks)
    if x.shape[1] == 1:
        x = np.repeat(x, 2, axis=1)
    return x, f.samplerate


def fade(n, out=False):
    """равномощный наплыв: sin² + cos² = 1 (шум мотора не проседает на стыке)"""
    t = np.linspace(0, np.pi / 2, n, dtype=np.float32)[:, None]
    return np.cos(t) if out else np.sin(t)


def rms(x):
    return float(np.sqrt(np.mean(x ** 2)) + 1e-9)


def avg_spectrum(x, n=4096):
    m = x.mean(1)
    frames = [np.abs(np.fft.rfft(m[i:i + n] * np.hanning(n))) for i in range(0, len(m) - n, n // 2)]
    return np.mean(frames, axis=0)


def match_filter(src, dst, sr, n=4096):
    """КИХ-фильтр, который делает средний спектр src похожим на dst (сглажено по третям октавы)"""
    a, b = avg_spectrum(src, n), avg_spectrum(dst, n)
    f = np.fft.rfftfreq(n, 1 / sr)
    gain = np.ones_like(f)
    for i, fi in enumerate(f):
        band = (f >= fi / 2 ** (1 / 6)) & (f <= fi * 2 ** (1 / 6))
        if band.any():
            gain[i] = np.sqrt(np.sum(b[band] ** 2) / (np.sum(a[band] ** 2) + 1e-12))
    gain = np.clip(gain, 10 ** (-MATCH_LIMIT / 20), 10 ** (MATCH_LIMIT / 20))
    ir = np.fft.fftshift(np.fft.irfft(gain, n)) * np.hanning(n)
    return ir.astype(np.float32)


def apply_fir(x, ir):
    y = np.stack([fftconvolve(x[:, c], ir)[len(ir) // 2:len(ir) // 2 + len(x)] for c in range(x.shape[1])], 1)
    return y.astype(np.float32)


def limit(x, sr, ceil=0.89):
    """ограничитель пиков: щелчок стартера после подгонки тембра упирается в потолок — прижать только его"""
    from scipy.ndimage import maximum_filter1d
    peak = maximum_filter1d(np.abs(x).max(1), int(0.005 * sr))
    g = np.minimum(1.0, ceil / (peak + 1e-9))
    att, rel = np.exp(-1 / (0.002 * sr)), np.exp(-1 / (0.08 * sr))
    for i in range(len(g) - 2, -1, -1):  # прижимает заранее, за ~2 мс до пика
        g[i] = min(g[i], g[i + 1] * att + (1 - att))
    for i in range(1, len(g)):           # отпускает за ~80 мс
        g[i] = min(g[i], g[i - 1] * rel + (1 - rel))
    return (x * g[:, None]).astype(np.float32)


def best_loop_end(x, sr):
    """конец петли там, где волна больше всего похожа на начало (меньше «биений» в наплыве)"""
    a = int(LOOP_FROM * sr)
    win = x[a:a + int(0.2 * sr)].mean(1)
    best, best_c = int(LOOP_TO * sr), -2.0
    for b in range(int((LOOP_TO - LOOP_SEARCH) * sr), int((LOOP_TO + LOOP_SEARCH) * sr), 8):
        seg = x[b:b + len(win)].mean(1)
        c = float(np.dot(win, seg) / (np.linalg.norm(win) * np.linalg.norm(seg) + 1e-9))
        if c > best_c:
            best, best_c = b, c
    return best, best_c


def save(name, x, sr, folder=OUT):
    os.makedirs(folder, exist_ok=True)
    path = os.path.join(folder, name)
    sf.write(path, np.clip(x, -1, 1), sr, subtype="PCM_16")
    print(f"{name:24s} {len(x) / sr:6.2f} с   {20 * np.log10(rms(x)):.1f} дБ")


def main():
    run, sr = load(RUN_FILE)
    start_src, sr2 = load(START_FILE)
    assert sr == sr2 == 44100, (sr, sr2)

    # петля работы
    a = int(LOOP_FROM * sr)
    b, corr = best_loop_end(run, sr)
    F = int(LOOP_FADE * sr)
    loop = run[a:b].copy()
    loop[:F] = run[b:b + F] * fade(F, out=True) + run[a:a + F] * fade(F)
    print(f"петля {LOOP_FROM:.2f}–{b / sr:.3f} с, похожесть волны на стыке {corr:.2f}")

    # запуск: подогнать тембр и громкость к работе, хвост — наплывом в конец петли
    ref = run[a:b]
    steady = start_src[int(START_END * sr):int(START_END * sr) + 20 * sr]  # ровная работа второй записи
    ir = match_filter(steady, ref, sr)
    start_eq = apply_fir(start_src[:int((START_END + START_FADE) * sr) + len(ir)], ir)
    start_eq *= rms(ref) / rms(apply_fir(steady, ir))
    e, G = int(START_END * sr), int(START_FADE * sr)
    start = start_eq[:e + G].copy()
    start[e:] = start_eq[e:e + G] * fade(G, out=True) + loop[len(loop) - G:] * fade(G)
    start[:e] = limit(start[:e], sr)  # только сам запуск: хвост с петлёй не трогать, иначе стык не сойдётся
    start[:64] *= np.linspace(0, 1, 64, dtype=np.float32)[:, None]

    # глушение: с ровной работы до тишины
    s = int(STOP_FROM * sr)
    tail = run[s:]
    loud = np.nonzero(np.abs(tail).max(1) > 10 ** (-70 / 20))[0]
    stop = tail[:loud[-1] + 1].copy()
    k = int(0.02 * sr)
    stop[:k] *= np.linspace(0, 1, k, dtype=np.float32)[:, None]
    stop[-k:] *= np.linspace(1, 0, k, dtype=np.float32)[:, None]

    # прослушка: запуск → петля (стык без наплыва — так и будет в игре) → наплыв в глушение
    P, H = int(PREVIEW_RUN * sr), int(PREVIEW_FADE * sr)
    body = np.concatenate([start, np.tile(loop, (P // len(loop) + 2, 1))[:P]])
    preview = np.concatenate([body[:-H], body[-H:] * fade(H, out=True) + stop[:H] * fade(H), stop[H:]])

    save("генератор запуск.wav", start, sr)
    save("генератор работа.wav", loop, sr)
    save("генератор глушение.wav", stop, sr)
    save("generator_preview.wav", preview, sr, PREVIEW)
    print("→", os.path.normpath(OUT))


if __name__ == "__main__":
    sys.exit(main())
