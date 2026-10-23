using System;

// Когда за смену звонят клиенты: от Balance.CallsPerNightMin до CallsPerNightMax звонков, примерно
// поровну по смене. Смена делится на равные отрезки, в каждом — один звонок в случайный момент первых 60%
// отрезка, поэтому подряд звонки не идут. Первый — не раньше CallFirstDelay, последний — не позже чем
// за CallLastMargin до конца. Время — секунды смены без пауз на мог-батл.
public static class CallPlan
{
    public static float[] Times(float duration, Random rng)
    {
        float start = Balance.CallFirstDelay, end = duration - Balance.CallLastMargin;
        if (end <= start) return new float[0];
        int count = rng.Next(Balance.CallsPerNightMin, Balance.CallsPerNightMax + 1);
        float slot = (end - start) / count;
        var times = new float[count];
        for (int i = 0; i < count; i++)
            times[i] = start + slot * i + (float)rng.NextDouble() * slot * 0.6f;
        return times;
    }
}
