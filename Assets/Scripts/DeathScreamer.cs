using UnityEngine;
using UnityEngine.UI;

// Скример на экране смерти: голова фурри, который убил, бросается на игрока и трясётся поверх немного затемнённой
// игры; потом NightDirector гасит экран в чёрный (SetAlpha, Hide) и пишет причину смерти (пользователь, 2026-11-06;
// Дир — без головы, чёрный экран «СКОНЧАЛСЯ»).
// Головы — копии моделей фурри (только меши головы) на своей «сцене» далеко под картой; их снимает своя камера в
// RenderTexture, его показывает RawImage на экране смерти. Строит сборщик офиса (TestPlatformBuilder.BuildDeathHeads).
// После смерти игра стоит (Time.timeScale = 0) — время здесь без учёта паузы. Все числа — константы ниже.
public class DeathScreamer : MonoBehaviour
{
    const float LungeTime = 0.16f;     // с: голова подлетает из глубины к камере
    const float LungeFrom = 2.5f;      // м: насколько дальше она в начале
    const float ShakeTime = 0.9f;      // с: сильная тряска после броска, потом слабеет до Twitch
    const float ShakeAngle = 9f;       // градусов
    const float ShakeOffset = 0.035f;  // м
    const float Twitch = 0.15f;        // доля тряски, которая остаётся, пока экран смерти открыт
    const int TextureSize = 1024;

    public Camera headCamera;
    public RawImage image;
    public Transform[] heads;          // по FurryId; null — головы нет (у Дира, или нет модели)

    Transform current;
    Vector3 basePosition;
    Vector3[] bases;               // где головы стоят по сборщику (тряска их двигает)
    RenderTexture texture;
    float time;

    public bool Has(FurryId id) => heads != null && (int)id < heads.Length && heads[(int)id] != null;

    void Awake()
    {
        bases = new Vector3[heads.Length];
        for (int i = 0; i < heads.Length; i++)
        {
            if (heads[i] == null) continue;
            bases[i] = heads[i].localPosition;
            heads[i].gameObject.SetActive(false);
        }
        headCamera.enabled = false;
        image.enabled = false;
    }

    void OnDestroy()
    {
        if (texture != null) texture.Release();
    }

    public void Show(FurryId id)
    {
        if (!Has(id)) return;
        if (texture == null) texture = new RenderTexture(TextureSize, TextureSize, 24) { name = "DeathHead" };
        headCamera.targetTexture = texture;
        headCamera.enabled = true;
        image.texture = texture;
        image.enabled = true;
        SetAlpha(1f);
        current = heads[(int)id];
        basePosition = bases[(int)id];
        current.gameObject.SetActive(true);
        time = 0f;
        Update();
    }

    // Экран гаснет — голова исчезает вместе с ним
    public void SetAlpha(float alpha) => image.color = new Color(1f, 1f, 1f, Mathf.Clamp01(alpha));

    public void Hide()
    {
        if (current != null) current.gameObject.SetActive(false);
        current = null;
        headCamera.enabled = false;
        image.enabled = false;
    }

    void Update()
    {
        if (current == null) return;
        time += Time.unscaledDeltaTime;
        float lunge = Mathf.Clamp01(time / LungeTime);
        float back = LungeFrom * (1f - lunge) * (1f - lunge); // быстро подлетает и резко встаёт
        float shake = Mathf.Lerp(1f, Twitch, Mathf.Clamp01((time - LungeTime) / ShakeTime));
        current.localPosition = basePosition + new Vector3(Random.Range(-1f, 1f), Random.Range(-1f, 1f), 0f) * (ShakeOffset * shake)
                                - Vector3.forward * back;
        current.localRotation = Quaternion.Euler(Random.Range(-1f, 1f) * ShakeAngle * shake,
            Random.Range(-1f, 1f) * ShakeAngle * shake, Random.Range(-1f, 1f) * ShakeAngle * shake);
    }
}
