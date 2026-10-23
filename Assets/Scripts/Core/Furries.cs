using System;

// Стрей: из комнаты отдыха (4) через зал (3) либо к дэнс полу (через 5), либо к двери (через 2).
// У дэнс пола боится горящего пола, у двери — закрытой двери. Не отпугнули — нападает.
public class StrayAI : Roamer
{
    static readonly Room[] ToDanceFloor = { Room.RestRoom, Room.Hall, Room.Corridor5, Room.DanceFloor };
    static readonly Room[] ToDoor = { Room.RestRoom, Room.Hall, Room.Corridor2, Room.Door };

    internal bool? forceDoorRoute; // для тестов

    public override string Name => "Стрей";
    protected override float Interval => Balance.StrayInterval;

    public StrayAI(int level, IOffice office, Random rng) : base(FurryId.Stray, level, ToDoor, office, rng) { }

    bool Countered => Room == Room.DanceFloor ? office.DanceFloorOn : office.DoorClosed;

    protected override void Opportunity()
    {
        if (AtEnd)
        {
            if (Room == Room.DanceFloor) ThresholdOpportunity(Countered, Balance.DanceFloorGrace, "выскочил с дэнс пола");
            else ThresholdOpportunity(Countered, Balance.DoorGrace, "ворвался в дверь");
            return;
        }
        if (!Roll()) return;
        if (AtHome) SetRoute((forceDoorRoute ?? rng.Next(2) == 0) ? ToDoor : ToDanceFloor);
        Advance();
    }

    protected override void Update(float dt)
    {
        if (AtEnd) ThresholdUpdate(dt, Countered, Room == Room.DanceFloor ? Balance.DanceScareTime : Balance.DoorLeaveTime);
    }
}

// Нова: из комнаты отдыха (4) через 3 и 5 на дэнс пол. Там сама включает пол, отталкивает всех
// на комнату назад и начинает мог-батл. Проиграл — смерть, выиграл — она уходит и становится опытнее.
public class NovaAI : Roamer
{
    static readonly Room[] Route = { Room.RestRoom, Room.Hall, Room.Corridor5, Room.DanceFloor };

    public int Wins { get; private set; } // сколько раз игрок её уже победил
    public bool InBattle { get; private set; }
    public float RestLeft { get; private set; } // после проигранного батла сидит дома
    public event Action<NovaAI> ReachedDanceFloor;

    public override string Name => "Нова";
    protected override float Interval => Balance.NovaInterval;

    public NovaAI(int level, IOffice office, Random rng) : base(FurryId.Nova, level, Route, office, rng) { }

    protected override void Opportunity()
    {
        if (InBattle || AtEnd || RestLeft > 0f || !Roll()) return;
        Advance();
    }

    protected override void Update(float dt)
    {
        if (RestLeft > 0f) RestLeft = Math.Max(0f, RestLeft - dt);
    }

    protected override void OnArrived(Room room)
    {
        InBattle = true;
        ReachedDanceFloor?.Invoke(this);
    }

    // Для проверки (панель F1): сразу на дэнс пол — и мог-батл
    public void RushToDanceFloor()
    {
        RestLeft = 0f;
        if (InBattle) return;
        while (!AtEnd) Advance();
    }

    public void BattleOver(bool playerWon)
    {
        InBattle = false;
        if (!playerWon) return;
        Wins++;
        RestLeft = Balance.NovaRestAfterBattle;
        GoHome();
    }

    public override void PushBack() { } // толкает она, её не толкают
}

// Дали: из серверной (6) через 8, 5 и проход Г к генератору. Кнопка звука, пока она в проходе Г, —
// и она убегает домой. У генератора глушит или сливает бензин; начатое звук не остановит.
// К генератору вдвоём с Доли не ходят: пока Доли идёт к генератору или ломает его, Дали ждёт в проходе 8.
public class DaliAI : Roamer
{
    static readonly Room[] Route = { Room.Server, Room.Vent, Room.Corridor5, Room.PassageG, Room.Generator };

    public override string Name => "Дали";
    protected override float Interval => Room == Room.PassageG ? Balance.PassageInterval : Balance.DaliInterval;

    internal DoliAI rival; // задаёт Night

    public DaliAI(int level, IOffice office, Random rng) : base(FurryId.Dali, level, Route, office, rng) { }

    public override bool HeadingToGenerator => step > 0; // вышла из серверной — значит, к генератору

    protected override void Opportunity()
    {
        if (Sabotaging || AtEnd) return;
        if (Room == Room.Vent && rival != null && rival.HeadingToGenerator) return; // ждёт в 8
        if (!Roll()) return;
        Advance();
    }

    protected override void OnArrived(Room room) => StartSabotage();

    protected override Sabotage PickSabotage() =>
        rng.NextDouble() < Balance.DaliStallChance ? Sabotage.Stall : Sabotage.Drain;

    protected override void Update(float dt)
    {
        if (Sabotaging) SabotageUpdate(dt);
    }

    public override void HearSound()
    {
        if (Room == Room.PassageG) GoHome();
    }
}

// Доли: из концертного зала (3) либо к двери (через 2), либо к генератору (через 5 и Г).
// У двери — как Стрей, но кнопку звука нажимать нельзя: нападёт сразу
// (если Balance.DoorMufflesSound — только при открытой двери).
// У генератора ломает его (BreakPerSecond), пока не спугнут кнопкой звука, но не дольше BreakTime.
// В проходе Г звук тоже прогоняет его домой.
// Если Дали уже идёт к генератору, Доли выбирает дверь.
public class DoliAI : Roamer
{
    static readonly Room[] ToDoor = { Room.Hall, Room.Corridor2, Room.Door };
    static readonly Room[] ToGenerator = { Room.Hall, Room.Corridor5, Room.PassageG, Room.Generator };

    internal bool? forceDoorRoute; // для тестов

    public override string Name => "Доли";
    protected override float Interval => Room == Room.PassageG ? Balance.PassageInterval : Balance.DoliInterval;

    internal DaliAI rival; // задаёт Night

    public DoliAI(int level, IOffice office, Random rng) : base(FurryId.Doli, level, ToDoor, office, rng) { }

    public override bool HeadingToGenerator => route == ToGenerator && step > 0;

    protected override void Opportunity()
    {
        if (Sabotaging) return;
        if (Room == Room.Door)
        {
            ThresholdOpportunity(office.DoorClosed, Balance.DoorGrace, "ворвался в дверь");
            return;
        }
        if (AtEnd || !Roll()) return;
        if (AtHome)
        {
            bool daliGoing = rival != null && rival.HeadingToGenerator;
            SetRoute(daliGoing || (forceDoorRoute ?? rng.Next(2) == 0) ? ToDoor : ToGenerator);
        }
        Advance();
    }

    protected override void OnArrived(Room room)
    {
        if (room == Room.Generator) StartSabotage();
    }

    protected override Sabotage PickSabotage() => Sabotage.Damage;

    protected override void Update(float dt)
    {
        if (Room == Room.Door) ThresholdUpdate(dt, office.DoorClosed, Balance.DoorLeaveTime);
        else if (Sabotaging) SabotageUpdate(dt);
    }

    public override void HearSound()
    {
        if (Room == Room.Door)
        {
            if (!(Balance.DoorMufflesSound && office.DoorClosed)) Attack("услышал звук у двери и напал");
        }
        else if (Room == Room.PassageG || Breaking) GoHome();
    }
}

// Дир: сидит в кормушке (7) и ест корм. Корм кончился — сигнал, через DirGraceTime нападает.
// Кормят через камеру 7 на мониторе. Уровень ИИ задаёт, как быстро он ест.
public class DirAI : Furry
{
    public float Food { get; private set; } = 1f; // 1 — полная кормушка
    public bool Starving { get; private set; }
    public float StarvingFor { get; private set; }
    public event Action<DirAI> StartedStarving;

    public override string Name => "Дир";
    protected override float Interval => 1f; // кубик не бросает

    public DirAI(int level, IOffice office, Random rng) : base(FurryId.Dir, level, Room.Feeding, office, rng) { }

    // На сколько секунд хватает полной кормушки
    public float FoodTime => Level <= 0
        ? float.PositiveInfinity
        : Balance.DirFoodTimeSlow + (Balance.DirFoodTimeFast - Balance.DirFoodTimeSlow) * (Level - 1) / 19f;

    protected override void Opportunity() { }

    protected override void Update(float dt)
    {
        if (Level <= 0) return;
        Food = Math.Max(0f, Food - dt / FoodTime);
        if (Food > 0f) return;
        if (!Starving)
        {
            Starving = true;
            StarvingFor = 0f;
            StartedStarving?.Invoke(this);
        }
        StarvingFor += dt;
        if (StarvingFor >= Balance.DirGraceTime) Attack("остался без корма");
    }

    public void Feed()
    {
        Food = 1f;
        Starving = false;
        StarvingFor = 0f;
    }
}
