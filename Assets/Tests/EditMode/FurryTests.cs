using System;
using NUnit.Framework;

// Тесты правил фурри из SSDFNC.Core: без сцены, с заданным зерном случайности.
// Запуск: Window → General → Test Runner → EditMode → Run All.
public class FurryTests
{
    class FakeOffice : IOffice
    {
        public bool DoorClosed { get; set; }
        public bool DanceFloorOn { get; set; }
        public int Stalls;
        public float Damage, Drained;
        public void StallGenerator() => Stalls++;
        public void DamageGenerator(float hp) => Damage += hp;
        public void DrainFuel(float amount) => Drained += amount;
    }

    const float Dt = 0.05f;

    static Random Rng(int seed = 1) => new Random(seed);

    static void Run(Furry f, float seconds)
    {
        for (float t = 0f; t < seconds; t += Dt) f.Tick(Dt);
    }

    // Гоняет, пока условие не выполнится; возвращает прошедшее время или -1
    static float RunUntil(Furry f, Func<bool> done, float limit)
    {
        for (float t = 0f; t < limit; t += Dt)
        {
            if (done()) return t;
            f.Tick(Dt);
        }
        return done() ? limit : -1f;
    }

    // ── Бросок кубика ──

    [Test]
    public void Level0_NeverMoves()
    {
        var stray = new StrayAI(0, new FakeOffice(), Rng());
        Run(stray, 300f);
        Assert.AreEqual(Room.RestRoom, stray.Room);
    }

    [Test]
    public void Level20_ReachesTheOfficeInThreeOpportunities()
    {
        var stray = new StrayAI(20, new FakeOffice(), Rng());
        Run(stray, 3 * Balance.StrayInterval + 0.1f);
        Assert.That(stray.Room == Room.Door || stray.Room == Room.DanceFloor, $"Стрей в {stray.Room}");
    }

    // ── Дверь ──

    StrayAI StrayAtDoor(FakeOffice office, out bool attacked)
    {
        var stray = new StrayAI(20, office, Rng()) { forceDoorRoute = true };
        bool hit = false;
        stray.Attacked += (f, how) => hit = true;
        Assert.That(RunUntil(stray, () => stray.Room == Room.Door, 30f) >= 0f, "Стрей не дошёл до двери");
        attacked = hit;
        return stray;
    }

    [Test]
    public void ClosedDoor_StrayNeverAttacks_AndLeaves()
    {
        var office = new FakeOffice { DoorClosed = true };
        bool attacked = false;
        var stray = StrayAtDoor(office, out _);
        stray.Attacked += (f, how) => attacked = true;
        float left = RunUntil(stray, () => stray.Room != Room.Door, 30f);
        Assert.IsFalse(attacked);
        Assert.That(left, Is.EqualTo(Balance.DoorLeaveTime).Within(0.2f));
        Assert.AreEqual(Room.RestRoom, stray.Room);
    }

    [Test]
    public void OpenDoor_StrayAttacks_NotBeforeGrace()
    {
        var office = new FakeOffice();
        bool attacked = false;
        var stray = StrayAtDoor(office, out _);
        stray.Attacked += (f, how) => attacked = true;
        float t = RunUntil(stray, () => attacked, 30f);
        Assert.That(t, Is.GreaterThanOrEqualTo(Balance.DoorGrace - Dt));
        Assert.That(t, Is.LessThanOrEqualTo(Balance.DoorGrace + Balance.StrayInterval + Dt));
    }

    // ── Дэнс пол ──

    [Test]
    public void DanceFloorOn_ScaresStrayAway()
    {
        var office = new FakeOffice();
        var stray = new StrayAI(20, office, Rng()) { forceDoorRoute = false };
        Assert.That(RunUntil(stray, () => stray.Room == Room.DanceFloor, 30f) >= 0f);
        bool attacked = false;
        stray.Attacked += (f, how) => attacked = true;
        office.DanceFloorOn = true;
        float left = RunUntil(stray, () => stray.Room != Room.DanceFloor, 30f);
        Assert.IsFalse(attacked);
        Assert.That(left, Is.EqualTo(Balance.DanceScareTime).Within(0.2f));
    }

    // ── Звук ──

    [Test]
    public void Sound_WithDoliAtOpenDoor_Kills()
    {
        var office = new FakeOffice();
        var doli = new DoliAI(20, office, Rng()) { forceDoorRoute = true };
        Assert.That(RunUntil(doli, () => doli.Room == Room.Door, 30f) >= 0f);
        string how = null;
        doli.Attacked += (f, h) => how = h;
        doli.HearSound();
        Assert.IsNotNull(how);
    }

    [Test]
    public void Sound_WithDoliAtClosedDoor_IsMuffled()
    {
        var office = new FakeOffice();
        var doli = new DoliAI(20, office, Rng()) { forceDoorRoute = true };
        Assert.That(RunUntil(doli, () => doli.Room == Room.Door, 30f) >= 0f);
        office.DoorClosed = true;
        bool attacked = false;
        doli.Attacked += (f, h) => attacked = true;
        doli.HearSound();
        Assert.AreEqual(Balance.DoorMufflesSound, !attacked);
    }

    [Test]
    public void Sound_InPassageG_SendsDaliHome()
    {
        var dali = new DaliAI(20, new FakeOffice(), Rng());
        Assert.That(RunUntil(dali, () => dali.Room == Room.PassageG, 30f) >= 0f);
        dali.HearSound();
        Assert.AreEqual(Room.Server, dali.Room);
    }

    // ── Генератор ──

    [Test]
    public void Dali_AtGenerator_SabotagesEvenWithSound_AndGoesHome()
    {
        var office = new FakeOffice();
        var dali = new DaliAI(20, office, Rng(3));
        Assert.That(RunUntil(dali, () => dali.Room == Room.Generator, 60f) >= 0f);
        dali.HearSound(); // у генератора звук уже не помогает
        Assert.AreEqual(Room.Generator, dali.Room);
        float back = RunUntil(dali, () => dali.Room != Room.Generator, 10f);
        Assert.That(back, Is.GreaterThan(0f));
        Assert.That(office.Stalls > 0 || office.Damage > 0f || office.Drained > 0f, "ничего не испортила");
        Assert.AreEqual(Room.Server, dali.Room);
    }

    [Test]
    public void Dali_OnlyStallsOrDrains_NeverBreaks()
    {
        var office = new FakeOffice();
        for (int seed = 1; seed <= 60; seed++)
        {
            var dali = new DaliAI(20, office, Rng(seed));
            RunUntil(dali, () => dali.Room == Room.Generator, 60f);
            RunUntil(dali, () => dali.Room != Room.Generator, 10f);
        }
        Assert.That(office.Stalls, Is.GreaterThan(0), "ни разу не заглушила");
        Assert.That(office.Drained, Is.GreaterThan(0f), "ни разу не слила");
        Assert.AreEqual(0f, office.Damage, "Дали не ломает");
    }

    DoliAI DoliAtGenerator(FakeOffice office)
    {
        var doli = new DoliAI(20, office, Rng()) { forceDoorRoute = false };
        Assert.That(RunUntil(doli, () => doli.Room == Room.Generator, 60f) >= 0f, "Доли не дошёл до генератора");
        Assert.IsTrue(doli.Breaking);
        return doli;
    }

    [Test]
    public void Doli_AtGenerator_BreaksUntilSound()
    {
        var office = new FakeOffice();
        var doli = DoliAtGenerator(office);
        Run(doli, 3f);
        Assert.That(office.Damage, Is.EqualTo(3f * Balance.BreakPerSecond).Within(Balance.BreakPerSecond * 0.1f));
        doli.HearSound();
        Assert.AreEqual(Room.Hall, doli.Room, "звук прогоняет Доли от генератора");
        float damage = office.Damage;
        Run(doli, 1f);
        Assert.AreEqual(damage, office.Damage, "после звука больше не ломает");
        Assert.AreEqual(0, office.Stalls);
        Assert.AreEqual(0f, office.Drained);
    }

    [Test]
    public void Doli_AtGenerator_WithoutSound_BreaksItCompletely()
    {
        var office = new FakeOffice();
        var doli = DoliAtGenerator(office);
        float left = RunUntil(doli, () => doli.Room != Room.Generator, 30f);
        Assert.That(left, Is.EqualTo(Balance.BreakTime).Within(0.2f));
        Assert.That(office.Damage, Is.GreaterThanOrEqualTo(Balance.GeneratorMaxHealth - Balance.BreakPerSecond * 0.2f));
    }

    // ── Дали и Доли к генератору вдвоём не ходят ──

    [Test]
    public void Doli_ChoosesDoor_WhenDaliIsHeadingToGenerator()
    {
        var office = new FakeOffice();
        var rng = Rng();
        var dali = new DaliAI(20, office, rng);
        var doli = new DoliAI(20, office, rng) { forceDoorRoute = false }; // сам бы пошёл к генератору
        dali.rival = doli;
        doli.rival = dali;
        RunUntil(dali, () => dali.Room == Room.Vent, 30f);
        Assert.IsTrue(dali.HeadingToGenerator);
        RunUntil(doli, () => doli.Room != Room.Hall, 30f);
        Assert.AreEqual(Room.Corridor2, doli.Room, "Дали уже идёт к генератору — Доли идёт к двери");
        Assert.IsFalse(doli.HeadingToGenerator);
    }

    [Test]
    public void Dali_WaitsInVent_WhileDoliIsAtGenerator()
    {
        var office = new FakeOffice();
        var rng = Rng();
        var dali = new DaliAI(20, office, rng);
        var doli = new DoliAI(20, office, rng) { forceDoorRoute = false };
        dali.rival = doli;
        doli.rival = dali;
        // Доли первым уходит к генератору, Дали дома
        RunUntil(doli, () => doli.Room == Room.Generator, 60f);
        Assert.AreEqual(Room.Server, dali.Room);
        // Дали доходит до 8 и ждёт, пока Доли ломает генератор
        for (float t = 0f; t < 30f && doli.Room == Room.Generator; t += Dt)
        {
            dali.Tick(Dt);
            doli.Tick(Dt);
            Assert.That(dali.Room == Room.Server || dali.Room == Room.Vent, $"Дали ушла дальше 8: {dali.Room}");
        }
        Assert.AreEqual(Room.Vent, dali.Room, "Дали ждёт в проходе 8");
        // Доли ушёл — Дали идёт дальше
        Assert.That(RunUntil(dali, () => dali.Room == Room.Corridor5, 30f) >= 0f, "Дали не пошла дальше после ухода Доли");
    }

    [Test]
    public void DaliAndDoli_NeverBothOnTheWayToGenerator()
    {
        for (int seed = 1; seed <= 40; seed++)
        {
            var night = new Night(new FakeOffice { DoorClosed = true }, new[] { 0, 0, 0, 20, 20 }, 600f, seed);
            for (float t = 0f; t < 300f && !night.Over; t += Dt)
            {
                night.Tick(Dt);
                var r = night.Dali.Room;
                bool daliPast8 = r == Room.Corridor5 || r == Room.PassageG || r == Room.Generator;
                Assert.IsFalse(daliPast8 && night.Doli.HeadingToGenerator, $"зерно {seed}, {t:0.0} с: Дали в {r}, Доли в {night.Doli.Room}");
            }
        }
    }

    // ── Панель сложности F1 ──

    [Test]
    public void SetBaseLevel_TurnsFurryOnAndOff_MidNight()
    {
        var night = new Night(new FakeOffice(), new[] { 0, 0, 0, 0, 0 }, 600f, 1);
        night.Tick(1f);
        Assert.IsFalse(night.Nova.Active);
        night.Nova.SetBaseLevel(20);
        Assert.IsTrue(night.Nova.Active);
        Assert.That(TickUntil(night, () => night.Nova.Room != Room.RestRoom, 20f) >= 0f, "Нова не пошла после включения");
        night.Nova.SetBaseLevel(0);
        Assert.IsFalse(night.Nova.Active);
        night.Stray.SetBaseLevel(25);
        Assert.AreEqual(20, night.Stray.BaseLevel, "больше 20 нельзя");
    }

    [Test]
    public void ForceNovaBattle_StartsBattleRightAway()
    {
        var night = new Night(new FakeOffice(), new[] { 0, 0, 0, 0, 0 }, 600f, 1);
        bool started = false;
        night.MogBattleStarted += n => started = true;
        night.ForceNovaBattle();
        Assert.IsTrue(started);
        Assert.IsTrue(night.Frozen);
        Assert.AreEqual(Room.DanceFloor, night.Nova.Room);
        Assert.IsTrue(night.Nova.Active, "выключенная Нова включается");
        night.FinishMogBattle(true);
        night.ForceNovaBattle(); // отдых после батла не мешает проверке
        Assert.IsTrue(night.Frozen);
    }

    // ── Дир ──

    [Test]
    public void Dir_Starves_ThenAttacksAfterGrace()
    {
        var dir = new DirAI(10, new FakeOffice(), Rng());
        bool alarm = false, attacked = false;
        dir.StartedStarving += d => alarm = true;
        dir.Attacked += (f, h) => attacked = true;
        float starve = RunUntil(dir, () => alarm, 200f);
        Assert.That(starve, Is.EqualTo(dir.FoodTime).Within(0.2f));
        Assert.IsFalse(attacked);
        float kill = RunUntil(dir, () => attacked, 10f);
        Assert.That(kill, Is.EqualTo(Balance.DirGraceTime).Within(0.2f));
    }

    [Test]
    public void Dir_FedDuringGrace_Survives()
    {
        var dir = new DirAI(10, new FakeOffice(), Rng());
        bool attacked = false;
        dir.Attacked += (f, h) => attacked = true;
        RunUntil(dir, () => dir.Starving, 200f);
        Run(dir, Balance.DirGraceTime * 0.5f);
        dir.Feed();
        Run(dir, Balance.DirGraceTime);
        Assert.IsFalse(attacked);
        Assert.IsFalse(dir.Starving);
    }

    [Test]
    public void Dir_Level0_NeverStarves()
    {
        var dir = new DirAI(0, new FakeOffice(), Rng());
        Run(dir, 600f);
        Assert.AreEqual(1f, dir.Food);
    }

    [Test]
    public void Dir_HigherLevel_EatsFaster()
    {
        Assert.That(new DirAI(15, new FakeOffice(), Rng()).FoodTime, Is.LessThan(new DirAI(5, new FakeOffice(), Rng()).FoodTime));
    }

    // ── Нова и ночь ──

    // Нова и Дали на 20 (Дали убить не может), остальных нет
    static Night NovaNight(FakeOffice office) => new Night(office, new[] { 0, 0, 20, 20, 0 }, 600f, 1);

    static float TickUntil(Night night, Func<bool> done, float limit)
    {
        for (float t = 0f; t < limit; t += Dt)
        {
            if (done()) return t;
            night.Tick(Dt);
        }
        return -1f;
    }

    [Test]
    public void Nova_OnDanceFloor_StartsBattle_FreezesNight_PushesOthersBack()
    {
        var office = new FakeOffice { DoorClosed = true };
        var night = NovaNight(office);
        NovaAI started = null;
        night.MogBattleStarted += n => started = n;
        Assert.That(TickUntil(night, () => started != null, 120f) >= 0f, "мог-батл не начался");
        Assert.IsTrue(night.Frozen);
        var daliRoom = night.Dali.Room;
        night.Tick(10f);
        Assert.AreEqual(daliRoom, night.Dali.Room, "во время батла все стоят");
    }

    [Test]
    public void Nova_BattleWon_NovaGoesHomeAndRests()
    {
        var night = NovaNight(new FakeOffice { DoorClosed = true });
        TickUntil(night, () => night.Frozen, 120f);
        night.FinishMogBattle(true);
        Assert.IsFalse(night.Over);
        Assert.AreEqual(Room.RestRoom, night.Nova.Room);
        Assert.AreEqual(1, night.Nova.Wins);
        Assert.That(night.Nova.RestLeft, Is.EqualTo(Balance.NovaRestAfterBattle));
    }

    [Test]
    public void Nova_BattleLost_KillsPlayer()
    {
        var night = NovaNight(new FakeOffice { DoorClosed = true });
        TickUntil(night, () => night.Frozen, 120f);
        night.FinishMogBattle(false);
        Assert.IsTrue(night.Over);
        Assert.AreEqual(FurryId.Nova, night.Killer.Id);
    }

    [Test]
    public void Night_LevelsRampEachQuarter()
    {
        var night = new Night(new FakeOffice(), new[] { 0, 0, 0, 5, 0 }, 100f, 1); // только Дали: убить не может
        night.Tick(0.01f);
        Assert.AreEqual(5, night.Dali.Level);
        for (int i = 0; i < 1100; i++) night.Tick(0.05f); // 55 с: третья четверть
        Assert.AreEqual(5 + 2 * Difficulty.RampPerQuarter, night.Dali.Level);
        Assert.AreEqual(0, night.Dir.Level, "уровень 0 не растёт: фурри не участвует");
    }

    [Test]
    public void Night_Describe_ShowsWhoIsInTheRoom()
    {
        var night = new Night(new FakeOffice(), new[] { 6, 5, 1, 6, 4 }, 600f, 1);
        StringAssert.Contains("Стрей", night.Describe(Room.RestRoom));
        StringAssert.Contains("Нова", night.Describe(Room.RestRoom));
        StringAssert.Contains("корм", night.Describe(Room.Feeding));
        Assert.AreEqual("пусто", night.Describe(Room.Corridor2));
    }

    // ── Симуляция баланса ──

    [Test]
    public void Sim_IsDeterministic()
    {
        var levels = Difficulty.Levels(DifficultyPreset.Normal);
        var a = NightSimulator.Run(levels, BotProfile.Average, 300f, 42);
        var b = NightSimulator.Run(levels, BotProfile.Average, 300f, 42);
        Assert.AreEqual(a.Survived, b.Survived);
        Assert.AreEqual(a.Time, b.Time);
        Assert.AreEqual(a.Killer, b.Killer);
    }

    [Test]
    public void Sim_AllLevelsZero_AlwaysSurvive()
    {
        var s = NightSimulator.RunMany(new[] { 0, 0, 0, 0, 0 }, BotProfile.Novice, 600f, 30);
        Assert.AreEqual(1f, s.SurvivalRate);
    }

    [Test]
    public void Sim_DifficultyOrder_EasyIsEasierThanHard()
    {
        var easy = NightSimulator.RunMany(Difficulty.Levels(DifficultyPreset.Easy), BotProfile.Average, 600f, 150);
        var normal = NightSimulator.RunMany(Difficulty.Levels(DifficultyPreset.Normal), BotProfile.Average, 600f, 150);
        var hard = NightSimulator.RunMany(Difficulty.Levels(DifficultyPreset.Hard), BotProfile.Average, 600f, 150);
        Assert.That(easy.SurvivalRate, Is.GreaterThan(normal.SurvivalRate));
        Assert.That(normal.SurvivalRate, Is.GreaterThan(hard.SurvivalRate));
    }

    [Test]
    public void Sim_SkillMatters()
    {
        var levels = Difficulty.Levels(DifficultyPreset.Normal);
        var skilled = NightSimulator.RunMany(levels, BotProfile.Skilled, 600f, 150);
        var novice = NightSimulator.RunMany(levels, BotProfile.Novice, 600f, 150);
        Assert.That(skilled.SurvivalRate, Is.GreaterThan(novice.SurvivalRate + 0.3f));
    }

    // Таблица баланса в лог теста (её же пишет меню SSDFNC → Balance Report)
    [Test]
    public void Sim_PrintReport()
    {
        TestContext.WriteLine(NightSimulator.Report(150, 600f));
        Assert.Pass();
    }
}
