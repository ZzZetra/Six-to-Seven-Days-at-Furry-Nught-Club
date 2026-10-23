"""SSDFNC — тест распознавания слов с микрофона (Vosk, офлайн, русский).

Задача: ловить КОМБИНАЦИИ из трёх слов (verity, lovety, moggaty) — в игре это будет «мониторный вирус»:
на мониторе код из трёх слов, его надо произнести.

Запуск:   run.bat                         (окно)
          run.bat --wav f.wav [--lang en|ru] --variant words|phrase|free [--window 4]   (запись 16 кГц моно)
          run.bat --selftest              (проверить, что всё грузится, окно закроется само)

Команды и синонимы. «verity: верити, верите» — команда VERITY срабатывает, если модель услышала
«верити» или «верите». Синонимы — то, что русская модель на самом деле слышит на выдуманном слове.

Варианты распознавания комбинации:
  words  — «По словам»: модель ищет только синонимы и служебные слова; тройка собирается из слов,
           сказанных в пределах окна (можно с паузами).
  phrase — «Фразой»: модель знает все тройки целиком (плюс одиночные слова и служебные, чтобы не
           выдумывать тройку из обрывка). Точнее на одном дыхании.
  free   — «Свободная речь + поиск»: распознаёт всё подряд, тройку ищет в тексте.
"""
import argparse
import itertools
import json
import os
import queue
import random
import sys
import threading
import time
import wave

from vosk import KaldiRecognizer, Model, SetLogLevel

HERE = os.path.dirname(os.path.abspath(__file__))
RATE = 16000
BLOCK = 3200  # 0.2 с за блок

# Язык: модель, команды с синонимами, служебные слова. Синонимы подобраны на синтезированной речи
# (см. README): это то, что модель на самом деле слышит на выдуманных словах
PRESETS = {
    "ru": {
        "title": "Русский",
        "model": "vosk-model-small-ru-0.22",
        "commands": """verity: верити, верите, верить
lovety: ловите, лови ты, ловить
moggaty: мага те, мага ты, мага ти, моги ты, магазин""",
        "fillers": "а и у к в на по уже это ну да нет вот там он она идёт что как так",
    },
    "en": {
        "title": "English",
        "model": "vosk-model-small-en-us-0.15",
        "commands": """verity: verity, variety
lovety: love eddie, love eddy, levity, lovie, love it, lovely
moggaty: margaret, marguerite, margaret he, mog eddy, mog he, mog a tea, mccarty, mccarthy""",
        "fillers": "a the and to of in it is you that i he she we they this what so oh yeah no yes okay",
    },
}


def model_dir(lang):
    return os.path.join(HERE, "models", PRESETS[lang]["model"])


DEFAULT_LANG = "en"  # английский: на тестах точнее (см. README)

VARIANTS = {"words": "По словам", "phrase": "Фразой", "free": "Свободная речь + поиск"}

SetLogLevel(-1)


# ── Разбор команд и грамматики ────────────────────────────────────────────────

def parse_commands(text):
    """«name: синоним, синоним» по строкам → {NAME: [синонимы]}"""
    commands = {}
    for line in text.splitlines():
        line = line.strip()
        if not line:
            continue
        if ":" in line:
            name, rest = line.split(":", 1)
            aliases = [a.strip().lower() for a in rest.split(",") if a.strip()]
        else:
            name, aliases = line, [line.lower()]
        commands[name.strip().upper()] = aliases
    return commands


def all_combos(commands, distinct):
    names = list(commands)
    return [c for c in itertools.product(names, repeat=3) if not distinct or len(set(c)) == 3]


def grammar_for(variant, commands, fillers, distinct):
    """Список фраз для грамматики Vosk или None (свободная речь)."""
    if variant == "free":
        return None
    singles = sorted({a for aliases in commands.values() for a in aliases})
    phrases = list(singles)
    if variant == "phrase":
        combo_phrases = set()
        for combo in all_combos(commands, distinct):
            for parts in itertools.product(*(commands[n] for n in combo)):
                combo_phrases.add(" ".join(parts))
        phrases = sorted(combo_phrases) + phrases
    return phrases + [f for f in fillers if f not in phrases] + ["[unk]"]


def make_recognizer(model, phrases):
    rec = KaldiRecognizer(model, RATE, json.dumps(phrases, ensure_ascii=False)) if phrases else KaldiRecognizer(model, RATE)
    rec.SetWords(True)
    return rec


def extract_sequence(words, commands):
    """Слова Vosk (уже прошедшие порог) → [(КОМАНДА, время конца слова)] по порядку речи.
    Многословные синонимы («мага те») ищутся раньше коротких."""
    aliases = sorted(((a.split(), name) for name, al in commands.items() for a in al), key=lambda x: -len(x[0]))
    tokens = [w["word"] for w in words]
    seq, i = [], 0
    while i < len(tokens):
        for parts, name in aliases:
            if tokens[i:i + len(parts)] == parts:
                seq.append((name, words[i + len(parts) - 1].get("end", 0.0)))
                i += len(parts)
                break
        else:
            i += 1
    return seq


class ComboTracker:
    """Собирает тройки из слов, сказанных не дальше window секунд друг от друга."""

    def __init__(self, window):
        self.window = window
        self.buf = []

    def feed(self, seq):
        done = []
        for name, t in seq:
            self.buf = [(n, tt) for n, tt in self.buf if t - tt <= self.window]
            self.buf.append((name, t))
            if len(self.buf) == 3:
                done.append(tuple(n for n, _ in self.buf))
                self.buf = []
        return done

    def pending(self):
        return [n for n, _ in self.buf]

    def reset(self):
        self.buf = []


def split_result(result_json, min_conf):
    """(весь текст, принятые слова-словари, отброшенные, средняя уверенность)"""
    res = json.loads(result_json)
    words = res.get("result", [])
    accepted = [w for w in words if w.get("conf", 1.0) >= min_conf and w["word"] != "[unk]"]
    rejected = [f'{w["word"]}({w.get("conf", 0):.2f})' for w in words if w.get("conf", 1.0) < min_conf]
    conf = sum(w.get("conf", 1.0) for w in words) / len(words) if words else 0.0
    return res.get("text", ""), accepted, rejected, conf


# ── Распознать файл (без окна) ────────────────────────────────────────────────

def recognize_file(path, variant, commands, fillers, distinct, window, min_conf, model_path):
    model = Model(model_path)
    rec = make_recognizer(model, grammar_for(variant, commands, fillers, distinct))
    tracker = ComboTracker(window)
    results = []
    with wave.open(path, "rb") as wf:
        if wf.getnchannels() != 1 or wf.getsampwidth() != 2 or wf.getframerate() != RATE:
            sys.exit("Нужен WAV 16 кГц, моно, 16 бит")
        while True:
            data = wf.readframes(BLOCK)
            if not data:
                break
            if rec.AcceptWaveform(data):
                results.append(rec.Result())
        results.append(rec.FinalResult())
    combos = []
    for r in results:
        text, accepted, rejected, conf = split_result(r, min_conf)
        if not text:
            continue
        seq = extract_sequence(accepted, commands)
        found = tracker.feed(seq)
        combos += found
        print(f"  [{conf:.2f}] «{text}» → {' '.join(n for n, _ in seq) or '—'}"
              + (f"   ТРОЙКА: {' · '.join(found[-1])}" if found else ""))
    return combos


# ── Окно ──────────────────────────────────────────────────────────────────────

def run_gui(selftest=False):
    import tkinter as tk
    from tkinter import filedialog, ttk
    import sounddevice as sd

    root = tk.Tk()
    root.title("SSDFNC — тест распознавания речи (Vosk)")
    root.geometry("1100x760")

    state = {"models": {}, "model_dir": model_dir(DEFAULT_LANG), "lang": DEFAULT_LANG, "stream": None, "running": False,
             "texts": {k: (v["commands"], v["fillers"]) for k, v in PRESETS.items()},
             "audio": queue.Queue(), "ui": queue.Queue(), "level": 0.0, "commands": {},
             "tracker": ComboTracker(4.0), "target": None, "target_since": 0.0, "flash_until": 0.0,
             "stats": {"tries": 0, "ok": 0, "wrong": 0, "times": []}}

    # ── Микрофон ──
    top = ttk.Frame(root, padding=6)
    top.pack(fill="x")
    ttk.Label(top, text="Микрофон:").pack(side="left")
    inputs = [(i, d["name"]) for i, d in enumerate(sd.query_devices()) if d["max_input_channels"] > 0]
    mic = ttk.Combobox(top, width=46, state="readonly", values=[f"{i}: {n}" for i, n in inputs])
    try:
        mic.current([i for i, _ in inputs].index(sd.default.device[0]))
    except (ValueError, IndexError, TypeError):
        if inputs:
            mic.current(0)
    mic.pack(side="left", padx=4)
    start_btn = ttk.Button(top, text="▶ Слушать")
    start_btn.pack(side="left", padx=4)
    ttk.Label(top, text="   Язык:").pack(side="left")
    lang_var = tk.StringVar(value=DEFAULT_LANG)
    for key, preset in PRESETS.items():
        ttk.Radiobutton(top, text=preset["title"], variable=lang_var, value=key,
                        command=lambda: switch_lang()).pack(side="left", padx=2)
    model_lbl = ttk.Label(top, text="Модель: " + PRESETS[DEFAULT_LANG]["model"])
    model_lbl.pack(side="left", padx=8)
    ttk.Button(top, text="Модель…", command=lambda: choose_model()).pack(side="left")

    level = tk.Canvas(root, height=14, bg="#222")
    level.pack(fill="x", padx=6)

    # ── Вариант распознавания и задание ──
    opts = ttk.Frame(root, padding=(6, 6, 6, 0))
    opts.pack(fill="x")
    ttk.Label(opts, text="Вариант:").pack(side="left")
    variant_var = tk.StringVar(value="words")
    for key, title in VARIANTS.items():
        ttk.Radiobutton(opts, text=title, variable=variant_var, value=key).pack(side="left", padx=4)
    ttk.Label(opts, text="   Окно, с:").pack(side="left")
    window_var = tk.DoubleVar(value=4.0)
    ttk.Spinbox(opts, from_=1.0, to=10.0, increment=0.5, width=5, textvariable=window_var).pack(side="left")
    fillers_var = tk.BooleanVar(value=True)
    ttk.Checkbutton(opts, text="служебные слова", variable=fillers_var).pack(side="left", padx=8)

    opts2 = ttk.Frame(root, padding=(6, 4, 6, 6))
    opts2.pack(fill="x")
    ttk.Label(opts2, text="Задание:").pack(side="left")
    task_var = tk.StringVar(value="virus")
    ttk.Radiobutton(opts2, text="Код вируса", variable=task_var, value="virus",
                    command=lambda: new_target()).pack(side="left", padx=4)
    ttk.Radiobutton(opts2, text="Любая тройка", variable=task_var, value="any",
                    command=lambda: new_target()).pack(side="left", padx=4)
    distinct_var = tk.BooleanVar(value=False)  # по умолчанию повторы разрешены: все 27 кодов
    ttk.Checkbutton(opts2, text="без повторов", variable=distinct_var,
                    command=lambda: new_target()).pack(side="left", padx=8)
    ttk.Button(opts2, text="Новый код", command=lambda: new_target()).pack(side="left", padx=4)
    ttk.Label(opts2, text="   Мин. уверенность:").pack(side="left")
    conf_var = tk.DoubleVar(value=0.5)
    conf_lbl = ttk.Label(opts2, text="0.50", width=5)
    ttk.Scale(opts2, from_=0.0, to=1.0, variable=conf_var, length=140,
              command=lambda v: conf_lbl.config(text=f"{float(v):.2f}")).pack(side="left")
    conf_lbl.pack(side="left")

    body = ttk.Frame(root, padding=6)
    body.pack(fill="both", expand=True)

    # ── Слева: команды, служебные слова, статистика ──
    left = ttk.Frame(body)
    left.pack(side="left", fill="y")
    ttk.Label(left, text="Команды  (имя: синоним, синоним):").pack(anchor="w")
    cmd_txt = tk.Text(left, width=34, height=6, font=("Segoe UI", 11))
    cmd_txt.insert("1.0", PRESETS[DEFAULT_LANG]["commands"])
    cmd_txt.pack(fill="x")
    ttk.Label(left, text="Служебные слова (через пробел):").pack(anchor="w", pady=(6, 0))
    fill_txt = tk.Text(left, width=34, height=3, font=("Segoe UI", 10), wrap="word")
    fill_txt.insert("1.0", PRESETS[DEFAULT_LANG]["fillers"])
    fill_txt.pack(fill="x")
    ttk.Button(left, text="Применить", command=lambda: restart()).pack(fill="x", pady=4)
    ttk.Label(left, text="Статистика:").pack(anchor="w")
    stats_txt = tk.Text(left, width=34, height=7, font=("Segoe UI", 11), state="disabled")
    stats_txt.pack(fill="x")
    ttk.Button(left, text="Сбросить статистику", command=lambda: reset_stats()).pack(fill="x", pady=4)

    # ── Справа: код, прогресс, журнал ──
    right = ttk.Frame(body)
    right.pack(side="left", fill="both", expand=True, padx=(8, 0))
    target_lbl = tk.Label(right, text="", font=("Segoe UI", 30, "bold"), fg="#b0249a")
    target_lbl.pack(anchor="w", fill="x")
    progress_lbl = tk.Label(right, text="", font=("Segoe UI", 22), fg="#555")
    progress_lbl.pack(anchor="w", fill="x")
    result_lbl = tk.Label(right, text="", font=("Segoe UI", 26, "bold"), fg="#888")
    result_lbl.pack(anchor="w", fill="x")
    partial = ttk.Label(right, text="…", font=("Segoe UI", 12), foreground="#777")
    partial.pack(anchor="w", fill="x")
    log = tk.Text(right, font=("Consolas", 11), wrap="word")
    log.pack(fill="both", expand=True)
    log.tag_config("hit", foreground="#0a7d2c", font=("Consolas", 11, "bold"))
    log.tag_config("bad", foreground="#c0392b", font=("Consolas", 11, "bold"))
    log.tag_config("dim", foreground="#999")
    status = ttk.Label(root, text="Готово", anchor="w", padding=4)
    status.pack(fill="x")

    # ── Логика ──
    def load_model():
        path = state["model_dir"]
        if path not in state["models"]:
            status.config(text="Загружаю модель…")
            root.update_idletasks()
            state["models"][path] = Model(path)
        return state["models"][path]

    def choose_model():
        path = filedialog.askdirectory(initialdir=os.path.join(HERE, "models"), title="Папка модели Vosk")
        if path:
            stop()
            state["model_dir"] = path
            model_lbl.config(text="Модель: " + os.path.basename(path))

    # Язык: своя модель и свои синонимы; правки в полях у каждого языка сохраняются отдельно
    def switch_lang():
        new = lang_var.get()
        if new == state["lang"]:
            return
        was_running = state["running"]
        stop()
        state["texts"][state["lang"]] = (cmd_txt.get("1.0", "end").strip(), fill_txt.get("1.0", "end").strip())
        state["lang"] = new
        commands_text, fillers_text = state["texts"][new]
        cmd_txt.delete("1.0", "end")
        cmd_txt.insert("1.0", commands_text)
        fill_txt.delete("1.0", "end")
        fill_txt.insert("1.0", fillers_text)
        state["model_dir"] = model_dir(new)
        model_lbl.config(text="Модель: " + PRESETS[new]["model"])
        state["commands"] = parse_commands(commands_text)
        new_target()
        if was_running:
            start()

    def new_target():
        state["tracker"].reset()
        if task_var.get() == "virus" and state["commands"]:
            state["target"] = random.choice(all_combos(state["commands"], distinct_var.get()))
            target_lbl.config(text="КОД ВИРУСА:  " + " · ".join(state["target"]))
        else:
            state["target"] = None
            target_lbl.config(text="Скажи любые три слова подряд")
        state["target_since"] = time.time()
        draw_progress()

    def draw_progress():
        pend = state["tracker"].pending()
        progress_lbl.config(text="Слышу:  " + " · ".join(pend + ["_"] * (3 - len(pend))))

    def flash(text, color):
        result_lbl.config(text=text, fg=color)
        state["flash_until"] = time.time() + 2.0

    def draw_stats():
        s = state["stats"]
        avg = sum(s["times"]) / len(s["times"]) if s["times"] else 0.0
        lines = [f"Троек услышано: {s['tries']}", f"Верных: {s['ok']}", f"Неверных: {s['wrong']}"]
        if s["tries"]:
            lines.append(f"Точность: {100 * s['ok'] / s['tries']:.0f}%")
        if s["times"]:
            lines.append(f"Среднее время до успеха: {avg:.1f} с")
        stats_txt.config(state="normal")
        stats_txt.delete("1.0", "end")
        stats_txt.insert("1.0", "\n".join(lines))
        stats_txt.config(state="disabled")

    def reset_stats():
        state["stats"] = {"tries": 0, "ok": 0, "wrong": 0, "times": []}
        draw_stats()

    def on_combo(combo):
        s = state["stats"]
        s["tries"] += 1
        stamp = time.strftime("%H:%M:%S")
        if state["target"] is None:
            s["ok"] += 1
            flash("ТРОЙКА: " + " · ".join(combo), "#0a7d2c")
            log.insert("end", f"{stamp}  ТРОЙКА {' · '.join(combo)}\n", "hit")
        elif combo == state["target"]:
            took = time.time() - state["target_since"]
            s["ok"] += 1
            s["times"].append(took)
            flash(f"ВИРУС УДАЛЁН ✔  за {took:.1f} с", "#0a7d2c")
            log.insert("end", f"{stamp}  ВЕРНО {' · '.join(combo)} за {took:.1f} с\n", "hit")
            root.after(1500, new_target)
        else:
            s["wrong"] += 1
            flash("НЕВЕРНО ✖  " + " · ".join(combo), "#c0392b")
            log.insert("end", f"{stamp}  НЕВЕРНО {' · '.join(combo)} (нужно {' · '.join(state['target'])})\n", "bad")
        log.see("end")
        draw_stats()

    def audio_cb(indata, frames, t, st):
        data = bytes(indata)
        state["audio"].put(data)
        import array
        samples = array.array("h", data)
        if samples:
            peak = max(abs(min(samples)), abs(max(samples))) / 32768.0
            state["level"] = max(peak, state["level"] * 0.85)

    def worker(rec):
        while state["running"]:
            try:
                data = state["audio"].get(timeout=0.2)
            except queue.Empty:
                continue
            t0 = time.perf_counter()
            if rec.AcceptWaveform(data):
                state["ui"].put(("final", rec.Result(), time.perf_counter() - t0))
            else:
                state["ui"].put(("partial", rec.PartialResult(), 0.0))

    def start():
        if state["running"]:
            stop()
            return
        model = load_model()
        state["commands"] = parse_commands(cmd_txt.get("1.0", "end"))
        fillers = fill_txt.get("1.0", "end").split() if fillers_var.get() else []
        phrases = grammar_for(variant_var.get(), state["commands"], fillers, distinct_var.get())
        rec = make_recognizer(model, phrases)
        state["tracker"] = ComboTracker(float(window_var.get()))
        new_target()
        device = inputs[mic.current()][0] if inputs else None
        state["audio"] = queue.Queue()
        state["running"] = True
        state["stream"] = sd.RawInputStream(samplerate=RATE, blocksize=BLOCK, device=device,
                                            dtype="int16", channels=1, callback=audio_cb)
        state["stream"].start()
        threading.Thread(target=worker, args=(rec,), daemon=True).start()
        start_btn.config(text="■ Стоп")
        n = len(phrases) if phrases else 0
        status.config(text=f"Слушаю: {mic.get()} · вариант: {VARIANTS[variant_var.get()]} · "
                           f"окно {float(window_var.get()):.1f} с · фраз в грамматике: {n or 'нет (свободно)'}")

    def stop():
        state["running"] = False
        if state["stream"] is not None:
            state["stream"].stop()
            state["stream"].close()
            state["stream"] = None
        start_btn.config(text="▶ Слушать")
        status.config(text="Остановлено")

    def restart():
        if state["running"]:
            stop()
            start()
        else:
            state["commands"] = parse_commands(cmd_txt.get("1.0", "end"))
            new_target()
            status.config(text=f"Применено: команд {len(state['commands'])}")

    def poll():
        level.delete("all")
        lv = min(1.0, state["level"])
        level.create_rectangle(0, 0, int(level.winfo_width() * lv), 14, fill="#3c3" if lv < 0.8 else "#e33", width=0)
        state["level"] *= 0.9
        if state["flash_until"] and time.time() > state["flash_until"]:
            result_lbl.config(text="", fg="#888")
            state["flash_until"] = 0.0
        while not state["ui"].empty():
            kind, payload, dt = state["ui"].get()
            if kind == "partial":
                partial.config(text=json.loads(payload).get("partial", "") or "…")
                continue
            text, accepted, rejected, conf = split_result(payload, conf_var.get())
            if not text or text == "[unk]":
                continue
            seq = extract_sequence(accepted, state["commands"])
            stamp = time.strftime("%H:%M:%S")
            log.insert("end", f"{stamp}  [{conf:.2f}]  «{' '.join(w['word'] for w in accepted) or '—'}»")
            if seq:
                log.insert("end", "  → " + " ".join(n for n, _ in seq), "hit")
            if rejected:
                log.insert("end", "   отброшено: " + " ".join(rejected), "dim")
            log.insert("end", f"   ({dt * 1000:.0f} мс)\n", "dim")
            for combo in state["tracker"].feed(seq):
                on_combo(combo)
            draw_progress()
            log.see("end")
            partial.config(text="…")
        root.after(50, poll)

    start_btn.config(command=start)
    root.protocol("WM_DELETE_WINDOW", lambda: (stop(), root.destroy()))
    state["commands"] = parse_commands(PRESETS[DEFAULT_LANG]["commands"])
    new_target()
    draw_stats()
    poll()
    if selftest:
        load_model()
        print("selftest: окно построено, модель загружена, микрофонов:", len(inputs))
        root.after(1500, root.destroy)
    root.mainloop()


if __name__ == "__main__":
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--wav", help="распознать WAV 16 кГц моно вместо микрофона")
    ap.add_argument("--lang", choices=list(PRESETS), default=DEFAULT_LANG, help="язык: модель и синонимы")
    ap.add_argument("--variant", choices=list(VARIANTS), default="words")
    ap.add_argument("--window", type=float, default=4.0, help="окно сбора тройки, с")
    ap.add_argument("--distinct", action="store_true", help="только тройки без повторов (для phrase)")
    ap.add_argument("--repeats", action="store_true", help="(по умолчанию) повторы разрешены")
    ap.add_argument("--words", help="файл с командами (формат как в окне)")
    ap.add_argument("--no-fillers", action="store_true")
    ap.add_argument("--min-conf", type=float, default=0.5)
    ap.add_argument("--selftest", action="store_true", help="построить окно и закрыться через 1.5 с")
    args = ap.parse_args()
    if args.wav:
        sys.stdout.reconfigure(encoding="utf-8")
        preset = PRESETS[args.lang]
        commands = parse_commands(open(args.words, encoding="utf-8").read() if args.words else preset["commands"])
        fillers = [] if args.no_fillers else preset["fillers"].split()
        combos = recognize_file(args.wav, args.variant, commands, fillers, args.distinct, args.window, args.min_conf,
                                model_dir(args.lang))
        print("ТРОЙКИ:", " | ".join(" · ".join(c) for c in combos) or "нет")
    else:
        run_gui(selftest=args.selftest)
