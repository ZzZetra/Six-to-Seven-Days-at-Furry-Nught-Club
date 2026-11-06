using System;
using System.Collections.Generic;
using System.Linq;

// Одна ночь (смена): пять фурри, рост сложности по четвертям смены, мог-батл, сбои монитора и итог.
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
    public event Action MonitorRebootStarted;        // монитор ушёл в перезагрузку

    // Шанс сбоя за один бросок (раз в секунду), чтобы за минуту выходило MonitorRebootChancePerMinute
    readonly double monitorRebootChancePerRoll;
    public float MonitorRebootChancePerMinute { get; }

    readonly IOffice office;
    readonly Random monitorRng; // свой поток: сбои не сдвигают броски фурри
    float monitorClock;

    public float MonitorRebootLeft { get; private set; } // с до конца перезагрузки, 0 — работает
    public bool MonitorRebooting => MonitorRebootLeft > 0f;

    // Может ли монитор сейчас сбоить: он работает и у Дира корма не меньше MonitorRebootMinDirFood
    public bool MonitorCanReboot =>
        office.MonitorPowered && !Dir.Starving && Dir.Food >= Balance.MonitorRebootMinDirFood;

    // levels — уровни ИИ в порядке FurryId: Дир, Стрей, Нова, Дали, Доли.
    // monitorRebootPerMinute — шанс сбоя монитора за минуту (у ночей прохождения свой, Nights.cs); < 0 — из Balance
    public Night(IOffice office, int[] levels, float duration, int seed, float monitorRebootPerMinute = -1f)
    {
        MonitorRebootChancePerMinute = monitorRebootPerMinute >= 0f ? monitorRebootPerMinute : Balance.MonitorRebootChancePerMinute;
        monitorRebootChancePerRoll = 1.0 - Math.Pow(1.0 - MonitorRebootChancePerMinute, 1.0 / 60.0);
        var rng = new Random(seed);
        monitorRng = new Random(unchecked(seed * 31 + 17));
        this.office = office;
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
        UpdateMonitor(dt);
        foreach (var f in All)
        {
            f.Tick(dt);
            if (Over || Frozen) return;
        }
    }

    // Сбой монитора: идёт перезагрузка — досчитать; иначе раз в секунду бросок, если монитор может сбоить
    void UpdateMonitor(float dt)
    {
        if (MonitorRebootLeft > 0f)
        {
            MonitorRebootLeft = Math.Max(0f, MonitorRebootLeft - dt);
            return;
        }
        monitorClock += dt;
        while (monitorClock >= 1f)
        {
            monitorClock -= 1f;
            if (MonitorCanReboot && monitorRng.NextDouble() < monitorRebootChancePerRoll)
            {
                StartMonitorReboot();
                return;
            }
        }
    }

    // Для проверки (панель F1): сбой сразу, без броска и без условия про Дира
    public void ForceMonitorReboot()
    {
        if (Over || Frozen || MonitorRebooting) return;
        StartMonitorReboot();
    }

    void StartMonitorReboot()
    {
        MonitorRebootLeft = Balance.MonitorRebootTime;
        monitorClock = 0f;
        MonitorRebootStarted?.Invoke();
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
