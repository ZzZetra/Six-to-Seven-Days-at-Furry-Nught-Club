using System;

// Звонок-монолог: начальство (туториал, своё на каждую ночь) или звонок по условию (CallDirector.Request).
// Отвечать не надо: ЛКМ — следующая реплика, после последней — гудки. Не взял трубку — ничего не будет.
// Файлы — Assets/StreamingAssets/Calls/Boss/night_N.json и Calls/Triggered/<условие>.json, формат — docs/CALLS.md.
[Serializable]
public class PhoneTalk
{
    public string caller = "Начальство";
    public string[] lines;
    public string when = ""; // "start" — в начале ночи, "middle" — в середине; пусто — первый в начале, остальные в середине

    public bool AtStart(int index) => when == "start" || (when != "middle" && index == 0);

    // Текст ошибки или null, если всё хорошо
    public string Validate()
    {
        if (lines == null || lines.Length == 0) return "нет реплик (lines)";
        if (when != "" && when != "start" && when != "middle") return $"«when»: «{when}» — бывает start или middle";
        return null;
    }
}

[Serializable]
public class PhoneTalkFile
{
    public PhoneTalk[] calls;
}

// Когда звонит начальство: «в начале ночи» — в случайный момент от BossCallStartFrom до BossCallStartTo секунд,
// «в середине» — в случайный момент от BossCallMiddleFrom до BossCallMiddleTo доли смены
public static class BossCallPlan
{
    public static float[] Times(PhoneTalk[] talks, float duration, Random rng)
    {
        var times = new float[talks.Length];
        for (int i = 0; i < talks.Length; i++)
        {
            float k = (float)rng.NextDouble();
            times[i] = talks[i].AtStart(i)
                ? Balance.BossCallStartFrom + k * (Balance.BossCallStartTo - Balance.BossCallStartFrom)
                : duration * (Balance.BossCallMiddleFrom + k * (Balance.BossCallMiddleTo - Balance.BossCallMiddleFrom));
        }
        return times;
    }
}
