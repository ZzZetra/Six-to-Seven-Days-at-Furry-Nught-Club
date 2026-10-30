using System.Collections;
using UnityEngine;

// Модель генератора (Assets/Models/Generator, из Blender): анимации по состоянию Generator.
//   Клик по ручке (GeneratorHandle) — клип Start: рывки стартера. Через Balance.GeneratorStartTime
//     (кадр 64 в Blender) генератор пробует завестись: завёлся — клип доигрывает, генератор начинает
//     трястись; нет бензина или сломан — клип обрывается, генератор стоит.
//   Работает — Idle (дрожь) по кругу. Заглох или сломан — стоит ровно.
//   Держат «Залить» и игрок рядом — крышка бака открыта (CapOpen), отпустили — закрывается (CapClose).
// Клипы Legacy, на разных слоях: дрожь, стартер и крышка не мешают друг другу (лишние каналы из клипов
// убирает GeneratorModelImport при импорте).
public class GeneratorModel : MonoBehaviour
{
    public const string StartClip = "Start", IdleClip = "Idle", CapOpenClip = "CapOpen", CapCloseClip = "CapClose";

    public Generator generator;
    public GeneratorPanel panel;
    public Animation anim;

    public bool Pulling { get; private set; }

    bool capOpen;

    void Awake()
    {
        anim.playAutomatically = false;
        Setup(IdleClip, 0, WrapMode.Loop);
        Setup(StartClip, 1, WrapMode.Once);
        Setup(CapOpenClip, 2, WrapMode.ClampForever);
        Setup(CapCloseClip, 2, WrapMode.ClampForever);
        Rest();
    }

    void Setup(string clip, int layer, WrapMode wrap)
    {
        anim[clip].layer = layer;
        anim[clip].wrapMode = wrap;
    }

    void Update()
    {
        bool running = generator.Current == Generator.State.Running;
        if (!Pulling)
        {
            if (running && !anim.IsPlaying(IdleClip)) anim.Play(IdleClip, PlayMode.StopSameLayer);
            else if (!running && anim.IsPlaying(IdleClip))
            {
                anim.Stop(IdleClip);
                Rest();
            }
        }

        bool open = panel != null && panel.Pouring;
        if (open != capOpen)
        {
            capOpen = open;
            PlayCap(open ? CapOpenClip : CapCloseClip, open ? CapCloseClip : CapOpenClip);
        }
    }

    // Клик по ручке. Работающий генератор не дёргают
    public void Pull()
    {
        if (Pulling || generator.Current == Generator.State.Running) return;
        StartCoroutine(PullRoutine());
    }

    IEnumerator PullRoutine()
    {
        Pulling = true;
        anim.Stop(IdleClip);
        var start = anim[StartClip];
        anim.Play(StartClip, PlayMode.StopSameLayer);
        while (anim.IsPlaying(StartClip) && start.time < Balance.GeneratorStartTime) yield return null;

        if (generator.TryStart())
        {
            while (anim.IsPlaying(StartClip)) yield return null; // завёлся: доигрываем, генератор трясётся
        }
        else
        {
            anim.Stop(StartClip); // не завёлся: рывки закончились, генератор стоит
            Rest();
        }
        Pulling = false;
    }

    // Крышка: если другая ещё не доиграла, начинаем с того же положения
    void PlayCap(string play, string other)
    {
        float done = anim.IsPlaying(other) ? Mathf.Clamp01(anim[other].normalizedTime) : 1f;
        anim.Play(play, PlayMode.StopSameLayer);
        anim[play].normalizedTime = 1f - done;
    }

    // Поза покоя: первый кадр Start — генератор ровно, ручка у корпуса (крышку не трогает)
    void Rest()
    {
        var start = anim[StartClip];
        start.enabled = true;
        start.weight = 1f;
        start.time = 0f;
        anim.Sample();
        start.enabled = false;
    }
}
