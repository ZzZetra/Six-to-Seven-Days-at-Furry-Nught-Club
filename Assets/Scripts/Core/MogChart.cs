using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

public enum MogCheckType { Circle, Button, Arrow, Bar }
public enum MogDir { Right, Up, Left, Down }

// Смена скорости полоски: с секунды at метка едет со скоростью speed
[Serializable]
public class MogSpeedKey
{
    public float at;     // с музыки
    public float speed;  // полосок в секунду; минус — назад
}

// Один скилл-чек мог-батла. Время — секунды от начала музыки раскладки. Формат — docs/MOG_CHARTS.md,
// файлы — Assets/StreamingAssets/MogCharts/*.json. Какие поля нужны, зависит от вида:
//   кружок  (circle) — hit, pos, zone, speed: стрелка стартует сверху и идёт по часовой, в hit она в центре зоны;
//   кнопка  (button) — hit, x, y, approach: кольцо сжимается approach секунд и в hit смыкается с кнопкой;
//   стрелка (arrow)  — hit, x, y, approach, dir: появляется за approach секунд до hit и ждёт взмаха в сторону dir,
//                      кольцо показывает, сколько осталось; в hit оно смыкается — не успел, промах;
//   полоска (bar)    — start, end, pos, zone, speed, speedKeys: метка едет, у краёв отскакивает назад.
// Старые раскладки (start + hit без speed, тип sides) читаются как раньше.
[Serializable]
public class MogCheck
{
    public string type = "bar";     // circle | button | arrow | bar (старое sides — это arrow)
    public float hit;               // с: бит — когда надо попасть
    public float zone = 0.22f;      // ширина зоны: доля полоски, у кружка — доля оборота
    public float pos = 0.6f;        // центр зоны: у полоски 0..1 слева направо, у кружка — доля оборота от верха
    public float speed;             // кружок — оборотов в секунду, полоска — полосок в секунду; 0 — посчитать из start и hit
    public float start;             // полоска: когда появляется (метка у левого края)
    public float end;               // полоска: когда пропадает; 0 — когда метка первый раз дойдёт до края
    public MogSpeedKey[] speedKeys = new MogSpeedKey[0]; // полоска: смены скорости по ходу
    public float approach = 1f;     // кнопка: секунд, за которые кольцо сжимается до кнопки; стрелка: время на взмах
    public float x = 0.5f;          // кнопка и стрелка: место в окне скилл-чека, 0 — левый край, 1 — правый
    public float y = 0.5f;          //                                            0 — низ, 1 — верх
    public string dir = "";         // стрелка: right | up | left | down ("" у старых sides — по pos)

    public MogCheckType Type
    {
        get
        {
            switch ((type ?? "").Trim().ToLowerInvariant())
            {
                case "circle": return MogCheckType.Circle;
                case "button": return MogCheckType.Button;
                case "arrow":
                case "sides": return MogCheckType.Arrow;
                default: return MogCheckType.Bar;
            }
        }
    }

    public MogDir Direction
    {
        get
        {
            switch ((dir ?? "").Trim().ToLowerInvariant())
            {
                case "right": return MogDir.Right;
                case "up": return MogDir.Up;
                case "left": return MogDir.Left;
                case "down": return MogDir.Down;
                default: return pos < 0.5f ? MogDir.Left : MogDir.Right; // старое «в стороны»
            }
        }
    }

    // Скорость: у кружка — оборотов, у полоски — полосок в секунду (в начале). Старый формат — из start и hit
    public float Speed => speed != 0f ? speed : hit > start ? pos / (hit - start) : 0f;

    // Когда чек появляется
    public float StartTime
    {
        get
        {
            switch (Type)
            {
                case MogCheckType.Circle: return Speed > 0f ? hit - pos / Speed : hit;
                case MogCheckType.Bar: return start;
                default: return hit - approach;
            }
        }
    }

    // С какого момента в чек уже можно попасть: стрелка кружка дошла до зоны, открылось окно у кнопки
    // и стрелки, полоска появилась. Тогда предыдущий нерешённый чек — промах
    public float ReadyTime(float hitWindow)
    {
        switch (Type)
        {
            case MogCheckType.Circle: return Speed > 0f ? StartTime + Math.Max(0f, pos - zone / 2f) / Speed : hit;
            case MogCheckType.Bar: return start;
            default: return hit - hitWindow;
        }
    }

    // Когда полоска пропадает: end или первый приход метки к краю
    public float BarEnd => end > start ? end : Speed != 0f ? start + 1f / Math.Abs(Speed) : start;

    // Когда чек точно закончился (не нажал — промах)
    public float EndTime(float hitWindow)
    {
        switch (Type)
        {
            case MogCheckType.Circle: return Speed > 0f ? StartTime + (pos + zone / 2f) / Speed : hit;
            case MogCheckType.Bar: return BarEnd;
            default: return hit + hitWindow;
        }
    }

    // Полоска: где метка в момент t, 0..1. Скорость меняется по speedKeys, у краёв метка отскакивает
    public float BarPos(float t)
    {
        if (t <= start) return 0f;
        float s = 0f, v = Speed, from = start;
        foreach (var k in (speedKeys ?? new MogSpeedKey[0]).OrderBy(k => k.at))
        {
            if (k.at >= t) break;
            if (k.at > from)
            {
                s += v * (k.at - from);
                from = k.at;
            }
            v = k.speed;
        }
        s += v * (t - from);
        s = Math.Abs(s) % 2f; // путь «туда-обратно» сворачивается в отскоки от краёв
        return s <= 1f ? s : 2f - s;
    }
}

// Раскладка мог-батла под один трек: музыка и скилл-чеки по времени.
// Фазы идут из раскладки: Появление (до первого чека) → группы чеков подряд одного вида → Уход или Смерть.
[Serializable]
public class MogChart
{
    public string name = "";
    public string music = "";    // файл музыки рядом с json (.ogg, .wav, .mp3); "" — без музыки
    public float offset;         // с: если музыка опаздывает относительно чеков — положительное число
    public MogCheck[] checks = new MogCheck[0];

    // Проверить и упорядочить. Вернёт текст ошибки или null, если всё хорошо
    public string Validate()
    {
        if (checks == null || checks.Length == 0) return "нет ни одного скилл-чека";
        foreach (var c in checks)
        {
            string at = $"чек «{c.type}» на {c.hit:0.00} с";
            if (c.zone <= 0f || c.zone > 1f) return $"{at}: «zone» должна быть от 0 до 1";
            switch (c.Type)
            {
                case MogCheckType.Circle:
                    if (c.pos <= 0f || c.pos > 1f) return $"{at}: «pos» должна быть от 0 до 1";
                    if (c.Speed <= 0f) return $"{at}: нужна «speed» больше 0 (оборотов в секунду)";
                    break;
                case MogCheckType.Bar:
                    if (c.pos <= 0f || c.pos > 1f) return $"{at}: «pos» должна быть от 0 до 1";
                    if (c.Speed == 0f) return $"{at}: нужна «speed» (полосок в секунду) или «hit» позже «start»";
                    if (c.BarEnd <= c.start + 0.05f) return $"{at}: «end» должен быть позже «start»";
                    break;
                default:
                    if (c.approach < 0.2f) return $"{at}: «approach» должен быть не меньше 0.2 с";
                    if (c.x < 0f || c.x > 1f || c.y < 0f || c.y > 1f) return $"{at}: «x» и «y» должны быть от 0 до 1";
                    string d = (c.dir ?? "").Trim().ToLowerInvariant();
                    if (c.Type == MogCheckType.Arrow && d != "" && d != "right" && d != "up" && d != "left" && d != "down")
                        return $"{at}: «dir» — right, up, left или down";
                    break;
            }
        }
        checks = checks.OrderBy(c => c.StartTime).ToArray();
        return null;
    }

    public float FirstCheckStart => checks.Length == 0 ? 0f : checks.Min(c => c.StartTime);

    // Когда закончится последний чек
    public float LastCheckEnd => checks.Length == 0 ? 0f : checks.Max(c => c.EndTime(Balance.MogHitWindow));

    // В json для файла (редактор раскладок): у чека только поля его вида, по чеку на строке.
    // Числа всегда с точкой — иначе на русской Windows вышла бы запятая и json сломался
    public string ToJson()
    {
        var sb = new StringBuilder();
        sb.Append("{\n");
        sb.Append($"    \"name\": {Str(name)},\n");
        sb.Append($"    \"music\": {Str(music)},\n");
        sb.Append($"    \"offset\": {Num(offset)},\n");
        sb.Append("    \"checks\": [\n");
        for (int i = 0; i < checks.Length; i++)
            sb.Append("        { ").Append(CheckJson(checks[i])).Append(i < checks.Length - 1 ? " },\n" : " }\n");
        sb.Append("    ]\n}\n");
        return sb.ToString();
    }

    static string CheckJson(MogCheck c)
    {
        var p = new List<string>();
        void Add(string key, string value) => p.Add($"\"{key}\": {value}");
        switch (c.Type)
        {
            case MogCheckType.Circle:
                Add("type", Str("circle"));
                Add("hit", Num(c.hit)); Add("pos", Num(c.pos)); Add("zone", Num(c.zone)); Add("speed", Num(c.Speed));
                break;
            case MogCheckType.Button:
                Add("type", Str("button"));
                Add("hit", Num(c.hit)); Add("x", Num(c.x)); Add("y", Num(c.y)); Add("approach", Num(c.approach));
                break;
            case MogCheckType.Arrow:
                Add("type", Str("arrow"));
                Add("hit", Num(c.hit)); Add("dir", Str(c.Direction.ToString().ToLowerInvariant()));
                Add("x", Num(c.x)); Add("y", Num(c.y)); Add("approach", Num(c.approach));
                break;
            default:
                Add("type", Str("bar"));
                Add("start", Num(c.start)); Add("end", Num(c.BarEnd)); Add("hit", Num(c.hit));
                Add("pos", Num(c.pos)); Add("zone", Num(c.zone)); Add("speed", Num(c.Speed));
                if (c.speedKeys != null && c.speedKeys.Length > 0)
                    Add("speedKeys", "[" + string.Join(", ", c.speedKeys.OrderBy(k => k.at)
                        .Select(k => $"{{ \"at\": {Num(k.at)}, \"speed\": {Num(k.speed)} }}")) + "]");
                break;
        }
        return string.Join(", ", p);
    }

    static string Num(float v) => Math.Round(v, 3).ToString(CultureInfo.InvariantCulture);
    static string Str(string s) => "\"" + (s ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";

    // Раскладка по умолчанию, без музыки: по 5 кружков, полосок и стрелок, бит каждые 2 с,
    // между группами пауза 1 с. Такая же лежит в StreamingAssets/MogCharts/default.json
    public static MogChart Default()
    {
        float[] positions = { 0.55f, 0.7f, 0.4f, 0.62f, 0.48f };
        string[] dirs = { "right", "up", "left", "down", "right" };
        var list = new List<MogCheck>();
        float t = 3.5f; // первый бит
        for (int i = 0; i < 5; i++, t += 2f)
            list.Add(new MogCheck { type = "circle", hit = t, pos = positions[i], zone = 0.22f, speed = 0.6f });
        t += 1f;
        for (int i = 0; i < 5; i++, t += 2f)
            list.Add(new MogCheck { type = "bar", start = t - 1f, hit = t, end = t + 0.9f, pos = positions[i], zone = 0.22f, speed = positions[i] });
        t += 1f;
        for (int i = 0; i < 5; i++, t += 2f)
            list.Add(new MogCheck { type = "arrow", hit = t, dir = dirs[i], approach = 1f });
        return new MogChart { name = "По умолчанию (без музыки)", checks = list.ToArray() };
    }
}
