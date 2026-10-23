using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Смена: идёт Duration секунд, часы в углу экрана показывают, сколько осталось.
// Время вышло — смена пройдена, возврат в главное меню.
// Для проверки (в редакторе и dev-сборке): F9 сразу заканчивает смену.
public class ShiftTimer : MonoBehaviour
{
    // Длина смены в секундах. Меняется в Balance.cs (10 минут = 600).
    public const float Duration = Balance.ShiftDuration;

    public string menuScene = "MainMenu";
    public Text clock;

    float elapsed;

    public float TimeLeft => Mathf.Max(0f, Duration - elapsed);

    void Update()
    {
        elapsed += Time.deltaTime;
        if (Debug.isDebugBuild && Keyboard.current != null && Keyboard.current.f9Key.wasPressedThisFrame)
            elapsed = Duration;
        if (clock != null)
        {
            int seconds = Mathf.CeilToInt(TimeLeft);
            clock.text = $"{seconds / 60}:{seconds % 60:00}";
        }

        if (TimeLeft <= 0f)
        {
            enabled = false;
            Debug.Log("[SSDFNC] Смена окончена, возврат в меню");
            SceneManager.LoadScene(menuScene);
        }
    }
}
