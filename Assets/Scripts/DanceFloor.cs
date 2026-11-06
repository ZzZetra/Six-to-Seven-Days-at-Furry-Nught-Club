using UnityEngine;

// Дэнс пол в коридоре перед офисом: работает, пока держишь кнопку на столе (Hold), как фонарик во FNAF 2.
// Плитки переливаются цветами, над ними горит свет, играет музыка (звук «Дэнс пол» в паузе → «Звуки»; когда пол
// включила Нова на мог-батл — нет, там своя музыка). Отпугивает стеснительных фурри. Нужен генератор.
public class DanceFloor : Deterrent
{
    public const ActivationMode Activation = ActivationMode.Hold;
    const float ColorSpeed = 0.6f; // оборотов радуги в секунду

    static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
    static readonly Color OffColor = new Color(0.1f, 0.1f, 0.12f);

    public Renderer[] tiles;
    public Light glow;

    MaterialPropertyBlock block;
    SoundBinding music;
    bool forced; // Нова сама включила пол на время мог-батла

    public void SetForced(bool on)
    {
        forced = on;
        glow.enabled = IsActive || forced;
        Paint(IsActive || forced);
    }

    protected override ActivationMode Mode => Activation;

    void Awake()
    {
        block = new MaterialPropertyBlock();
        Paint(false);
        var source = gameObject.AddComponent<AudioSource>();
        source.spatialBlend = 0f;
        music = GameSounds.Bind(source, SoundSlot.DanceFloorMusic);
    }

    void Update()
    {
        if (IsActive || forced) Paint(true);
        music.SetPlaying(IsActive && !forced);
    }

    protected override void OnActiveChanged(bool on)
    {
        Debug.Log(on ? "[SSDFNC] Дэнс пол включён" : "[SSDFNC] Дэнс пол выключен");
        glow.enabled = on || forced;
        Paint(on || forced);
    }

    void Paint(bool on)
    {
        for (int i = 0; i < tiles.Length; i++)
        {
            var color = on ? Color.HSVToRGB(Mathf.Repeat(Time.time * ColorSpeed + i * 0.17f, 1f), 0.8f, 1f) : OffColor;
            block.SetColor(BaseColor, color);
            tiles[i].SetPropertyBlock(block);
        }
        if (on) glow.color = Color.HSVToRGB(Mathf.Repeat(Time.time * ColorSpeed, 1f), 0.5f, 1f);
    }
}
