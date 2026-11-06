using UnityEngine;

// Общее состояние игры: пауза (Esc), экран «Ночь окончена», идёт загрузка. Пока InputBlocked, скрипты
// офиса не читают клавиатуру и мышь (поворот камеры, пробел, звонки, мог-батл, F1, F9…).
// Время на паузе и после конца ночи стоит (Time.timeScale = 0), звук тоже (AudioListener.pause).
public static class GameFlow
{
    public static bool Paused { get; private set; }
    public static bool NightOver { get; private set; }
    public static bool InputBlocked => Paused || NightOver || SceneLoader.IsLoading;

    public static void SetPaused(bool on)
    {
        Paused = on;
        Freeze(on || NightOver);
    }

    public static void EndNight()
    {
        NightOver = true;
        Freeze(true);
    }

    // Перед загрузкой сцены и при запуске: всё снова идёт
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    public static void Reset()
    {
        Paused = NightOver = false;
        Freeze(false);
    }

    static void Freeze(bool on)
    {
        Time.timeScale = on ? 0f : 1f;
        AudioListener.pause = on;
        if (on)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }
}
