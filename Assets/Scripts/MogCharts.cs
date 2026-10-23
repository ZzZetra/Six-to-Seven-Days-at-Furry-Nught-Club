using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

// Раскладки мог-батла: Assets/StreamingAssets/MogCharts/*.json (подпапки не читаются — там примеры).
// Файл с ошибкой пропускается с предупреждением в Console. Нет ни одной рабочей — берётся встроенная
// MogChart.Default(). Формат — docs/MOG_CHARTS.md.
public static class MogCharts
{
    public static string Folder => Path.Combine(Application.streamingAssetsPath, "MogCharts");

    public static List<MogChart> LoadAll()
    {
        var list = new List<MogChart>();
        if (Directory.Exists(Folder))
        {
            foreach (var path in Directory.GetFiles(Folder, "*.json"))
            {
                string file = Path.GetFileName(path);
                try
                {
                    var chart = JsonUtility.FromJson<MogChart>(File.ReadAllText(path));
                    string error = chart == null ? "пустой файл" : chart.Validate();
                    if (error != null)
                    {
                        Debug.LogWarning($"[SSDFNC] Раскладка {file} пропущена: {error}");
                        continue;
                    }
                    if (string.IsNullOrEmpty(chart.name)) chart.name = Path.GetFileNameWithoutExtension(path);
                    list.Add(chart);
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[SSDFNC] Раскладка {file} не читается: {e.Message}");
                }
            }
        }
        if (list.Count == 0) list.Add(MogChart.Default());
        return list;
    }

    // Полный путь к музыке раскладки или null, если раскладка без музыки
    public static string MusicPath(MogChart chart) =>
        string.IsNullOrEmpty(chart.music) ? null : Path.Combine(Folder, chart.music);
}
