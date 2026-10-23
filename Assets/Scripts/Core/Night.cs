using System;
using System.Collections.Generic;
using System.Linq;

// Одна ночь (смена): пять фурри, рост сложности по четвертям смены, мог-батл и итог.
// Без Unity-сцены: в игре её тикает NightDirector, в тестах и симуляции — свой цикл.
public class Night
{
    public readonly DirAI Dir;
    public readonly StrayAI Stray;
    public readonly NovaAI Nova;
    public readonly DaliAI Dali;
    public readonly DoliAI Doli;
    public readonly Furry[] All;

    public float Duration { get; }
    public float Elapsed { get; private set; }
    public bool Frozen { get; private set; }     // идёт мог-батл: все замерли
    public bool Over { get; private set; }       // игрока убили
    public bool Survived => !Over && Elapsed >= Duration;
    public Furry Killer { get; private set; }
    public string KillReason { get; private set; }

    public event Action<Furry, string> PlayerKilled;
    public event Action<NovaAI> MogBattleStarted;
    public event Action<Furry, Room, Room> FurryMoved;
    public event Action<DirAI> DirStarving;
    public event Action<Roamer> GeneratorSabotaged; // Дали или Доли начали портить генератор

    // levels — уровни ИИ в порядке FurryId: Дир, Стрей, Нова, Дали, Доли
    public Night(IOffice office, int[] levels, float duration, int seed)
    {
        var rng = new Random(seed);
        Duration = duration;
        Dir = new DirAI(levels[0], office, rng);
        Stray = new StrayAI(levels[1], office, rng);
        Nova = new NovaAI(levels[2], office, rng);
        Dali = new DaliAI(levels[3], office, rng);
        Doli = new DoliAI(levels[4], office, rng);
        All = new Furry[] { Dir, Stray, Nova, Dali, Doli };

        foreach (var f in All)
        {
            f.Moved += (who, from, to) => FurryMoved?.Invoke(who, from, to);
            f.Attacked += Kill;
        }
        Nova.ReachedDanceFloor += OnNovaArrived;
        Dali.rival = Doli; // к генератору вдвоём не ходят
        Doli.rival = Dali;
        Dali.SabotageStarted += r => GeneratorSabotaged?.Invoke(r);
        Doli.SabotageStarted += r => GeneratorSabotaged?.Invoke(r);
        Dir.StartedStarving += d => DirStarving?.Invoke(d);
    }

    // Какая сейчас четверть смены: 0–3
    public int Quarter => Math.Min(3, (int)(Elapsed / (Duration / 4f)));

    public void Tick(float dt)
    {
        if (Over) return;
        Elapsed += dt;
        foreach (var f in All)
            if (f.Active) f.SetLevel(f.BaseLevel + Quarter * Difficulty.RampPerQuarter);
        if (Frozen) return;
        foreach (var f in All)
        {
            f.Tick(dt);
            if (Over || Frozen) return;
        }
    }

    public void SoundFired()
    {
        if (Over || Frozen) return;
        foreach (var f in All)
        {
            f.HearSound();
            if (Over) return;
        }
    }

    public void FeedDir() => Dir.Feed();

    // Для проверки (панель F1): Нова сразу на дэнс пол. Если была выключена — включается с уровнем 1
    public void ForceNovaBattle()
    {
        if (Over || Frozen) return;
        if (!Nova.Active) Nova.SetBaseLevel(1);
        Nova.RushToDanceFloor();
    }

    void OnNovaArrived(NovaAI nova)
    {
        Frozen = true;
        foreach (var f in All)
            if (f != nova) f.PushBack();
        MogBattleStarted?.Invoke(nova);
    }

    public void FinishMogBattle(bool playerWon)
    {
        Frozen = false;
        Nova.BattleOver(playerWon);
        if (!playerWon) Kill(Nova, "победила в мог-батле");
    }

    void Kill(Furry who, string how)
    {
        if (Over) return;
        Over = true;
        Killer = who;
        KillReason = how;
        PlayerKilled?.Invoke(who, how);
    }

    public IEnumerable<Furry> In(Room room) => All.Where(f => f.Active && f.Room == room);

    // Что видно на камере в этой комнате
    public string Describe(Room room)
    {
        var lines = new List<string>();
        foreach (var f in In(room))
        {
            if (f is DirAI d)
                lines.Add(d.Starving
                    ? $"Дир — ГОЛОДЕН! {Math.Max(0f, Balance.DirGraceTime - d.StarvingFor):0.0} с"
                    : $"Дир — корм {d.Food * 100f:0}%");
            else
                lines.Add(f.Name);
        }
        return lines.Count == 0 ? "пусто" : string.Join("\n", lines);
    }
}
