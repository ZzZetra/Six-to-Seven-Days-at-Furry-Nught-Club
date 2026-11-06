using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

// Лампы клуба (ставит сборщик офиса из MapBuilder.Lamps) — все от генератора:
//   генератор встал или сломался — лампы OffFlickerTime мигают вразнобой и гаснут, светящиеся материалы (лампа на
//     столе, вывески) тоже; завёлся — коротко моргнули и загорелись;
//   пока всё работает — редко (раз в IdleEveryMin–Max с) моргают одна-две лампы;
//   в генераторной лампа моргает тем чаще и глубже, чем меньше бензина: с LowFuelStart (50%) — изредка,
//     к LowFuelStrong (20%) и ниже — очень сильно;
//   фоновый свет (заполняет тени) — тоже от генератора: AmbientOn, без питания — AmbientOff, мигает вместе с лампами;
//   неоновый вариант (меню → «Настройки» → «Свет», в офисе F4): обычные лампы тусклее (NeonLampScale), вдоль краёв
//     потолка горят неоновые полосы, свет от них идёт ровно вдоль стен (лампы по длине полос), фон чуть фиолетовый.
// Все числа — константы ниже. На паузе стоит (Time.deltaTime = 0).
public class OfficeLights : MonoBehaviour
{
    public enum Role { Office, Generator, Corridor, Corridor2, Passage, Neon }

    const float OffFlickerTime = 1.4f;   // с, мигание перед тем, как погаснуть
    const float OnFlickerTime = 0.5f;    // с, моргание при включении
    const float IdleEveryMin = 12f, IdleEveryMax = 35f; // с между случайными миганиями
    const float IdleTimeMin = 0.15f, IdleTimeMax = 0.6f; // с, сколько мигает
    const float LowFuelStart = 0.5f;     // доля бензина, с которой моргает генераторная
    const float LowFuelStrong = 0.2f;    // и с которой — очень сильно
    const float LowFuelRateWeak = 0.12f, LowFuelRateStrong = 2.5f; // миганий в секунду: в начале и в самом сильном
    const float NeonLampScale = 0.4f;    // обычные лампы в неоновом варианте
    public const string NeonKey = "SSDFNC.Neon";
    // Фоновый свет: с генератором (обычный и неон) и без него
    // 2026-11-06: чуть светлее (пользователь: «даже генератор в темноте не видно»): было 0.075 / 0.07 / 0.012
    public static readonly Color AmbientOn = new Color(0.1f, 0.095f, 0.11f);
    public static readonly Color AmbientNeon = new Color(0.095f, 0.065f, 0.12f);
    public static readonly Color AmbientOff = new Color(0.02f, 0.02f, 0.026f);

    public Light[] lamps;
    public Role[] roles;
    public float[] baseIntensity;
    public Renderer[] glowing;           // светящиеся материалы карты (гаснут вместе со светом)
    public int[] glowingMaterial;        // какой материал у рендерера
    public Color[] glowingColor;         // его цвет свечения
    public GameObject neonRoot;          // неоновые полосы
    public Generator generator;

    public static bool NeonEnabled
    {
        get => PlayerPrefs.GetInt(NeonKey, 0) == 1;
        set { PlayerPrefs.SetInt(NeonKey, value ? 1 : 0); PlayerPrefs.Save(); }
    }

    bool wasOn = true, neon;
    float sinceChange = 99f;
    Color ambient = Color.clear;
    float nextIdle;
    float[] flickerUntil, flickerDepth, seed;
    MaterialPropertyBlock block;
    static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");

    void Awake()
    {
        flickerUntil = new float[lamps.Length];
        flickerDepth = new float[lamps.Length];
        seed = new float[lamps.Length];
        for (int i = 0; i < seed.Length; i++) seed[i] = Random.value * 100f;
        block = new MaterialPropertyBlock();
        nextIdle = Random.Range(IdleEveryMin, IdleEveryMax);
        SetNeon(NeonEnabled);
    }

    public void SetNeon(bool on)
    {
        neon = on;
        if (neonRoot != null) neonRoot.SetActive(on);
    }

    void Update()
    {
        var kb = Keyboard.current;
        if (Debug.isDebugBuild && kb != null && kb.f4Key.wasPressedThisFrame && !GameFlow.InputBlocked)
        {
            NeonEnabled = !neon;
            SetNeon(NeonEnabled);
            Debug.Log("[SSDFNC] Свет: " + (neon ? "неон" : "обычный"));
        }

        float dt = Time.deltaTime;
        bool on = generator == null || generator.IsPowered;
        if (on != wasOn)
        {
            wasOn = on;
            sinceChange = 0f;
        }
        sinceChange += dt;

        if (on) PlanFlickers(dt);

        float glow = 0f;
        for (int i = 0; i < lamps.Length; i++)
        {
            if (lamps[i] == null) continue;
            float k = Power(i, on) * LampFlicker(i);
            float scale = roles[i] == Role.Neon ? (neon ? 1f : 0f) : neon ? NeonLampScale : 1f;
            lamps[i].intensity = baseIntensity[i] * k * scale;
            lamps[i].enabled = lamps[i].intensity > 0.001f;
            if (roles[i] == Role.Office) glow = Mathf.Max(glow, k);
        }
        if (!on && sinceChange >= OffFlickerTime) glow = 0f;
        SetAmbient(Color.Lerp(AmbientOff, neon ? AmbientNeon : AmbientOn, glow));
        for (int i = 0; i < glowing.Length; i++)
        {
            if (glowing[i] == null) continue;
            glowing[i].GetPropertyBlock(block, glowingMaterial[i]);
            block.SetColor(EmissionColor, glowingColor[i] * glow);
            glowing[i].SetPropertyBlock(block, glowingMaterial[i]);
        }
    }

    // Фон (RenderSettings, режим Flat): в рантайме меняется через ambientProbe
    void SetAmbient(Color c)
    {
        if (Mathf.Abs(c.r - ambient.r) + Mathf.Abs(c.g - ambient.g) + Mathf.Abs(c.b - ambient.b) < 0.001f) return;
        ambient = c;
        RenderSettings.ambientLight = c;
        var sh = new SphericalHarmonicsL2();
        sh.AddAmbientLight(c);
        RenderSettings.ambientProbe = sh;
    }

    // Питание: есть — 1 (при включении коротко моргает), нет — мигает вразнобой, тускнея, и гаснет
    float Power(int i, bool on)
    {
        float t = sinceChange;
        if (on)
        {
            if (t >= OnFlickerTime) return 1f;
            return Mathf.PerlinNoise(seed[i], t * 22f) > 0.55f - t / OnFlickerTime * 0.6f ? 1f : 0.15f;
        }
        if (t >= OffFlickerTime) return 0f;
        float x = t / OffFlickerTime;
        bool lit = Mathf.PerlinNoise(seed[i], t * 18f) > 0.3f + 0.55f * x;
        return lit ? (1f - x) * (0.6f + 0.4f * Mathf.PerlinNoise(seed[i] + 7f, t * 9f)) : 0.03f;
    }

    // Случайные мигания: редкие — у одной-двух ламп, в генераторной — от того, сколько бензина
    void PlanFlickers(float dt)
    {
        nextIdle -= dt;
        if (nextIdle <= 0f)
        {
            nextIdle = Random.Range(IdleEveryMin, IdleEveryMax);
            int count = Random.Range(1, 3);
            for (int n = 0; n < count; n++)
            {
                int i = Random.Range(0, lamps.Length);
                if (roles[i] == Role.Neon) continue;
                Flicker(i, Random.Range(IdleTimeMin, IdleTimeMax), Random.Range(0f, 0.3f));
            }
        }

        if (generator == null) return;
        float fuel = generator.Fuel / Generator.TankSize;
        if (fuel >= LowFuelStart) return;
        float s = Mathf.InverseLerp(LowFuelStart, LowFuelStrong, fuel); // 0 — только начали, 1 — очень сильно
        float rate = Mathf.Lerp(LowFuelRateWeak, LowFuelRateStrong, s);
        if (Random.value >= rate * dt) return;
        for (int i = 0; i < lamps.Length; i++)
            if (roles[i] == Role.Generator)
                Flicker(i, Mathf.Lerp(0.12f, 0.6f, s * Random.value + 0.2f), Mathf.Lerp(0.5f, 0f, s));
    }

    void Flicker(int i, float time, float depth)
    {
        flickerUntil[i] = Mathf.Max(flickerUntil[i], Time.time + time);
        flickerDepth[i] = depth;
    }

    // Лампа мигает: быстро гаснет до depth и загорается
    float LampFlicker(int i)
    {
        if (Time.time >= flickerUntil[i]) return 1f;
        return Mathf.PerlinNoise(seed[i] + 3f, Time.time * 28f) > 0.45f ? 1f : flickerDepth[i];
    }
}
