using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Экран «Ночь N пройдена»: смена дошла до конца (ShiftTimer). Ночь записывается в прохождение (NightProgress),
// всё замирает (GameFlow.EndNight), на экране — итоги ночи и кнопки «Следующая ночь» (если она открыта),
// «Заново» и «В меню». Следующая закрыта (7-я без денег) — на кнопке написано почему.
public class NightEndScreen : MonoBehaviour
{
    const float FadeTime = 0.5f;
    const float LockDelay = 0f; // с: замок начинается через столько после начала «Конца ночи» (0 — одновременно)

    public GameObject panel;
    public CanvasGroup group;
    public Text title;
    public Text stats;
    public Button nextButton, restartButton, menuButton;
    public Text nextLabel;
    public NightDirector director;
    public CallDirector calls;
    public string menuScene = "MainMenu";

    float shownAt = -1f;

    public bool Shown => shownAt >= 0f;

    void Awake()
    {
        restartButton.onClick.AddListener(() =>
            SceneLoader.Load(SceneManager.GetActiveScene().name, $"{NightProgress.Name} · заново…"));
        if (nextButton != null)
            nextButton.onClick.AddListener(() =>
            {
                int next = NightProgress.Next;
                if (next == 0) return;
                NightProgress.Select(next);
                SceneLoader.Load(SceneManager.GetActiveScene().name, $"Ночь {next}…");
            });
        menuButton.onClick.AddListener(() => SceneLoader.Load(menuScene, "Возвращаемся в меню…"));
        panel.SetActive(false);
    }

    public void Show()
    {
        if (Shown || (director != null && director.Night.Over)) return; // погиб — экран смерти, не этот
        shownAt = Time.unscaledTime;
        GameFlow.EndNight();
        NightProgress.MarkPassed();
        // «Конец ночи» и вместе с ним замок — уходим и запираем за собой. Остальной звук на этом экране стоит
        GameSounds.PlayPair(SoundSlot.NightEnd, SoundSlot.NightEndLock, LockDelay, ui: true);
        if (title != null) title.text = NightProgress.IsCustom ? "НОЧЬ ОКОНЧЕНА" : $"НОЧЬ {NightProgress.Current} ПРОЙДЕНА";
        ShowNext();
        stats.text = Stats();
        group.alpha = 0f;
        panel.SetActive(true);
        Debug.Log("[SSDFNC] Ночь окончена: " + stats.text.Replace("\n", " · "));
    }

    void Update()
    {
        if (Shown) group.alpha = Mathf.Clamp01((Time.unscaledTime - shownAt) / FadeTime);
    }

    // «Следующая ночь»: открыта — можно нажать; 7-я без денег — серая и с причиной; дальше ночей нет — кнопки нет
    void ShowNext()
    {
        if (nextButton == null) return;
        int following = NightProgress.Current + 1;
        bool exists = !NightProgress.IsCustom && following <= Nights.Count;
        nextButton.gameObject.SetActive(exists);
        if (!exists) return;
        string reason = NightProgress.LockReason(following);
        nextButton.interactable = reason == null;
        if (nextLabel != null) nextLabel.text = reason == null ? $"Ночь {following}" : $"Ночь {following}: {reason}";
    }

    string Stats()
    {
        var lines = new System.Collections.Generic.List<string>();
        if (director != null)
        {
            lines.Add($"Дир накормлен: {director.Feeds} раз");
            lines.Add($"Мог-батлов выиграно: {director.Night.Nova.Wins}");
        }
        if (calls != null)
            lines.Add($"Звонки: верно {calls.Right}, ошибок {calls.Answered - calls.Right}, пропущено {calls.Missed} · +{calls.Earned} $");
        lines.Add($"Деньги всего: {Wallet.Money} $");
        return string.Join("\n", lines);
    }
}
