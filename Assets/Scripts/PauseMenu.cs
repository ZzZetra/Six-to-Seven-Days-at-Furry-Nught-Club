using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Меню паузы в офисе: Esc — открыть / закрыть. «Продолжить», «Начать ночь заново», «Звуки» (SoundSettingsPanel:
// сменить звук любого события прямо во время ночи; Esc там — назад), «В главное меню», «Выйти из игры». На паузе время и звук стоят (GameFlow), клики в сцену не проходят (панель на весь экран).
// Не открывается, пока идёт загрузка, после смерти и на экране «Ночь окончена».
public class PauseMenu : MonoBehaviour
{
    public GameObject panel;
    public GameObject card; // карточка с кнопками: прячется, пока открыты «Звуки»
    public Button resumeButton, restartButton, menuButton, quitButton, soundsButton;
    public SoundSettingsPanel sounds;
    public NightDirector director;
    public string menuScene = "MainMenu";

    void Awake()
    {
        resumeButton.onClick.AddListener(() => SetOpen(false));
        restartButton.onClick.AddListener(Restart);
        menuButton.onClick.AddListener(() => SceneLoader.Load(menuScene, "Возвращаемся в меню…"));
        quitButton.onClick.AddListener(Quit);
        if (soundsButton != null && sounds != null)
        {
            soundsButton.onClick.AddListener(OpenSounds);
            sounds.Closed += () => card.SetActive(true);
            sounds.gameObject.SetActive(false);
        }
        panel.SetActive(false);
    }

    void Update()
    {
        var kb = Keyboard.current;
        if (kb == null || !kb.escapeKey.wasPressedThisFrame) return;
        if (SceneLoader.IsLoading || GameFlow.NightOver || (director != null && director.Night.Over)) return;
        if (sounds != null && sounds.IsOpen) sounds.Close(); // из «Звуков» — назад в паузу
        else SetOpen(!GameFlow.Paused);
    }

    void OpenSounds()
    {
        card.SetActive(false);
        sounds.Open();
    }

    void SetOpen(bool open)
    {
        if (!open && sounds != null && sounds.IsOpen) sounds.Close();
        GameFlow.SetPaused(open);
        panel.SetActive(open);
        Debug.Log(open ? "[SSDFNC] Пауза" : "[SSDFNC] Продолжаем");
    }

    void Restart() => SceneLoader.Load(SceneManager.GetActiveScene().name, "Начинаем ночь заново…");

    void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
