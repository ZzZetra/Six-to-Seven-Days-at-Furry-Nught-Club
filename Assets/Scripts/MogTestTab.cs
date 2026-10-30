using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Проверка скилл-чеков (вкладка «Скилл-чеки» в главном меню): список раскладок из
// StreamingAssets/MogCharts (вместе с примерами), клик — раскладка проигрывается в том же окне, что атака Новы
// (MogBattle, с музыкой), без появления Новы — сразу с первого чека, чеки один за другим. Меню на это время
// прячется. В конце — итог; Esc — прервать.
public class MogTestTab : MonoBehaviour
{
    const int MaxRows = 6;
    const float RowHeight = 66f;

    public MogBattle battle;
    public CanvasGroup menu;        // главное меню: прячется, пока идёт проверка
    public RectTransform list;      // сюда кнопки раскладок
    public Button template;         // образец кнопки (выключен)
    public Text result;

    readonly List<GameObject> rows = new List<GameObject>();
    MogChart playing;

    // Перечитать раскладки (зовёт MainMenu, когда открывают вкладку): новые файлы видны без перезапуска
    public void Refresh()
    {
        foreach (var row in rows) Destroy(row);
        rows.Clear();
        var files = MogCharts.LoadFiles(withExamples: true);
        if (files.Count == 0) files.Add(("", MogChart.Default()));
        for (int i = 0; i < files.Count && i < MaxRows; i++)
        {
            var (path, chart) = files[i];
            bool example = path.Replace('\\', '/').Contains("/examples/") && !chart.name.Contains("пример");
            var button = Instantiate(template, list);
            button.gameObject.SetActive(true);
            ((RectTransform)button.transform).anchoredPosition = new Vector2(0f, -i * RowHeight);
            button.GetComponentInChildren<Text>().text =
                $"{chart.name}  ·  {chart.checks.Length} чеков{(example ? "  (пример)" : "")}" +
                (string.IsNullOrEmpty(chart.music) ? "" : $"  ·  ♪ {Path.GetFileName(chart.music)}");
            button.onClick.AddListener(() => Play(chart));
            rows.Add(button.gameObject);
        }
        if (files.Count > MaxRows) result.text = $"Показаны первые {MaxRows} раскладок из {files.Count}";
    }

    void Play(MogChart chart)
    {
        playing = chart;
        result.text = "";
        SetMenuVisible(false);
        battle.BeginTest(chart, won =>
        {
            SetMenuVisible(true);
            var t = battle.Timeline;
            result.text = $"«{chart.name}»: " + (won ? "пройдено" : $"провал на чеке {t.Index} из {t.Count}") +
                          $" — попаданий {t.Hits}, промахов {t.Misses}, мог {t.Lives}/{Balance.MogMaxLives}";
        });
    }

    void Update()
    {
        var kb = Keyboard.current;
        if (!battle.Running || kb == null || !kb.escapeKey.wasPressedThisFrame) return;
        battle.Abort();
        SetMenuVisible(true);
        result.text = $"«{playing.name}»: прервано";
    }

    void SetMenuVisible(bool on)
    {
        menu.alpha = on ? 1f : 0f;
        menu.interactable = on;
        menu.blocksRaycasts = on;
    }
}
