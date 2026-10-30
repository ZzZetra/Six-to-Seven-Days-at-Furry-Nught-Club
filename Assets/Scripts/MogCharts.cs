using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

// Раскладки мог-батла: Assets/StreamingAssets/MogCharts/*.json. Атака Новы берёт только файлы прямо в папке,
// проверка скилл-чеков и редактор раскладок — и примеры из подпапок. Файл с ошибкой пропускается с предупреждением в
// Console. Нет ни одной рабочей — берётся встроенная MogChart.Default(). Формат — docs/MOG_CHARTS.md.
public static class MogCharts
{
    static string folder;

    // Папка раскладок. Редактор раскладок — отдельная программа — ставит сюда папку проекта
    public static string Folder
    {
        get => folder ?? Path.Combine(Application.streamingAssetsPath, "MogCharts");
        set => folder = value;
    }

    public static List<MogChart> LoadAll(bool withExamples = false)
    {
        var list = new List<MogChart>();
        foreach (var (path, chart) in LoadFiles(withExamples)) list.Add(chart);
        if (list.Count == 0) list.Add(MogChart.Default());
        return list;
    }

    // Пары «файл — раскладка», только рабочие
    public static List<(string path, MogChart chart)> LoadFiles(bool withExamples)
    {
        var list = new List<(string, MogChart)>();
        if (!Directory.Exists(Folder)) return list;
        var option = withExamples ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        foreach (var path in Directory.GetFiles(Folder, "*.json", option))
        {
            var chart = Load(path, out string error);
            if (chart != null) list.Add((path, chart));
            else Debug.LogWarning($"[SSDFNC] Раскладка {Path.GetFileName(path)} пропущена: {error}");
        }
        return list;
    }

    public static MogChart Load(string path, out string error)
    {
        try
        {
            var chart = JsonUtility.FromJson<MogChart>(File.ReadAllText(path));
            error = chart == null ? "пустой файл" : chart.Validate();
            if (error != null) return null;
            if (string.IsNullOrEmpty(chart.name)) chart.name = Path.GetFileNameWithoutExtension(path);
            return chart;
        }
        catch (Exception e)
        {
            error = e.Message;
            return null;
        }
    }

    // Полный путь к музыке раскладки или null, если раскладка без музыки
    public static string MusicPath(MogChart chart) =>
        string.IsNullOrEmpty(chart.music) ? null : Path.Combine(Folder, chart.music);

    // Загрузить музыку из файла (.ogg, .wav, .mp3). Не вышло — done(null) и предупреждение в Console
    public static IEnumerator LoadClip(string path, Action<AudioClip> done)
    {
        using (var req = UnityWebRequestMultimedia.GetAudioClip("file:///" + path.Replace('\\', '/'), AudioTypeOf(path)))
        {
            ((DownloadHandlerAudioClip)req.downloadHandler).streamAudio = false; // целиком: нужна полоска звука
            yield return req.SendWebRequest();
            if (req.result == UnityWebRequest.Result.Success)
            {
                var clip = DownloadHandlerAudioClip.GetContent(req);
                clip.name = Path.GetFileNameWithoutExtension(path);
                done(clip);
            }
            else
            {
                Debug.LogWarning($"[SSDFNC] Музыка {Path.GetFileName(path)} не загрузилась: {req.error}");
                done(null);
            }
        }
    }

    static AudioType AudioTypeOf(string path)
    {
        switch (Path.GetExtension(path).ToLowerInvariant())
        {
            case ".ogg": return AudioType.OGGVORBIS;
            case ".wav": return AudioType.WAV;
            case ".mp3": return AudioType.MPEG;
            default: return AudioType.UNKNOWN;
        }
    }
}
