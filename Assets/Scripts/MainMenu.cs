using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Главное меню с двумя вкладками: «Главная» («Начать ночь», «Выйти») и «Настройка ночи»
// (уровни фурри, см. NightSettingsTab). «Начать ночь» загружает офис, «Выйти» закрывает игру.
// В правом верхнем углу — деньги игрока (Wallet), заработанные на звонках.
public class MainMenu : MonoBehaviour
{
    static readonly Color TabOn = new Color(0.45f, 0.25f, 0.6f);
    static readonly Color TabOff = new Color(0.18f, 0.12f, 0.24f);

    public Button startButton;
    public Button quitButton;
    public Button settingsStartButton; // «Начать ночь» на вкладке настройки
    public Button mainTabButton;
    public Button settingsTabButton;
    public GameObject mainTab;
    public GameObject settingsTab;
    public Text moneyLabel;
    public string gameScene = "SampleScene";

    void Awake()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        startButton.onClick.AddListener(StartNight);
        quitButton.onClick.AddListener(Quit);
        settingsStartButton.onClick.AddListener(StartNight);
        mainTabButton.onClick.AddListener(() => ShowTab(false));
        settingsTabButton.onClick.AddListener(() => ShowTab(true));
        ShowTab(false);
        if (moneyLabel != null) moneyLabel.text = $"Деньги: {Wallet.Money} $";
    }

    public void ShowTab(bool settings)
    {
        mainTab.SetActive(!settings);
        settingsTab.SetActive(settings);
        mainTabButton.image.color = settings ? TabOff : TabOn;
        settingsTabButton.image.color = settings ? TabOn : TabOff;
    }

    public void StartNight()
    {
        Debug.Log("[SSDFNC] Начать ночь: " + NightSettings.Describe());
        SceneManager.LoadScene(gameScene);
    }

    public void Quit()
    {
        Debug.Log("[SSDFNC] Выйти");
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false; // в редакторе Application.Quit ничего не делает
#else
        Application.Quit();
#endif
    }
}
