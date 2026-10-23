using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Панель сложности во время ночи: F1 — открыть / закрыть (в редакторе и dev-сборке).
// Уровень ИИ каждого фурри кнопками − / + (0 — выключен, до 20), пресеты Easy / Normal / Hard,
// «Только Нова» (Нова 20, остальные выключены) и «Нова → батл» (сразу на дэнс пол — мог-батл).
// Меняет уровни только текущей ночи; постоянная сложность — в Difficulty.cs.
public class DifficultyPanel : MonoBehaviour
{
    public NightDirector director;
    public GameObject panel;
    public Text hint;                 // «F1 — сложность» в углу экрана
    public Text[] values;             // по FurryId
    public Button[] minus;            // по FurryId
    public Button[] plus;             // по FurryId
    public Button easy, normal, hard, onlyNova, novaBattle;
    public Text info;

    Night Night => director.Night;

    void Awake()
    {
        for (int i = 0; i < values.Length; i++)
        {
            var id = (FurryId)i;
            minus[i].onClick.AddListener(() => Change(id, -1));
            plus[i].onClick.AddListener(() => Change(id, +1));
        }
        easy.onClick.AddListener(() => SetAll(Difficulty.Levels(DifficultyPreset.Easy), "Easy"));
        normal.onClick.AddListener(() => SetAll(Difficulty.Levels(DifficultyPreset.Normal), "Normal"));
        hard.onClick.AddListener(() => SetAll(Difficulty.Levels(DifficultyPreset.Hard), "Hard"));
        onlyNova.onClick.AddListener(() => SetAll(new[] { 0, 0, 20, 0, 0 }, "только Нова"));
        novaBattle.onClick.AddListener(() =>
        {
            Debug.Log("[SSDFNC] Панель F1: Нова → дэнс пол");
            Night.ForceNovaBattle();
        });
        panel.SetActive(false);
        hint.gameObject.SetActive(Debug.isDebugBuild);
    }

    void Update()
    {
        var kb = Keyboard.current;
        if (Debug.isDebugBuild && kb != null && kb.f1Key.wasPressedThisFrame) panel.SetActive(!panel.activeSelf);
        if (!panel.activeSelf) return;

        for (int i = 0; i < values.Length; i++)
        {
            var f = Night.All[i];
            values[i].text = !f.Active ? "выкл" : f.Level == f.BaseLevel ? f.BaseLevel.ToString() : $"{f.BaseLevel} → {f.Level}";
        }
        info.text = $"Четверть смены: {Night.Quarter + 1}/4, рост +{Night.Quarter * Difficulty.RampPerQuarter}" +
                    (Night.Nova.RestLeft > 0f ? $"\nНова отдыхает ещё {Night.Nova.RestLeft:0} с" : "");
    }

    void Change(FurryId id, int delta)
    {
        var f = Night.All[(int)id];
        f.SetBaseLevel(f.BaseLevel + delta);
        Debug.Log($"[SSDFNC] Панель F1: {f.Name} = {(f.Active ? f.BaseLevel.ToString() : "выкл")}");
    }

    void SetAll(int[] levels, string name)
    {
        for (int i = 0; i < levels.Length; i++) Night.All[i].SetBaseLevel(levels[i]);
        Debug.Log($"[SSDFNC] Панель F1: {name} — {string.Join("/", levels)}");
    }
}
