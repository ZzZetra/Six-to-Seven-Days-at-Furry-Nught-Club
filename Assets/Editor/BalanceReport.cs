using System.IO;
using UnityEditor;
using UnityEngine;

// Меню SSDFNC → Balance Report: симулирует ночи (NightSimulator) — ночи прохождения 1–7 (Nights.cs) и пресеты
// Easy / Normal / Hard для «Своей ночи» — для трёх ботов и пишет таблицы в docs/BALANCE_REPORT.md. Занимает пару секунд.
public static class BalanceReport
{
    const int Nights = 300;

    [MenuItem("SSDFNC/Balance Report")]
    public static void Write()
    {
        string path = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "docs", "BALANCE_REPORT.md");
        float shift = Balance.ShiftDuration;
        string text =
            "# Отчёт по балансу\n\n" +
            "Сгенерирован меню SSDFNC → Balance Report. Играет бот, а не человек: как он устроен — в docs/FURRY_AI.md.\n\n" +
            $"## Ночи 1–7 (смена {shift:0} с)\n\nУровни — Assets/Scripts/Core/Nights.cs.\n\n" + NightSimulator.NightsReport(Nights) +
            $"\n## Пресеты Easy / Normal / Hard для «Своей ночи» (смена {shift:0} с)\n\n" + NightSimulator.Report(Nights, shift);
        File.WriteAllText(path, text);
        Debug.Log("[SSDFNC] Отчёт по балансу: " + path);
    }
}
