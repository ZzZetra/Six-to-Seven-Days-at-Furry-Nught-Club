using System;
using System.Collections.Generic;
using System.Linq;

public enum MogCheckType { Circle, Bar, Sides }

// Один скилл-чек мог-батла. Время — в секундах от начала музыки раскладки.
// Формат описан в docs/MOG_CHARTS.md; файлы раскладок — Assets/StreamingAssets/MogCharts/*.json.
[Serializable]
public class MogCheck
{
    public string type = "bar";  // circle | bar | sides
    public float start;          // с: чек появляется, метка стартует от левого края
    public float hit;            // с: «бит» — метка ровно в центре зоны
    public float zone = 0.22f;   // ширина зоны, доля полоски (у кружка — доля оборота)
    public float pos = 0.6f;     // центр зоны 0..1 (у кружка — угол как доля оборота; у «в стороны»: < 0.5 — влево)

    public MogCheckType Type
    {
        get
        {
            switch ((type ?? "").Trim().ToLowerInvariant())
            {
                case "circle": return MogCheckType.Circle;
                case "sides": return MogCheckType.Sides;
                default: return MogCheckType.Bar;
            }
        }
    }
}

// Раскладка мог-батла под один трек: музыка и скилл-чеки по времени.
// Фазы идут из раскладки: Появление (до первого чека) → группы чеков → Уход или Смерть.
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
            if (c.hit <= c.start + 0.05f) return $"чек на {c.start:0.00} с: «hit» должен быть позже «start» хотя бы на 0.05 с";
            if (c.zone <= 0f || c.zone > 1f) return $"чек на {c.start:0.00} с: «zone» должна быть от 0 до 1";
            if (c.pos <= 0f || c.pos > 1f) return $"чек на {c.start:0.00} с: «pos» должна быть от 0 до 1";
        }
        checks = checks.OrderBy(c => c.start).ToArray();
        return null;
    }

    public float FirstCheckStart => checks.Length == 0 ? 0f : checks.Min(c => c.start);

    // Примерное время до конца последнего чека (метка доходит до края полоски)
    public float LastCheckEnd => checks.Length == 0 ? 0f : checks.Max(c => c.start + (c.hit - c.start) / c.pos);

    // Раскладка по умолчанию, без музыки: по 5 кружков, полосок и «в стороны», каждые 2 с,
    // между группами пауза 1 с. Такая же лежит в StreamingAssets/MogCharts/default.json
    public static MogChart Default()
    {
        float[] positions = { 0.55f, 0.7f, 0.4f, 0.62f, 0.48f };
        var list = new List<MogCheck>();
        string[] types = { "circle", "bar", "sides" };
        float t = 2.5f;
        foreach (var type in types)
        {
            for (int i = 0; i < 5; i++)
            {
                list.Add(new MogCheck { type = type, start = t, hit = t + 1f, zone = 0.22f, pos = positions[i] });
                t += 2f;
            }
            t += 1f;
        }
        return new MogChart { name = "По умолчанию (без музыки)", checks = list.ToArray() };
    }
}
