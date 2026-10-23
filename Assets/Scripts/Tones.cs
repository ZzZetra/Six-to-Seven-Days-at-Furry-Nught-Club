using UnityEngine;

// Простые звуки, созданные в коде (файлов нет): гудок-меандр с мягкими краями без щелчков.
public static class Tones
{
    public static AudioClip Beep(string name, float pitch, float seconds, float volume = 0.25f)
    {
        const int rate = 44100;
        int count = (int)(rate * seconds);
        var data = new float[count];
        for (int i = 0; i < count; i++)
        {
            float t = (float)i / rate;
            float envelope = Mathf.Min(1f, t * 50f) * Mathf.Min(1f, (seconds - t) * 20f);
            data[i] = volume * envelope * Mathf.Sign(Mathf.Sin(2f * Mathf.PI * pitch * t));
        }
        return Make(name, data, rate);
    }

    // Низкое рычание для зацикливания: пила 95 Гц, громкость пульсирует 2 раза в секунду.
    // 2 с — целое число периодов, поэтому петля без щелчка
    public static AudioClip Growl(string name, float volume = 0.35f)
    {
        const int rate = 44100;
        const float seconds = 2f;
        var data = new float[(int)(rate * seconds)];
        for (int i = 0; i < data.Length; i++)
        {
            float t = (float)i / rate;
            float saw = 2f * Mathf.Repeat(95f * t, 1f) - 1f;
            float pulse = 0.55f + 0.45f * Mathf.Sin(2f * Mathf.PI * 2f * t);
            data[i] = volume * pulse * saw;
        }
        return Make(name, data, rate);
    }

    // Бульканье для зацикливания: всплески приглушённого шума 7 раз в секунду
    public static AudioClip Glug(string name, float volume = 0.5f)
    {
        const int rate = 44100;
        var data = new float[rate]; // 1 с = 7 всплесков
        var rng = new System.Random(7);
        float smooth = 0f;
        for (int i = 0; i < data.Length; i++)
        {
            float t = (float)i / rate;
            float phase = Mathf.Repeat(t * 7f, 1f);
            float envelope = phase < 0.4f ? Mathf.Sin(Mathf.PI * phase / 0.4f) : 0f;
            smooth += 0.08f * ((float)rng.NextDouble() * 2f - 1f - smooth); // фильтр: шум глуше
            data[i] = volume * envelope * smooth * 3f;
        }
        return Make(name, data, rate);
    }

    static AudioClip Make(string name, float[] data, int rate)
    {
        var clip = AudioClip.Create(name, data.Length, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }
}
