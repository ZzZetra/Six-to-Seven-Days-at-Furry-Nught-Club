using System;
using UnityEngine;

// Настройка ночи из главного меню (вкладка «Настройка ночи»): уровни ИИ фурри 0–20.
// Запоминается между запусками (PlayerPrefs). Пока не настраивали — берётся Difficulty.Preset из кода.
// NightDirector начинает ночь с этими уровнями.
public static class NightSettings
{
    const string Key = "SSDFNC.NightLevels";
    static int[] levels;

    // Копия: порядок Дир, Стрей, Нова, Дали, Доли (как в FurryId)
    public static int[] Levels
    {
        get
        {
            Load();
            return (int[])levels.Clone();
        }
    }

    public static void Set(FurryId id, int level)
    {
        Load();
        levels[(int)id] = Mathf.Clamp(level, 0, 20);
        Save();
    }

    public static void SetAll(int[] newLevels)
    {
        Load();
        for (int i = 0; i < levels.Length && i < newLevels.Length; i++) levels[i] = Mathf.Clamp(newLevels[i], 0, 20);
        Save();
    }

    public static void ResetToCode() => SetAll(Difficulty.Levels(Difficulty.Preset));

    // «Normal (6/5/1/6/4)» или «Своя (…)», если не совпадает ни с одним пресетом
    public static string Describe()
    {
        Load();
        string name = "Своя";
        foreach (DifficultyPreset p in new[] { DifficultyPreset.Easy, DifficultyPreset.Normal, DifficultyPreset.Hard })
            if (Same(Difficulty.Levels(p), levels)) name = p.ToString();
        return $"{name} ({string.Join("/", levels)})";
    }

    static bool Same(int[] a, int[] b)
    {
        for (int i = 0; i < a.Length; i++)
            if (a[i] != b[i]) return false;
        return true;
    }

    static void Load()
    {
        if (levels != null) return;
        levels = Difficulty.Levels(Difficulty.Preset);
        var saved = PlayerPrefs.GetString(Key, "").Split(',');
        if (saved.Length != levels.Length) return;
        var parsed = new int[levels.Length];
        for (int i = 0; i < saved.Length; i++)
            if (!int.TryParse(saved[i], out parsed[i])) return;
        Array.Copy(parsed, levels, levels.Length);
    }

    static void Save()
    {
        PlayerPrefs.SetString(Key, string.Join(",", levels));
        PlayerPrefs.Save();
    }
}
