using UnityEngine;

// Первая сцена игры (Boot): сразу показывает загрузочный экран и грузит главное меню.
public class Boot : MonoBehaviour
{
    const float BootShowTime = 2f; // с, загрузочный экран при запуске игры

    public string menuScene = "MainMenu";

    void Start() => SceneLoader.Load(menuScene, "Загрузка клуба…", BootShowTime);
}
