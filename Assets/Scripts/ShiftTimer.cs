using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Смена: идёт Duration секунд, часы в правом нижнем углу монитора камер показывают, сколько осталось («5:59»;
// без питания и при перезагрузке монитора их не видно — CameraMonitor).
// Время вышло — смена пройдена, экран «Ночь окончена» (NightEndScreen) с итогами, «Заново» и «В меню».
// Для проверки (в редакторе и dev-сборке): F9 сразу заканчивает смену.
public class ShiftTimer : MonoBehaviour
{
    // Длина смены в секундах. Меняется в Balance.cs (6 минут = 360).
    public const float Duration = Balance.ShiftDuration;

    public string menuScene = "MainMenu";
    public Text clock;
    public NightEndScreen nightEnd;   // не задан — сразу в меню

    float elapsed;

    public float TimeLeft => Mathf.Max(0f, Duration - elapsed);

    void Update()
    {
        elapsed += Time.deltaTime;
        if (Debug.isDebugBuild && !GameFlow.InputBlocked && Keyboard.current != null && Keyboard.current.f9Key.wasPressedThisFrame)
            elapsed = Duration;
        if (clock != null)
        {
            int seconds = Mathf.CeilToInt(TimeLeft);
            clock.text = $"{seconds / 60}:{seconds % 60:00}";
        }

        if (TimeLeft <= 0f)
        {
            enabled = false;
            Debug.Log("[SSDFNC] Смена окончена");
            if (nightEnd != null) nightEnd.Show();
            else SceneLoader.Load(menuScene, "Возвращаемся в меню…");
        }
    }
}
