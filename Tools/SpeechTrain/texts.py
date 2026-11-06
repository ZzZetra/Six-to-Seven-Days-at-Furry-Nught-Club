"""Тексты для синтеза примеров.

TARGETS — как написать каждое слово, чтобы синтезатор сказал его по-разному (английские голоса читают
латиницу, русские — кириллицу, так получается русский акцент). TRAPS — похожие слова, которые
командой НЕ считаются. Фразы — обычная речь, на ней модель учится молчать.

Близнецы настоящего произношения («love a tea», «mog a tea», «верите», «ловите») в ловушки не берём:
так пользователь на самом деле произносит слова (см. журнал в Tools/SpeechTest/README.md).
"""
import random

WORDS = ["verity", "lovety", "moggaty"]  # классы 1, 2, 3; класс 0 — «не команда»

TARGETS = {
    "verity": {
        "en": ["verity", "Verity", "verity.", "verity!", "verrity", "vehrity", "verity?"],
        "ru": ["верити", "вэрити", "верити.", "вери-ти"],
    },
    "lovety": {
        "en": ["lovety", "Lovety", "lovety.", "luvvity", "lovity", "luvety", "lovetty", "love-ity", "lovety!"],
        "ru": ["лавити", "лавэти", "ловити", "лавити."],
    },
    "moggaty": {
        "en": ["moggaty", "Moggaty", "moggaty.", "moggity", "mogaty", "moggatty", "mogga-tee", "moggaty!"],
        "ru": ["моггати", "могати", "моггаты", "мог-гати"],
    },
}

TRAPS = {
    "en": ["verify", "variety", "very", "vary", "verily", "rarity", "charity", "clarity", "parity", "gravity",
           "cavity", "liberty", "poverty", "novelty", "heavy", "navy", "ferret", "Verona", "very good",
           "lovely", "love it", "love", "loved", "lover", "lobby", "lovingly", "glove", "above it",
           "Margaret", "maggot", "muggy", "foggy", "mogul", "moggy", "mug", "mighty", "naughty", "party",
           "city", "eighty", "pretty", "property", "buggy", "doggy", "Mogadishu", "mascot", "my God"],
    "ru": ["верить", "ветер", "вертеть", "верно", "вечер", "ловить", "лови", "ладно", "лапти", "любить",
           "магазин", "мага", "могу", "моги", "могила", "мокрый", "картина", "работа", "карета", "погода"],
}

_EN_SUBJ = ["I", "you", "we", "they", "the guard", "my friend", "the manager", "somebody", "the client", "Nova",
            "the old man", "this girl", "the night shift", "everyone", "nobody", "the DJ", "the cat", "he", "she"]
_EN_VERB = ["closed the door", "checked the cameras", "fixed the generator", "called the club", "heard a noise",
            "opened the window", "turned on the lights", "lost the keys", "found the map", "waited for an hour",
            "ordered a pizza", "fell asleep at the desk", "walked down the corridor", "pressed the red button",
            "looked at the monitor", "poured some fuel", "danced on the floor", "answered the phone",
            "forgot the password", "played the music", "cleaned the office", "watched the clock"]
_EN_TAIL = ["last night", "again", "five minutes ago", "before midnight", "in the dark", "right now", "today",
            "for no reason", "very quickly", "at three o'clock", "after the party", "without asking", ""]
_EN_FREE = ["Hello, is anybody there?", "What time is it now?", "Can you hear me?", "I think we need more fuel.",
            "Don't open that door.", "The camera on the left is broken.", "Okay, let's try again.",
            "This is the reception, how can I help you?", "Is the VIP room free tonight?",
            "I would love to come to the party.", "My mother called me yesterday.", "It's really cold in here.",
            "Turn it off, please.", "Wait, what was that sound?", "I have no idea what you mean.",
            "The generator is making a weird noise.", "Let me check the schedule.", "See you tomorrow.",
            "Thank you very much.", "No, no, no, stop it.", "One, two, three, four, five.", "Yes, of course.",
            "Where is the manager?", "Something is moving in the corridor.", "Please speak a bit louder."]

_RU_SUBJ = ["я", "ты", "мы", "они", "охранник", "мой друг", "директор", "кто-то", "клиент", "Нова", "старик",
            "эта девушка", "ночная смена", "все", "никто", "диджей", "кошка", "он", "она"]
_RU_VERB = ["закрыл дверь", "проверил камеры", "починил генератор", "позвонил в клуб", "услышал шум",
            "открыл окно", "включил свет", "потерял ключи", "нашёл карту", "ждал целый час", "заказал пиццу",
            "уснул за столом", "прошёл по коридору", "нажал красную кнопку", "смотрел в монитор",
            "залил бензин", "танцевал на полу", "ответил на звонок", "забыл пароль", "включил музыку",
            "убрал в офисе", "смотрел на часы"]
_RU_TAIL = ["вчера ночью", "опять", "пять минут назад", "до полуночи", "в темноте", "прямо сейчас", "сегодня",
            "просто так", "очень быстро", "в три часа", "после вечеринки", "без спроса", ""]
_RU_FREE = ["Алло, есть кто-нибудь?", "Который сейчас час?", "Ты меня слышишь?", "Кажется, нужно больше бензина.",
            "Не открывай эту дверь.", "Левая камера не работает.", "Ладно, давай ещё раз.",
            "Ресепшен, чем могу помочь?", "VIP-комната сегодня свободна?", "Я бы с радостью пришёл на вечеринку.",
            "Мама вчера звонила.", "Здесь очень холодно.", "Выключи, пожалуйста.", "Подожди, что это был за звук?",
            "Понятия не имею, о чём ты.", "Генератор как-то странно шумит.", "Сейчас посмотрю расписание.",
            "До завтра.", "Большое спасибо.", "Нет, нет, хватит.", "Раз, два, три, четыре, пять.",
            "Да, конечно.", "Где директор?", "В коридоре что-то двигается.", "Говори погромче, пожалуйста.",
            "Верите или нет, но это правда.", "Ловите момент, пока можно.", "Зайди в магазин после работы."]


def sentence(lang, rng):
    """Случайная обычная фраза: шаблон «кто — что сделал — когда» или готовая"""
    if rng.random() < 0.35:
        return rng.choice(_EN_FREE if lang == "en" else _RU_FREE)
    if lang == "en":
        s = f"{rng.choice(_EN_SUBJ)} {rng.choice(_EN_VERB)} {rng.choice(_EN_TAIL)}".strip()
    else:
        s = f"{rng.choice(_RU_SUBJ)} {rng.choice(_RU_VERB)} {rng.choice(_RU_TAIL)}".strip()
    return s[0].upper() + s[1:] + rng.choice([".", ".", "!", "?", ""])


if __name__ == "__main__":
    r = random.Random(1)
    for _ in range(5):
        print(sentence("en", r), "|", sentence("ru", r))
