using System;

public enum FurryId { Dir, Stray, Nova, Dali, Doli }

// Мозг фурри без Unity-сцены: его можно гонять в тестах и в симуляции баланса.
// Как во FNAF: каждые Interval секунд «возможность хода» — бросок кубика 1–20,
// ход, если выпало не больше уровня ИИ (Level). Level 0 — фурри стоит на месте.
public abstract class Furry
{
    public FurryId Id { get; }
    public abstract string Name { get; }
    public int BaseLevel { get; private set; }  // уровень на начало смены (из Difficulty или панели F1)
    public int Level { get; private set; }      // сейчас: растёт за смену
    public Room Room { get; private set; }
    public bool Active => BaseLevel > 0;
    protected abstract float Interval { get; }

    protected readonly Random rng;
    protected readonly IOffice office;
    float timer;

    public event Action<Furry, Room, Room> Moved;  // кто, откуда, куда
    public event Action<Furry, string> Attacked;   // кто и как напал

    protected Furry(FurryId id, int level, Room start, IOffice office, Random rng)
    {
        Id = id;
        BaseLevel = level;
        Level = level;
        Room = start;
        this.office = office;
        this.rng = rng;
    }

    public void SetLevel(int level) => Level = Math.Max(0, Math.Min(20, level));

    // Поменять уровень посреди ночи (панель F1). 0 — фурри выключен; рост за смену добавится сверху
    public void SetBaseLevel(int level)
    {
        BaseLevel = Math.Max(0, Math.Min(20, level));
        SetLevel(BaseLevel);
    }

    public void Tick(float dt)
    {
        timer += dt;
        while (timer >= Interval)
        {
            timer -= Interval;
            Opportunity();
        }
        Update(dt);
    }

    protected bool Roll() => Level > 0 && rng.Next(1, 21) <= Level;

    protected abstract void Opportunity();       // возможность хода
    protected virtual void Update(float dt) { }  // каждый кадр: таймеры у порога, порча генератора
    public virtual void HearSound() { }          // игрок нажал кнопку звука
    public virtual void PushBack() { }           // Нова отталкивает всех на комнату назад

    protected void MoveTo(Room room)
    {
        if (room == Room) return;
        var from = Room;
        Room = room;
        Moved?.Invoke(this, from, room);
    }

    protected void Attack(string how) => Attacked?.Invoke(this, how);
}

// Фурри, который ходит к офису по маршрутам. Маршрут — список комнат, первая — дом.
// В конце маршрута либо порог (дверь, дэнс пол): ждёт и нападает, если не отпугнули;
// либо генератор: портит его и уходит домой.
public abstract class Roamer : Furry
{
    protected enum Sabotage { None, Stall, Damage, Drain }

    protected Room[] route;
    protected int step;
    float atThreshold;
    float counteredFor;
    Sabotage sabotage;
    float sabotageTime;

    protected Roamer(FurryId id, int level, Room[] firstRoute, IOffice office, Random rng)
        : base(id, level, firstRoute[0], office, rng)
    {
        route = firstRoute;
    }

    protected bool AtHome => step == 0;
    public virtual bool HeadingToGenerator => false; // идёт к генератору или уже у него
    protected bool AtEnd => step == route.Length - 1;
    public bool Sabotaging => sabotage != Sabotage.None;
    public bool Stalling => sabotage == Sabotage.Stall;
    public bool Draining => sabotage == Sabotage.Drain;
    public bool Breaking => sabotage == Sabotage.Damage;
    public event Action<Roamer> SabotageStarted;
    public float AtThresholdFor => atThreshold;

    protected void SetRoute(Room[] newRoute)
    {
        route = newRoute;
        step = 0;
    }

    protected void Advance()
    {
        if (AtEnd) return;
        step++;
        MoveTo(route[step]);
        if (!AtEnd) return;
        atThreshold = 0f;
        counteredFor = 0f;
        OnArrived(Room);
    }

    protected virtual void OnArrived(Room room) { }

    protected void GoHome()
    {
        step = 0;
        sabotage = Sabotage.None;
        MoveTo(route[0]);
    }

    public override void PushBack()
    {
        if (step == 0) return;
        sabotage = Sabotage.None;
        step--;
        MoveTo(route[step]);
    }

    // У порога: countered — дверь закрыта или дэнс пол горит. Продержали leaveAfter — уходит домой
    protected void ThresholdUpdate(float dt, bool countered, float leaveAfter)
    {
        atThreshold += dt;
        if (!countered) return;
        counteredFor += dt;
        if (counteredFor >= leaveAfter) GoHome();
    }

    // Нападает не раньше grace секунд у порога и только если его сейчас не держат
    protected void ThresholdOpportunity(bool countered, float grace, string how)
    {
        if (atThreshold >= grace && !countered && Roll()) Attack(how);
    }

    // Что делает у генератора: Дали — глушит или сливает, Доли — ломает
    protected virtual Sabotage PickSabotage() => Sabotage.None;

    protected void StartSabotage()
    {
        sabotage = PickSabotage();
        sabotageTime = 0f;
        SabotageStarted?.Invoke(this);
    }

    // Глушение и слив звук уже не остановит; поломку останавливает (см. DoliAI.HearSound)
    protected void SabotageUpdate(float dt)
    {
        sabotageTime += dt;
        switch (sabotage)
        {
            case Sabotage.Stall:
                if (sabotageTime < Balance.StallTime) return;
                office.StallGenerator();
                break;
            case Sabotage.Damage:
                office.DamageGenerator(Balance.BreakPerSecond * dt);
                if (sabotageTime < Balance.BreakTime) return;
                break;
            case Sabotage.Drain:
                office.DrainFuel(Balance.DrainPerSecond * dt);
                if (sabotageTime < Balance.DrainTime) return;
                break;
            default:
                return;
        }
        GoHome();
    }

    public string SabotageName => sabotage switch
    {
        Sabotage.Stall => "глушит",
        Sabotage.Damage => "ломает",
        Sabotage.Drain => "сливает бензин",
        _ => "",
    };
}
