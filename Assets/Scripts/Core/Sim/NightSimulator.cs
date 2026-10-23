using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

// Симуляция ночей без Unity, чтобы прикинуть баланс: офис по тем же правилам и числам (Balance),
// что и в игре, плюс бот-игрок. Отчёт — меню SSDFNC → Balance Report (пишет docs/BALANCE_REPORT.md).

// Офис для симуляции: дверь (20 с максимум, блок 10 с), дэнс пол, генератор (бензин, HP, глушение)
public class SimOffice : IOffice
{
    public bool DoorClosed { get; private set; }
    public float DoorClosedFor { get; private set; }
    public float DoorLockedFor { get; private set; }
    public bool DanceHeld;
    public bool DanceFloorOn => DanceHeld && Powered;

    public float Fuel = Balance.TankSize;
    public float Reserve = Balance.StartReserve;
    public float Health = Balance.GeneratorMaxHealth;
    public bool Stalled, Broken, Paused;
    public bool Powered => !Stalled && !Broken;

    public int Stalls, DoorAutoOpens;

    public void Tick(float dt, float shiftDuration)
    {
        if (Powered && !Paused) DrainFuel(Balance.FuelPerShift / shiftDuration * dt);
        if (DoorClosed)
        {
            DoorClosedFor += dt;
            if (!Powered) OpenDoor();
            else if (DoorClosedFor >= Balance.DoorMaxClosedTime)
            {
                OpenDoor();
                DoorLockedFor = Balance.DoorLockTime;
                DoorAutoOpens++;
            }
        }
        else if (DoorLockedFor > 0f) DoorLockedFor = Math.Max(0f, DoorLockedFor - dt);
    }

    public bool CanCloseDoor => Powered && !DoorClosed && DoorLockedFor <= 0f;

    public void CloseDoor()
    {
        if (!CanCloseDoor) return;
        DoorClosed = true;
        DoorClosedFor = 0f;
    }

    public void OpenDoor()
    {
        DoorClosed = false;
        DoorClosedFor = 0f;
    }

    public void StallGenerator()
    {
        if (!Powered) return;
        Stalled = true;
        Stalls++;
    }

    public void DamageGenerator(float hp)
    {
        if (Broken) return;
        Health -= hp;
        if (Health > 0f) return;
        Health = 0f;
        Broken = true;
    }

    public void DrainFuel(float amount)
    {
        Fuel = Math.Max(0f, Fuel - amount);
        if (Fuel <= 0f) StallGenerator();
    }

    public void Pour(float dt)
    {
        float amount = Math.Min(Math.Min(Balance.PourRate * dt, Reserve), Balance.TankSize - Fuel);
        Reserve -= amount;
        Fuel += amount;
    }

    public void Restart()
    {
        if (Stalled && !Broken && Fuel > 0f) Stalled = false;
    }
}

// Как играет бот: реакция (с), шанс прозевать событие (тогда реагирует втрое дольше),
// шанс попасть в зону в мог-батле
public class BotProfile
{
    public readonly string Name;
    public readonly float Reaction;
    public readonly float Miss;
    public readonly float MogHit;

    public BotProfile(string name, float reaction, float miss, float mogHit)
    {
        Name = name;
        Reaction = reaction;
        Miss = miss;
        MogHit = mogHit;
    }

    public static readonly BotProfile Skilled = new BotProfile("Опытный", 0.7f, 0.05f, 0.85f);
    public static readonly BotProfile Average = new BotProfile("Средний", 1.3f, 0.15f, 0.7f);
    public static readonly BotProfile Novice = new BotProfile("Новичок", 2.2f, 0.3f, 0.55f);
    public static readonly BotProfile[] All = { Skilled, Average, Novice };
}

// Бот-игрок. Знает, где кто (как будто следит за камерами), но реагирует с задержкой,
// делает одно дело за раз, поворот и проход к генератору занимают время, как в игре.
// Приоритеты: Стрей на дэнс полу → кто-то у открытой двери → покормить Дира → звук на тех, кто идёт
// к генератору или Доли ломает генератор (если у двери нет Доли) → генератор (завести, долить)
// → открыть дверь, когда угроза ушла.
class SimBot
{
    enum Spot { Left, Forward, Right, Generator }

    const float TurnTime = 0.65f;   // поворот на 90°
    const float WalkTime = 1.2f;    // до генератора или обратно
    const float FeedTime = 0.5f + Balance.DirFeedHoldTime; // выбрать камеру 7 и подержать «Покормить»
    const float PressTime = 0.2f;
    const float RestartTime = 0.5f;
    const float FeedBelow = 0.35f;  // кормить, когда корма меньше
    const float RefuelBelow = 20f;  // идти доливать, когда бензина меньше

    readonly BotProfile profile;
    readonly Night night;
    readonly SimOffice office;
    readonly Random rng;
    readonly Dictionary<string, float> noticeAt = new Dictionary<string, float>();

    Spot spot = Spot.Forward;
    Spot doneSpot;
    float busyUntil;
    Action onDone;
    bool refueling;
    float now;

    public int Feeds, Sounds;

    public SimBot(BotProfile profile, Night night, SimOffice office, Random rng)
    {
        this.profile = profile;
        this.night = night;
        this.office = office;
        this.rng = rng;
    }

    // Заметил ли бот, что условие выполняется: с момента начала прошло его время реакции
    bool Noticed(string key, bool condition)
    {
        if (!condition)
        {
            noticeAt.Remove(key);
            return false;
        }
        if (!noticeAt.TryGetValue(key, out float at))
        {
            float delay = profile.Reaction * (rng.NextDouble() < profile.Miss ? 3f : 1f);
            noticeAt[key] = at = now + delay;
        }
        return now >= at;
    }

    static int Facing(Spot s) => s == Spot.Left ? 0 : s == Spot.Forward ? 1 : 2;

    float TravelTime(Spot from, Spot to)
    {
        if (from == to) return 0f;
        if (to == Spot.Generator) return TurnTime * Facing(from) + WalkTime;
        if (from == Spot.Generator) return WalkTime + TurnTime * Facing(to);
        return TurnTime * Math.Abs(Facing(from) - Facing(to));
    }

    void GoTo(Spot target, float actionTime, Action effect)
    {
        busyUntil = now + TravelTime(spot, target) + actionTime;
        doneSpot = target;
        onDone = effect;
        if (target != Spot.Forward) office.DanceHeld = false;
    }

    public bool PlayMogBattle()
    {
        // Как MogTimeline: запас мога с MogStartLives, попал — плюс до максимума, промах — минус, ноль — смерть
        float hit = profile.MogHit - 0.05f * night.Nova.Wins;
        int rounds = MogChart.Default().checks.Length, lives = Balance.MogStartLives;
        for (int i = 0; i < rounds && lives > 0; i++)
            lives = rng.NextDouble() < hit ? Math.Min(Balance.MogMaxLives, lives + Balance.MogLivesPerHit) : lives - 1;
        return lives > 0;
    }

    public void Tick(float t, float dt)
    {
        now = t;
        if (onDone != null)
        {
            if (now < busyUntil) return;
            spot = doneSpot;
            var effect = onDone;
            onDone = null;
            effect();
        }

        var stray = night.Stray;
        var doli = night.Doli;
        bool strayFront = stray.Active && stray.Room == Room.DanceFloor;
        bool doliAtDoor = doli.Active && doli.Room == Room.Door;
        bool doorThreat = (stray.Active && stray.Room == Room.Door) || doliAtDoor;
        bool saboteur = (night.Dali.Active && night.Dali.Room == Room.PassageG)
                        || (doli.Active && (doli.Room == Room.PassageG || (doli.Room == Room.Generator && doli.Sabotaging)));
        bool dirHungry = night.Dir.Active && (night.Dir.Food < FeedBelow || night.Dir.Starving);
        bool genDown = office.Stalled && !office.Broken && office.Fuel > 0f;
        bool fuelLow = !office.Broken && office.Fuel < RefuelBelow && office.Reserve > 0f;

        if (office.DanceHeld && !strayFront) office.DanceHeld = false;

        // Долив у генератора идёт, пока не полный бак или пока в офисе не стало опасно
        if (refueling)
        {
            office.Pour(dt);
            bool full = office.Fuel >= Balance.TankSize - 0.5f || office.Reserve <= 0f;
            if (!full && !strayFront && !doorThreat && !dirHungry) return;
            refueling = false;
        }

        if (Noticed("front", strayFront))
        {
            if (spot != Spot.Forward || !office.DanceHeld) GoTo(Spot.Forward, 0f, () => office.DanceHeld = true);
            return;
        }
        if (office.DanceHeld) return; // держит пол — не отворачивается

        if (Noticed("door", doorThreat && !office.DoorClosed) && office.CanCloseDoor)
        {
            GoTo(Spot.Right, PressTime, office.CloseDoor);
            return;
        }
        if (Noticed("dir", dirHungry) && office.Powered)
        {
            GoTo(Spot.Forward, FeedTime, () => { night.FeedDir(); Feeds++; });
            return;
        }
        // Звук на тех, кто идёт к генератору. Если Доли у открытой двери, звук его спровоцирует:
        // бот проверяет это и перед решением, и в момент нажатия, но иногда прозёвывает
        bool doliHears = doliAtDoor && !(Balance.DoorMufflesSound && office.DoorClosed);
        if (Noticed("sab", saboteur) && !doliHears)
        {
            GoTo(Spot.Forward, PressTime, () =>
            {
                bool hearsNow = night.Doli.Active && night.Doli.Room == Room.Door
                                && !(Balance.DoorMufflesSound && office.DoorClosed);
                if (hearsNow && rng.NextDouble() >= profile.Miss) return; // заметил Доли и не нажал
                night.SoundFired();
                Sounds++;
            });
            return;
        }
        if (Noticed("gen", genDown || fuelLow))
        {
            GoTo(Spot.Generator, RestartTime, () =>
            {
                office.Restart();
                refueling = office.Fuel < Balance.TankSize - 0.5f && office.Reserve > 0f;
            });
            return;
        }
        if (Noticed("open", office.DoorClosed && !doorThreat))
        {
            GoTo(Spot.Right, PressTime, office.OpenDoor);
            return;
        }
        if (spot == Spot.Generator) GoTo(Spot.Forward, 0f, () => { });
    }
}

public class NightResult
{
    public bool Survived;
    public FurryId? Killer;
    public string Reason;
    public float Time;
    public int Stalls, DoorAutoOpens, Feeds, Sounds, Battles;
    public bool GeneratorBroken;
}

public static class NightSimulator
{
    public const float Step = 0.05f;

    public static NightResult Run(int[] levels, BotProfile bot, float duration, int seed)
    {
        var office = new SimOffice();
        var night = new Night(office, levels, duration, seed);
        var player = new SimBot(bot, night, office, new Random(seed * 7919 + 17));
        int battles = 0;
        float t = 0f;

        while (t < duration && !night.Over)
        {
            if (night.Frozen)
            {
                // Мог-батл: все стоят, бензин не тратится, время идёт
                battles++;
                bool won = player.PlayMogBattle();
                office.Paused = true;
                float battleTime = MogChart.Default().LastCheckEnd + Balance.MogOutroTime;
                for (float b = 0f; b < battleTime && t < duration; b += Step)
                {
                    t += Step;
                    office.Tick(Step, duration);
                    night.Tick(Step);
                }
                office.Paused = false;
                night.FinishMogBattle(won);
                continue;
            }
            t += Step;
            office.Tick(Step, duration);
            night.Tick(Step);
            player.Tick(t, Step);
        }

        return new NightResult
        {
            Survived = !night.Over,
            Killer = night.Killer?.Id,
            Reason = night.KillReason,
            Time = t,
            Stalls = office.Stalls,
            DoorAutoOpens = office.DoorAutoOpens,
            Feeds = player.Feeds,
            Sounds = player.Sounds,
            Battles = battles,
            GeneratorBroken = office.Broken,
        };
    }

    public class Summary
    {
        public int Nights, Survived;
        public readonly Dictionary<FurryId, int> Deaths = new Dictionary<FurryId, int>();
        public float Stalls, AutoOpens, Broken, Battles, Feeds;
        public float SurvivalRate => Nights == 0 ? 0f : (float)Survived / Nights;
        public int DeathsBy(FurryId id) => Deaths.TryGetValue(id, out int n) ? n : 0;
    }

    public static Summary RunMany(int[] levels, BotProfile bot, float duration, int nights, int firstSeed = 1)
    {
        var s = new Summary();
        for (int i = 0; i < nights; i++)
        {
            var r = Run(levels, bot, duration, firstSeed + i);
            s.Nights++;
            if (r.Survived) s.Survived++;
            else if (r.Killer.HasValue) s.Deaths[r.Killer.Value] = s.DeathsBy(r.Killer.Value) + 1;
            s.Stalls += r.Stalls;
            s.AutoOpens += r.DoorAutoOpens;
            s.Broken += r.GeneratorBroken ? 1 : 0;
            s.Battles += r.Battles;
            s.Feeds += r.Feeds;
        }
        if (s.Nights > 0)
        {
            s.Stalls /= s.Nights;
            s.AutoOpens /= s.Nights;
            s.Broken /= s.Nights;
            s.Battles /= s.Nights;
            s.Feeds /= s.Nights;
        }
        return s;
    }

    // Таблица markdown: сложности × боты
    public static string Report(int nights, float duration)
    {
        var presets = new[] { DifficultyPreset.Easy, DifficultyPreset.Normal, DifficultyPreset.Hard };
        var sb = new StringBuilder();
        sb.AppendLine($"Смена {duration:0} с, {nights} ночей на каждую клетку.");
        sb.AppendLine();
        sb.AppendLine("| Сложность | Бот | Выжил | Дир | Стрей | Нова | Дали | Доли | Заглох за ночь | Сломан | Автооткрытий двери | Мог-батлов | Кормлений |");
        sb.AppendLine("|---|---|---|---|---|---|---|---|---|---|---|---|---|");
        foreach (var preset in presets)
        {
            var levels = Difficulty.Levels(preset);
            foreach (var bot in BotProfile.All)
            {
                var s = RunMany(levels, bot, duration, nights);
                string D(FurryId id) => $"{100f * s.DeathsBy(id) / s.Nights:0}%";
                sb.AppendLine($"| {preset} ({string.Join("/", levels)}) | {bot.Name} | **{100f * s.SurvivalRate:0}%** | " +
                              $"{D(FurryId.Dir)} | {D(FurryId.Stray)} | {D(FurryId.Nova)} | {D(FurryId.Dali)} | {D(FurryId.Doli)} | " +
                              $"{s.Stalls:0.0} | {100f * s.Broken:0}% | {s.AutoOpens:0.0} | {s.Battles:0.0} | {s.Feeds:0.0} |");
            }
        }
        return sb.ToString();
    }
}
