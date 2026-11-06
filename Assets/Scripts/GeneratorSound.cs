using System.Collections;
using UnityEngine;

// Звук генератора (звуки — GameSounds, меняются в паузе → «Звуки»):
//   завёлся — «запуск», сразу за ним без стыка — «работа» по кругу (хвост запуска переходит в конец петли);
//   встал или сломался — работа затихает за StopFade, поверх — «глушение».
// В начале ночи генератор уже работает — сразу петля, без запуска. Звук не объёмный, как и остальные подсказки.
[RequireComponent(typeof(Generator))]
public class GeneratorSound : MonoBehaviour
{
    const float StopFade = 0.3f; // с, работа → глушение

    Generator generator;
    SoundBinding loop;
    AudioSource once;

    void Awake()
    {
        generator = GetComponent<Generator>();
        loop = GameSounds.Bind(gameObject.AddComponent<AudioSource>(), SoundSlot.GeneratorLoop);
        once = gameObject.AddComponent<AudioSource>();
        once.playOnAwake = false;
        generator.PowerChanged += OnPower;
    }

    void Start() => loop.SetPlaying(generator.IsPowered);

    void OnDestroy() => generator.PowerChanged -= OnPower;

    void OnPower(bool on)
    {
        StopAllCoroutines(); // недоигранный запуск или затухание
        loop.Source.volume = GameSounds.VolumeOf(SoundSlot.GeneratorLoop);
        if (on) Starting();
        else
        {
            once.Stop();
            GameSounds.Play(SoundSlot.GeneratorStop, once);
            StartCoroutine(FadeOut());
        }
    }

    // Запуск, а работа — ровно когда он кончится (по часам звука, без щели)
    void Starting()
    {
        loop.SetPlaying(false);
        GameSounds.Clip(SoundSlot.GeneratorStart, clip =>
        {
            if (this == null || !generator.IsPowered) return;
            if (clip == null)
            {
                loop.SetPlaying(true);
                return;
            }
            double at = AudioSettings.dspTime + 0.05;
            once.Stop();
            once.clip = clip;
            once.volume = GameSounds.VolumeOf(SoundSlot.GeneratorStart);
            once.PlayScheduled(at);
            loop.SetPlaying(false);
            if (loop.Source.clip != null) loop.Source.PlayScheduled(at + (double)clip.samples / clip.frequency);
            StartCoroutine(KeepLoop(clip.length + 0.2f));
        });
    }

    // Если петля догрузилась позже запуска — включить её, когда запуск доиграл
    IEnumerator KeepLoop(float after)
    {
        yield return new WaitForSeconds(after);
        if (generator.IsPowered) loop.SetPlaying(true);
    }

    IEnumerator FadeOut()
    {
        var s = loop.Source;
        float from = s.volume;
        for (float t = 0f; t < StopFade; t += Time.unscaledDeltaTime)
        {
            s.volume = from * (1f - t / StopFade);
            yield return null;
        }
        loop.SetPlaying(false);
        s.Stop(); // и петлю, поставленную в очередь за запуском
        s.volume = from;
    }
}
