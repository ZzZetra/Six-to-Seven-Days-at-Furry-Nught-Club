using UnityEngine;
using UnityEngine.UI;

// Помехи на мониторе камер, как у старого монитора видеонаблюдения (поверх кадра, под надписями и кнопками):
//   строки развёртки, «снег», медленно ползущая светлая полоса, тёмные края;
//   изредка сбой — яркие цветные полосы поперёк экрана и кадр дёргается вбок;
//   переключил камеру — короткая вспышка помех (BurstTime); камера 1 (не работает) — сплошные помехи (NoSignalAlpha).
//   Экран выключен — помех нет.
// Текстуры делаются здесь в коде, все числа — константы ниже. На паузе стоит.
public class MonitorStatic : MonoBehaviour
{
    const float ScanlineStep = 4f;        // px экрана между строками развёртки
    const float ScanlineAlpha = 0.45f;
    const float NoiseAlpha = 0.12f;       // «снег» всегда
    const float BurstAlpha = 0.75f;       // «снег» при переключении камеры
    const float BurstTime = 0.25f;        // с
    const float NoSignalAlpha = 0.85f;    // «снег» на неработающей камере
    const float BarAlpha = 0.07f, BarSpeed = 0.22f; // светлая полоса: яркость, экранов в секунду
    const float VignetteAlpha = 0.55f;
    const float GlitchEveryMin = 2.5f, GlitchEveryMax = 7f; // с между сбоями
    const float GlitchTimeMin = 0.06f, GlitchTimeMax = 0.22f;
    const int GlitchLines = 6;            // полос за сбой (до)
    const float JitterPx = 7f;            // насколько дёргается кадр при сбое
    const int NoiseSize = 256;
    const float NoiseGrain = 2f;          // px экрана на крупинку «снега»

    public RawImage scanlines, noise, bar, vignette;
    public Image[] lines;                 // полосы сбоя (по GlitchLines)
    public RawImage feed;                 // кадр камеры — его дёргает при сбое

    static readonly Color[] LineColors =
    {
        new Color(1f, 1f, 1f, 0.55f), new Color(0.55f, 1f, 0.75f, 0.5f), new Color(1f, 0.4f, 0.9f, 0.45f),
        new Color(0.4f, 0.85f, 1f, 0.5f), new Color(0f, 0f, 0f, 0.6f),
    };

    bool on = true, glitchStarted, noSignal;
    float burst, glitch, nextGlitch, barPos;
    RectTransform rect;

    void Awake()
    {
        rect = (RectTransform)transform;
        scanlines.texture = MakeScanlines();
        noise.texture = MakeNoise();
        bar.texture = MakeBar();
        vignette.texture = MakeVignette();
        scanlines.color = new Color(1f, 1f, 1f, ScanlineAlpha);
        vignette.color = new Color(1f, 1f, 1f, VignetteAlpha);
        nextGlitch = Random.Range(GlitchEveryMin, GlitchEveryMax);
        foreach (var l in lines) l.enabled = false;
    }

    // Экран включился / выключился (нет питания, перезагрузка)
    public void SetOn(bool value)
    {
        on = value;
        gameObject.SetActive(value);
    }

    // Переключили камеру — вспышка помех
    public void Burst() => burst = BurstTime;

    // Камера не работает — сплошной «снег»
    public void SetNoSignal(bool value) => noSignal = value;

    void Update()
    {
        if (!on) return;
        float dt = Time.deltaTime;
        float h = rect.rect.height;

        scanlines.uvRect = new Rect(0f, 0f, 1f, h / ScanlineStep);
        burst = Mathf.Max(0f, burst - dt);
        float k = burst / BurstTime;
        noise.color = new Color(1f, 1f, 1f, noSignal ? NoSignalAlpha : Mathf.Lerp(NoiseAlpha, BurstAlpha, k));
        if (dt > 0f) // «снег» меняется каждый кадр: тот же шум, другой кусок
        {
            float scale = rect.rect.width / (NoiseSize * NoiseGrain);
            noise.uvRect = new Rect(Random.value, Random.value, scale, scale * h / rect.rect.width);
        }

        barPos = Mathf.Repeat(barPos + BarSpeed * dt, 1.4f);
        bar.rectTransform.anchorMin = new Vector2(0f, 1.2f - barPos - 0.2f);
        bar.rectTransform.anchorMax = new Vector2(1f, 1.2f - barPos);
        bar.color = new Color(1f, 1f, 1f, BarAlpha);

        nextGlitch -= dt;
        if (nextGlitch <= 0f)
        {
            nextGlitch = Random.Range(GlitchEveryMin, GlitchEveryMax);
            glitch = Random.Range(GlitchTimeMin, GlitchTimeMax);
            glitchStarted = true; // в этом кадре не убывает: полосы видны хотя бы кадр и при низком FPS
            int n = Random.Range(2, GlitchLines + 1);
            for (int i = 0; i < lines.Length; i++)
            {
                lines[i].enabled = i < n;
                if (i >= n) continue;
                var lr = lines[i].rectTransform;
                float y = Random.value;
                float thick = Random.Range(1.5f, 9f) / h;
                lr.anchorMin = new Vector2(0f, y);
                lr.anchorMax = new Vector2(1f, Mathf.Min(1f, y + thick));
                lines[i].color = LineColors[Random.Range(0, LineColors.Length)];
            }
        }
        if (glitch > 0f || burst > 0f)
        {
            if (!glitchStarted) glitch -= dt;
            glitchStarted = false;
            if (feed != null) feed.rectTransform.anchoredPosition = new Vector2(Random.Range(-JitterPx, JitterPx), 0f);
            if (glitch <= 0f) foreach (var l in lines) l.enabled = false;
        }
        else if (feed != null) feed.rectTransform.anchoredPosition = Vector2.zero;
    }

    static Texture2D MakeScanlines()
    {
        var t = new Texture2D(1, 4, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear, name = "Scanlines" };
        t.SetPixels(new[] { new Color(0, 0, 0, 1f), new Color(0, 0, 0, 0.45f), new Color(0, 0, 0, 0f), new Color(0, 0, 0, 0f) });
        t.Apply();
        return t;
    }

    static Texture2D MakeNoise()
    {
        var t = new Texture2D(NoiseSize, NoiseSize, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Point, name = "Static" };
        var px = new Color32[NoiseSize * NoiseSize];
        var rng = new System.Random(7);
        for (int i = 0; i < px.Length; i++)
        {
            byte v = (byte)rng.Next(256);
            px[i] = new Color32(v, v, v, (byte)(80 + rng.Next(176)));
        }
        t.SetPixels32(px);
        t.Apply();
        return t;
    }

    static Texture2D MakeBar()
    {
        var t = new Texture2D(1, 32, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "RollBar" };
        for (int y = 0; y < 32; y++) t.SetPixel(0, y, new Color(1, 1, 1, Mathf.Sin(y / 31f * Mathf.PI)));
        t.Apply();
        return t;
    }

    static Texture2D MakeVignette()
    {
        const int w = 64, h = 36;
        var t = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "Vignette" };
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float dx = (x + 0.5f) / w * 2f - 1f, dy = (y + 0.5f) / h * 2f - 1f;
                float d = Mathf.Sqrt(dx * dx * 0.8f + dy * dy * 0.8f);
                t.SetPixel(x, y, new Color(0, 0, 0, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.55f, 1.25f, d))));
            }
        t.Apply();
        return t;
    }
}
