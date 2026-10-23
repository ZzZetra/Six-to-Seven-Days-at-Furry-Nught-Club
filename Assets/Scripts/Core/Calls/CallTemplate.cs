using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

// Типы звонков: о чём спрашивает клиент и как по расписанию найти верный ответ (CallBuilder)
public enum CallType
{
    WhatAtTime,   // «Что будет в {time}?» — дело и кто его ведёт
    WhenFurry,    // «Во сколько у {furry_gen} {activity}?» — время
    FurryAtTime,  // «Что будет делать {furry} в {time}?» — дело или «свободен»
    WhoFree,      // «Кто свободен в {time}?» — свободный фурри
    OrderFurry,   // «Можно {furry_acc} на {time}?» — да, если свободен
    VipFree,      // «{room} свободна в {time}?» — да, если никто не занял
}

// Один диалог звонка: тексты с переменными. Файлы — Assets/StreamingAssets/Calls/Dialogues/*.json,
// формат — docs/CALLS.md. Переменная — {имя}, например {time}; род — {свободен|свободна} (по фурри).
// Клиент не знает правильного ответа (иначе бы не звонил), поэтому реагирует на то, что ему ответили:
// за верный ответ — деньги, за неверный — та же реакция, только без денег.
[Serializable]
public class CallTemplate
{
    public string id = "";
    public string type = "";
    public string caller = "Клиент";
    public string client = "";   // реплика клиента
    public string answer = "";   // вариант ответа — для WhatAtTime, WhenFurry, FurryAtTime, WhoFree
    public string yes = "";      // ответы «да» и «нет» — для OrderFurry и VipFree
    public string no = "";
    public string thanks = "";   // реакция клиента на ответ; для OrderFurry и VipFree — на «да»: «Отлично, приду»
    public string thanksNo = ""; // только для OrderFurry и VipFree — реакция на «нет»: «Жаль, тогда в другой раз»
    public int reward;           // $ за верный ответ; 0 — случайно от Balance.CallRewardMin до CallRewardMax

    public bool TryGetType(out CallType result) => CallTypes.TryParse(type, out result);

    // Текст ошибки или null, если всё хорошо
    public string Validate()
    {
        if (string.IsNullOrWhiteSpace(id)) return "пустой «id»";
        if (!TryGetType(out var t))
            return $"неизвестный «type»: «{type}». Можно: {string.Join(", ", Enum.GetNames(typeof(CallType)))}";
        if (string.IsNullOrWhiteSpace(client)) return "пустая реплика клиента «client»";
        if (string.IsNullOrWhiteSpace(thanks)) return "пустой «thanks»";
        if (reward < 0) return "«reward» не может быть меньше 0";

        var fields = new List<(string name, string text)> { ("client", client), ("thanks", thanks) };
        if (CallTypes.IsYesNo(t))
        {
            if (string.IsNullOrWhiteSpace(yes) || string.IsNullOrWhiteSpace(no)) return "для этого типа нужны «yes» и «no»";
            if (string.IsNullOrWhiteSpace(thanksNo)) return "для этого типа нужен «thanksNo» — реакция клиента на «нет»";
            fields.Add(("yes", yes));
            fields.Add(("no", no));
            fields.Add(("thanksNo", thanksNo));
        }
        else
        {
            if (string.IsNullOrWhiteSpace(answer)) return "для этого типа нужен «answer»";
            var key = CallTypes.AnswerVariables(t);
            if (!CallText.Variables(answer).Any(v => key.Contains(v)))
                return $"в «answer» нужна переменная с ответом: {string.Join(" или ", key.Select(v => "{" + v + "}"))}";
            fields.Add(("answer", answer));
        }
        foreach (var (name, text) in fields)
        {
            var error = CallText.Check(text, CallTypes.Variables(t), CallTypes.HasFurry(t));
            if (error != null) return $"«{name}»: {error}";
        }
        return null;
    }
}

public static class CallTypes
{
    public static readonly string[] FurryForms = { "furry", "furry_gen", "furry_dat", "furry_acc", "furry_ins" };

    public static bool TryParse(string text, out CallType result)
    {
        result = default;
        if (string.IsNullOrWhiteSpace(text) || char.IsDigit(text.Trim()[0])) return false;
        return Enum.TryParse(text.Trim(), true, out result) && Enum.IsDefined(typeof(CallType), result);
    }

    public static bool IsYesNo(CallType t) => t == CallType.OrderFurry || t == CallType.VipFree;
    public static bool HasFurry(CallType t) => t != CallType.VipFree;

    // Какие переменные можно писать в текстах звонка этого типа
    public static string[] Variables(CallType t)
    {
        switch (t)
        {
            case CallType.WhatAtTime:
            case CallType.WhenFurry:
            case CallType.FurryAtTime:
                return FurryForms.Concat(new[] { "time", "activity" }).ToArray();
            case CallType.WhoFree:
            case CallType.OrderFurry:
                return FurryForms.Concat(new[] { "time" }).ToArray();
            default:
                return new[] { "room", "time" };
        }
    }

    // В варианте ответа должна быть хотя бы одна из них — иначе все варианты выйдут одинаковыми
    public static string[] AnswerVariables(CallType t)
    {
        switch (t)
        {
            case CallType.WhatAtTime:
            case CallType.FurryAtTime: return new[] { "activity" };
            case CallType.WhenFurry: return new[] { "time" };
            case CallType.WhoFree: return FurryForms;
            default: return new string[0];
        }
    }

    public static string Describe(CallType t)
    {
        switch (t)
        {
            case CallType.WhatAtTime: return "Что будет в такое-то время → дело и кто его ведёт";
            case CallType.WhenFurry: return "Во сколько у фурри такое-то дело → время";
            case CallType.FurryAtTime: return "Что будет делать фурри в такое-то время → дело или «свободен»";
            case CallType.WhoFree: return "Кто свободен в такое-то время → свободный фурри";
            case CallType.OrderFurry: return "Заказать фурри на время → да, если свободен";
            default: return "Свободна ли VIP-комната в такое-то время → да, если никто не занял";
        }
    }
}

// Подстановка переменных в текст звонка
public static class CallText
{
    static readonly Regex Token = new Regex(@"\{([^{}]*)\}");

    // {time} → значение из vars; {свободен|свободна} → первая форма для него, вторая для неё
    public static string Fill(string text, IReadOnlyDictionary<string, string> vars, bool female)
    {
        return Token.Replace(text ?? "", m =>
        {
            string inner = m.Groups[1].Value;
            int bar = inner.IndexOf('|');
            if (bar >= 0) return female ? inner.Substring(bar + 1) : inner.Substring(0, bar);
            return vars.TryGetValue(inner.Trim(), out var value) ? value : m.Value;
        });
    }

    // Имена переменных в тексте (без форм рода)
    public static IEnumerable<string> Variables(string text) =>
        Token.Matches(text ?? "").Cast<Match>().Select(m => m.Groups[1].Value.Trim()).Where(v => !v.Contains('|'));

    // Ошибка в тексте или null: неизвестная переменная, род там, где нет фурри, непарные скобки
    public static string Check(string text, ICollection<string> allowed, bool hasFurry)
    {
        text = text ?? "";
        if (text.Count(c => c == '{') != text.Count(c => c == '}')) return "непарные скобки { }";
        foreach (Match m in Token.Matches(text))
        {
            string inner = m.Groups[1].Value.Trim();
            if (inner.Contains('|'))
            {
                if (!hasFurry) return $"«{{{inner}}}»: род не по кому выбрать — в этом звонке нет фурри";
                if (inner.Count(c => c == '|') != 1) return $"«{{{inner}}}»: нужно ровно две формы через |";
            }
            else if (!allowed.Contains(inner))
                return $"неизвестная переменная {{{inner}}}. Можно: {string.Join(", ", allowed.Select(v => "{" + v + "}"))}";
        }
        return null;
    }
}
