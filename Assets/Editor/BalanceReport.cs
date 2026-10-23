using System.IO;
using UnityEditor;
using UnityEngine;

// Меню SSDFNC → Balance Report: симулирует ночи (NightSimulator) для всех сложностей и трёх ботов
// и пишет таблицы в docs/BALANCE_REPORT.md. Занимает пару секунд.
public static class BalanceReport
{
    const int Nights = 300;

    [MenuItem("SSDFNC/Balance Report")]
    public static void Write()
    {
        string path = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "docs", "BALANCE_REPORT.md");
        float shift = Balance.ShiftDuration; // переменная, а не константа: иначе предупреждение о недостижимом коде
        string text =
            "# Отчёт по балансу\n\n" +
            "Сгенерирован меню SSDFNC → Balance Report. Играет бот, а не человек: как он устроен — в docs/FURRY_AI.md.\n\n" +
            $"## Текущая смена ({shift:0} с)\n\n" + NightSimulator.Report(Nights, shift);
        if (shift != 600f) // смена укорочена для разработки — показать и смену по дизайну
            text += "\n## Смена 10 минут (по дизайну)\n\n" + NightSimulator.Report(Nights, 600f);
        File.WriteAllText(path, text);
        Debug.Log("[SSDFNC] Отчёт по балансу: " + path);
    }
}
