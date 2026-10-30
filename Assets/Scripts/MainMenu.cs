using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Главное меню с тремя вкладками: «Главная» («Начать ночь», «Выйти»), «Настройка ночи»
// (уровни фурри, см. NightSettingsTab) и «Скилл-чеки» (проверка раскладок мог-батла, см. MogTestTab).
// Сами раскладки делаются в отдельной программе — редакторе раскладок (MogChartEditor).
// «Начать ночь» загружает офис, «Выйти» закрывает игру.
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
    public Button checksTabButton;
    public GameObject mainTab;
    public GameObject settingsTab;
    public GameObject checksTab;
    public MogTestTab checksTest;
    public Text moneyLabel;
    public string gameScene = "SampleScene";

    void Awake()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        startButton.onClick.AddListener(StartNight);
        quitButton.onClick.AddListener(Quit);
        settingsStartButton.onClick.AddListener(StartNight);
        mainTabButton.onClick.AddListener(() => ShowTab(0));
        settingsTabButton.onClick.AddListener(() => ShowTab(1));
        if (checksTabButton != null) checksTabButton.onClick.AddListener(() => ShowTab(2));
        ShowTab(0);
        if (moneyLabel != null) moneyLabel.text = $"Деньги: {Wallet.Money} $";
    }

    // 0 — «Главная», 1 — «Настройка ночи», 2 — «Скилл-чеки»
    public void ShowTab(int index)
    {
        mainTab.SetActive(index == 0);
        settingsTab.SetActive(index == 1);
        if (checksTab != null) checksTab.SetActive(index == 2);
        mainTabButton.image.color = index == 0 ? TabOn : TabOff;
        settingsTabButton.image.color = index == 1 ? TabOn : TabOff;
        if (checksTabButton != null) checksTabButton.image.color = index == 2 ? TabOn : TabOff;
        if (index == 2 && checksTest != null) checksTest.Refresh();
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
