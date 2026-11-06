"""Проверка на голосах, которых не было в обучении: Windows Irina (русский) и Zira (английский).

  .venv\\Scripts\\python evaluate.py [--threshold 0.8] [--remake]

Записи (data/test, делаются один раз): тройки с паузами, тройки на одном дыхании, отдельные слова, обычная речь
с похожими словами-ловушками. Для каждой — сколько троек собрала своя модель и сколько Vosk («По словам», английская
модель, как в Tools/SpeechTest), и сколько лишних слов/троек на обычной речи.
"""
import argparse
import os
import random
import subprocess
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
TEST = os.path.join(HERE, "data", "test")
sys.path.insert(0, os.path.join(HERE, "..", "SpeechTest"))

import detect  # noqa: E402

SPELL = {
    "en": {"VERITY": "verity", "LOVETY": "lovety", "MOGGATY": "moggaty"},
    "ru": {"VERITY": "верити", "LOVETY": "лавити", "MOGGATY": "моггати"},
}
VOICE = {"en": ("Microsoft Zira Desktop", "en-US"), "ru": ("Microsoft Irina Desktop", "ru-RU")}

NEG = {
    "en": ["Hello, this is the reception, how can I help you?", "I would love to come to the party tonight.",
           "Can you verify the variety of drinks we have?", "Margaret said the weather is muggy and foggy.",
           "That was a lovely evening, I love it.", "The gravity of the situation is very clear.",
           "We need more fuel for the generator, very soon.", "My mother called me yesterday about the lobby.",
           "Please check the cameras in the corridor.", "One, two, three, four, five, six, seven.",
           "The city is pretty at night, eighty floors of lights.", "Don't open the door, something is moving.",
           "Charity and clarity matter, said the mogul.", "Is the VIP room free at three o'clock?"],
    "ru": ["Алло, это ресепшен, чем могу помочь?", "Я бы с радостью пришёл на вечеринку сегодня.",
           "Верите или нет, но генератор опять сломался.", "Ловите момент, пока можно.",
           "Зайди в магазин после работы и купи хлеба.", "Мама вчера звонила, говорит, на улице ветер.",
           "Проверь камеры в коридоре, там что-то двигается.", "Раз, два, три, четыре, пять, шесть, семь.",
           "Не открывай эту дверь, пожалуйста.", "VIP-комната свободна в три часа?",
           "Могу ли я заказать столик на вечер?", "Верить никому нельзя, особенно ночью."],
}


def speak(lang, ssml_body, path):
    name, culture = VOICE[lang]
    ssml = (f'<speak version="1.0" xmlns="http://www.w3.org/2001/10/synthesis" xml:lang="{culture}">'
            f'{ssml_body}</speak>')
    ssml_path = path + ".ssml"
    open(ssml_path, "w", encoding="utf-8").write(ssml)
    ps = ("Add-Type -AssemblyName System.Speech; $s = New-Object System.Speech.Synthesis.SpeechSynthesizer; "
          f"$s.SelectVoice('{name}'); "
          "$f = New-Object System.Speech.AudioFormat.SpeechAudioFormatInfo(16000, "
          "[System.Speech.AudioFormat.AudioBitsPerSample]::Sixteen, [System.Speech.AudioFormat.AudioChannel]::Mono); "
          f"$s.SetOutputToWaveFile('{path}', $f); "
          f"$s.SpeakSsml([IO.File]::ReadAllText('{ssml_path}', [Text.Encoding]::UTF8)); $s.Dispose()")
    subprocess.run(["powershell", "-NoProfile", "-Command", ps], check=True)
    os.remove(ssml_path)


def make_tests():
    """[(файл, ожидаемые тройки или None для обычной речи)]"""
    os.makedirs(TEST, exist_ok=True)
    rng = random.Random(7)
    names = list(SPELL["en"])
    cases = []
    for lang in ("en", "ru"):
        for style, gap_word, gap_triple in (("pauses", 450, 2200), ("breath", 60, 2200)):
            triples = [tuple(rng.choice(names) for _ in range(3)) for _ in range(8)]
            body = "".join(
                "".join(f'{SPELL[lang][w]}<break time="{gap_word}ms"/>' for w in t) + f'<break time="{gap_triple}ms"/>'
                for t in triples)
            path = os.path.join(TEST, f"{lang}_{style}.wav")
            cases.append((path, triples, body, lang))
        words = [w for w in names for _ in range(4)]
        rng.shuffle(words)
        body = "".join(f'{SPELL[lang][w]}<break time="1500ms"/>' for w in words)
        cases.append((os.path.join(TEST, f"{lang}_single.wav"), [tuple(words[i:i + 3]) for i in range(0, 12, 3)], body, lang))
        body = "".join(f'{s}<break time="900ms"/>' for s in NEG[lang])
        cases.append((os.path.join(TEST, f"{lang}_speech.wav"), None, body, lang))
    return cases


def vosk_triples(path, window=4.0):
    import speech_test as st
    preset = st.PRESETS["en"]
    commands = st.parse_commands(preset["commands"])
    import io, contextlib
    with contextlib.redirect_stdout(io.StringIO()):
        combos = st.recognize_file(path, "words", commands, preset["fillers"].split(), False, window, 0.5, st.model_dir("en"))
    return combos


def score(expected, got):
    """сколько ожидаемых троек собрано по порядку (жадно)"""
    hit, j = 0, 0
    for t in got:
        if j < len(expected) and tuple(t) == tuple(expected[j]):
            hit += 1
            j += 1
        elif j < len(expected) and tuple(t) == tuple(expected[min(j + 1, len(expected) - 1)]):
            hit += 1
            j += 2
    return hit


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--threshold", type=float, default=0.8)
    ap.add_argument("--remake", action="store_true")
    ap.add_argument("--no-vosk", action="store_true")
    args = ap.parse_args()
    cases = make_tests()
    for path, _, body, lang in cases:
        if args.remake or not os.path.isfile(path):
            speak(lang, body, path)

    print(f"порог {args.threshold}")
    print("запись | ожидалось троек | своя модель: верно / лишних слов | Vosk: верно / всего троек")
    for path, expected, _, _ in cases:
        window = 10.0 if "single" in path else 4.0  # отдельные слова через 1.5 с: тройка не влезает в окно 4 с
        found = detect.detect_file(path, threshold=args.threshold)
        tracker = __import__("speech_test").ComboTracker(window)
        triples = tracker.feed([(w, t) for w, t, _ in found])
        vt = [] if args.no_vosk else vosk_triples(path, window)
        name = os.path.basename(path)
        if expected is None:
            print(f"{name:16s} | обычная речь | лишних слов {len(found)}, троек {len(triples)}"
                  f" ({' '.join(w for w, _, _ in found) or '—'}) | Vosk лишних троек {len(vt)}")
        else:
            words_expected = sum(len(t) for t in expected)
            print(f"{name:16s} | {len(expected)} | {score(expected, triples)} / слов {len(found)} из {words_expected}"
                  f" | {score(expected, vt)} / {len(vt)}")


if __name__ == "__main__":
    sys.exit(main())
