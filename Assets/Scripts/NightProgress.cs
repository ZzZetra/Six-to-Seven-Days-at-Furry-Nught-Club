using UnityEngine;

// Прохождение ночей 1–7 (Nights.cs): какая ночь сейчас идёт и сколько пройдено. Пройденное хранится между
// запусками (PlayerPrefs, ключ SSDFNC.NightsPassed). Ночь открыта, когда пройдена предыдущая; 7-я — ещё и
// если на счету не меньше Balance.Night7Money (деньги не тратятся).
// Current = 0 — «Своя ночь» из вкладки меню: уровни из NightSettings, на прохождение не влияет.
public static class NightProgress
{
    const string Key = "SSDFNC.NightsPassed";

    // Какая ночь идёт или выбрана в меню. Play сразу в офисе (без меню) — 1-я
    public static int Current { get; private set; } = 1;

    public static int Passed => Mathf.Clamp(PlayerPrefs.GetInt(Key, 0), 0, Nights.Count);
    public static bool IsCustom => Current == 0;
    public static NightConfig Config => IsCustom ? null : Nights.Get(Current);

    // Уровни фурри на эту ночь: Дир, Стрей, Нова, Дали, Доли
    public static int[] Levels => IsCustom ? NightSettings.Levels : (int[])Config.Levels.Clone();
    public static float MonitorRebootChancePerMinute =>
        IsCustom ? Balance.MonitorRebootChancePerMinute : Config.MonitorRebootChancePerMinute;
    public static int CallsMin => IsCustom ? Balance.CallsPerNightMin : Config.CallsMin;
    public static int CallsMax => IsCustom ? Balance.CallsPerNightMax : Config.CallsMax;

    public static string Name => IsCustom ? "Своя ночь" : $"Ночь {Current}";

    // Почему ночь закрыта, или null, если открыта
    public static string LockReason(int night)
    {
        if (night < 1 || night > Nights.Count) return "нет такой ночи";
        if (Passed < night - 1) return $"пройди ночь {night - 1}";
        if (night == Nights.Count && Wallet.Money < Balance.Night7Money) return $"нужно {Balance.Night7Money} $";
        return null;
    }

    public static bool IsUnlocked(int night) => LockReason(night) == null;

    // Самая дальняя открытая ночь — с неё «Продолжить»
    public static int LastUnlocked
    {
        get
        {
            int n = 1;
            while (n < Nights.Count && IsUnlocked(n + 1)) n++;
            return n;
        }
    }

    public static void Select(int night) => Current = Mathf.Clamp(night, 0, Nights.Count);

    // Смена дошла до конца: ночь пройдена (своя ночь не считается)
    public static void MarkPassed()
    {
        if (IsCustom || Current <= Passed) return;
        PlayerPrefs.SetInt(Key, Current);
        PlayerPrefs.Save();
    }

    // Следующая ночь после текущей, если она есть и открыта; иначе 0
    public static int Next => !IsCustom && Current < Nights.Count && IsUnlocked(Current + 1) ? Current + 1 : 0;
}
