using System.Collections;
using UnityEngine;

// Кнопка звукового сигнала на столе: нажал — один гудок (Pulse). Работает и без генератора.
// Отпугивает тех, кто идёт к генератору (когда появятся фурри). Звук создаётся в коде, файла нет.
public class SoundSignal : Deterrent
{
    public const ActivationMode Activation = ActivationMode.Pulse;
    const float BeepTime = 0.5f;  // с
    const float BeepPitch = 660f; // Гц

    static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
    static readonly Color FlashColor = new Color(1f, 0.9f, 0.4f);

    public AudioSource source;
    public Renderer lamp; // шляпка кнопки: вспыхивает на время гудка

    AudioClip beep;
    MaterialPropertyBlock block;

    protected override ActivationMode Mode => Activation;
    protected override bool NeedsPower => false;

    void Awake()
    {
        beep = MakeBeep();
        block = new MaterialPropertyBlock();
    }

    protected override void OnFired()
    {
        Debug.Log("[SSDFNC] Звуковой сигнал");
        if (source != null) source.PlayOneShot(beep);
        StopAllCoroutines();
        StartCoroutine(Flash());
    }

    IEnumerator Flash()
    {
        block.SetColor(BaseColor, FlashColor);
        lamp.SetPropertyBlock(block);
        yield return new WaitForSeconds(BeepTime);
        lamp.SetPropertyBlock(null); // обратно к цвету материала
    }

    // Гудок-меандр с мягким началом и концом, чтобы не щёлкал
    static AudioClip MakeBeep()
    {
        const int rate = 44100;
        int count = (int)(rate * BeepTime);
        var data = new float[count];
        for (int i = 0; i < count; i++)
        {
            float t = (float)i / rate;
            float envelope = Mathf.Min(1f, t * 50f) * Mathf.Min(1f, (BeepTime - t) * 20f);
            data[i] = 0.25f * envelope * Mathf.Sign(Mathf.Sin(2f * Mathf.PI * BeepPitch * t));
        }
        var clip = AudioClip.Create("Beep", count, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }
}
