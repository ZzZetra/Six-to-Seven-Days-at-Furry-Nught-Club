using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

// Данные звонков из Assets/StreamingAssets/Calls (формат — docs/CALLS.md):
//   schedule.json — из чего каждую ночь собирается расписание;
//   Dialogues/*.json — диалоги, по одному в файле;
//   Boss/night_N.json — звонки начальства на ночь N (туториал);
//   Triggered/<условие>.json — звонок по условию (CallDirector.Request("<условие>")).
// Файл с ошибкой пропускается, причина пишется в Console.
public static class CallData
{
    public static string Folder => Path.Combine(Application.streamingAssetsPath, "Calls");

    public static ScheduleData LoadSchedule()
    {
        string path = Path.Combine(Folder, "schedule.json");
        try
        {
            var data = JsonUtility.FromJson<ScheduleData>(File.ReadAllText(path));
            string error = data == null ? "пустой файл" : data.Validate();
            if (error == null) return data;
            Debug.LogWarning($"[SSDFNC] schedule.json: {error}. Беру расписание по умолчанию");
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[SSDFNC] schedule.json не читается: {e.Message}. Беру расписание по умолчанию");
        }
        return ScheduleData.Default();
    }

    // Звонки начальства на ночь (своя ночь — 0: их нет). Нет файла — нет звонков
    public static PhoneTalk[] LoadBossCalls(int night) =>
        night > 0 ? LoadTalks(Path.Combine(Folder, "Boss", $"night_{night}.json")) : new PhoneTalk[0];

    // Звонок по условию. Нет файла — заглушка, чтобы условие можно было проверить до того, как написан текст
    public static PhoneTalk LoadTriggered(string trigger)
    {
        var talks = LoadTalks(Path.Combine(Folder, "Triggered", trigger + ".json"));
        if (talks.Length > 0) return talks[0];
        return new PhoneTalk { caller = "Звонок", lines = new[] { $"(звонок по условию «{trigger}» — текста пока нет)" } };
    }

    static PhoneTalk[] LoadTalks(string path)
    {
        if (!File.Exists(path)) return new PhoneTalk[0];
        string file = Path.GetFileName(path);
        try
        {
            var data = JsonUtility.FromJson<PhoneTalkFile>(File.ReadAllText(path));
            var list = new List<PhoneTalk>();
            foreach (var talk in data?.calls ?? new PhoneTalk[0])
            {
                string error = talk.Validate();
                if (error != null) Debug.LogWarning($"[SSDFNC] Звонок в {file} пропущен: {error}");
                else list.Add(talk);
            }
            return list.ToArray();
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[SSDFNC] {file} не читается: {e.Message}");
            return new PhoneTalk[0];
        }
    }

    public static List<CallTemplate> LoadDialogues()
    {
        var list = new List<CallTemplate>();
        string dir = Path.Combine(Folder, "Dialogues");
        if (!Directory.Exists(dir)) return list;
        foreach (var path in Directory.GetFiles(dir, "*.json"))
        {
            string file = Path.GetFileName(path);
            try
            {
                var template = JsonUtility.FromJson<CallTemplate>(File.ReadAllText(path));
                string error = template == null ? "пустой файл" : template.Validate();
                if (error != null) Debug.LogWarning($"[SSDFNC] Диалог {file} пропущен: {error}");
                else list.Add(template);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SSDFNC] Диалог {file} не читается: {e.Message}");
            }
        }
        return list;
    }
}
