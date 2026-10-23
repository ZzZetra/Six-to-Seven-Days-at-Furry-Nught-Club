using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

// Данные звонков из Assets/StreamingAssets/Calls (формат — docs/CALLS.md):
//   schedule.json — из чего каждую ночь собирается расписание;
//   Dialogues/*.json — диалоги, по одному в файле.
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
