using UnityEngine;
using UnityEngine.UI;

// Вкладка «Настройка ночи» в главном меню: уровень каждого фурри кнопками − / +, пресеты,
// «Только Нова», «По умолчанию» (сложность из Difficulty.cs). Пишет в NightSettings.
public class NightSettingsTab : MonoBehaviour
{
    public Text[] values;     // по FurryId
    public Button[] minus;    // по FurryId
    public Button[] plus;     // по FurryId
    public Button easy, normal, hard, onlyNova, defaults;
    public Text summary;

    void Awake()
    {
        for (int i = 0; i < values.Length; i++)
        {
            var id = (FurryId)i;
            minus[i].onClick.AddListener(() => Change(id, -1));
            plus[i].onClick.AddListener(() => Change(id, +1));
        }
        easy.onClick.AddListener(() => SetAll(Difficulty.Levels(DifficultyPreset.Easy)));
        normal.onClick.AddListener(() => SetAll(Difficulty.Levels(DifficultyPreset.Normal)));
        hard.onClick.AddListener(() => SetAll(Difficulty.Levels(DifficultyPreset.Hard)));
        onlyNova.onClick.AddListener(() => SetAll(new[] { 0, 0, 20, 0, 0 }));
        defaults.onClick.AddListener(() =>
        {
            NightSettings.ResetToCode();
            Redraw();
        });
    }

    void OnEnable() => Redraw();

    void Change(FurryId id, int delta)
    {
        NightSettings.Set(id, NightSettings.Levels[(int)id] + delta);
        Redraw();
    }

    void SetAll(int[] levels)
    {
        NightSettings.SetAll(levels);
        Redraw();
    }

    void Redraw()
    {
        var levels = NightSettings.Levels;
        for (int i = 0; i < values.Length; i++) values[i].text = levels[i] == 0 ? "выкл" : levels[i].ToString();
        summary.text = "Сложность ночи: " + NightSettings.Describe();
    }
}
