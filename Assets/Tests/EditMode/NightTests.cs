using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

// Тесты ночей прохождения (Nights.cs): кто участвует, рост сложности, сбои монитора, звонки — и звонков
// начальства (BossCallPlan, файлы Calls/Boss)
public class NightTests
{
    const int Dir = 0, Stray = 1, Nova = 2, Dali = 3, Doli = 4;

    static NightConfig N(int n) => Nights.Get(n);

    [Test]
    public void SevenNightsOfSixMinutes()
    {
        Assert.That(Nights.Count, Is.EqualTo(7));
        Assert.That(Balance.ShiftDuration, Is.EqualTo(360f), "ночь — 6 минут");
        for (int n = 1; n <= Nights.Count; n++) Assert.That(N(n).Number, Is.EqualTo(n));
    }

    [Test]
    public void WhoTakesPart()
    {
        // 1-я: Стрей, Дали и Нова
        Assert.That(N(1).Levels[Dir], Is.Zero);
        Assert.That(N(1).Levels[Doli], Is.Zero);
        Assert.That(new[] { Stray, Dali, Nova }.All(i => N(1).Levels[i] > 0));
        // 2-я и 3-я: Дир, Стрей, Дали и Нова
        foreach (int n in new[] { 2, 3 })
        {
            Assert.That(N(n).Levels[Doli], Is.Zero, $"ночь {n}: без Доли");
            Assert.That(new[] { Dir, Stray, Dali, Nova }.All(i => N(n).Levels[i] > 0), $"ночь {n}");
        }
        // с 4-й — все пятеро
        for (int n = 4; n <= Nights.Count; n++)
            Assert.That(N(n).Levels.All(l => l > 0), $"ночь {n}: все пятеро");
    }

    [Test]
    public void HarderEachNight()
    {
        for (int n = 2; n <= Nights.Count; n++)
        {
            for (int i = 0; i < 5; i++)
            {
                // кроме 4-й: пришла Доли — Стрей и Дали полегче, чем на 3-й (пользователь, 2026-11-06)
                if (n == 4 && (i == Stray || i == Dali))
                    Assert.That(N(n).Levels[i], Is.LessThan(N(n - 1).Levels[i]), $"ночь 4: {(FurryId)i} полегче");
                else Assert.That(N(n).Levels[i], Is.GreaterThanOrEqualTo(N(n - 1).Levels[i]), $"ночь {n}, фурри {(FurryId)i}");
            }
            Assert.That(N(n).Levels.Sum(), Is.GreaterThan(N(n - 1).Levels.Sum()), $"ночь {n} сложнее {n - 1}-й");
            Assert.That(N(n).CallsMax, Is.GreaterThanOrEqualTo(N(n - 1).CallsMax));
        }
    }

    [Test]
    public void MonitorRebootsMoreFromNight3()
    {
        Assert.That(N(1).MonitorRebootChancePerMinute, Is.EqualTo(Balance.MonitorRebootChancePerMinute));
        Assert.That(N(2).MonitorRebootChancePerMinute, Is.EqualTo(Balance.MonitorRebootChancePerMinute));
        Assert.That(Balance.MonitorRebootChanceHard, Is.EqualTo(0.18f));
        for (int n = 3; n <= Nights.Count; n++)
            Assert.That(N(n).MonitorRebootChancePerMinute, Is.EqualTo(Balance.MonitorRebootChanceHard), $"ночь {n}");
    }

    [Test]
    public void UpToNineCalls()
    {
        for (int n = 1; n <= Nights.Count; n++)
        {
            Assert.That(N(n).CallsMax, Is.InRange(N(n).CallsMin, 9), $"ночь {n}");
            for (int seed = 1; seed <= 50; seed++)
            {
                var times = CallPlan.Times(Balance.ShiftDuration, new System.Random(seed), N(n).CallsMin, N(n).CallsMax);
                Assert.That(times.Length, Is.InRange(N(n).CallsMin, N(n).CallsMax));
                Assert.That(times.Last(), Is.LessThanOrEqualTo(Balance.ShiftDuration - Balance.CallLastMargin));
            }
        }
        Assert.That(N(Nights.Count).CallsMax, Is.EqualTo(9));
    }

    // Бот-игрок средний: с каждой ночью выживает реже, 1-я проходится почти всегда
    [Test]
    public void SimulationGetsHarder()
    {
        float Survival(int n) => NightSimulator.RunMany(N(n).Levels, BotProfile.Average, Balance.ShiftDuration, 80, 1,
            N(n).MonitorRebootChancePerMinute).SurvivalRate;
        float first = Survival(1), fourth = Survival(4), last = Survival(Nights.Count);
        Assert.That(first, Is.GreaterThan(0.7f), "1-я ночь");
        Assert.That(fourth, Is.LessThan(first));
        Assert.That(last, Is.LessThan(fourth));
    }

    // ── Звонки начальства ──

    [Test]
    public void BossCalls_StartAndMiddle()
    {
        var talks = new[]
        {
            new PhoneTalk { lines = new[] { "a" } },                    // первый без when — в начале
            new PhoneTalk { lines = new[] { "b" } },                    // следующие без when — в середине
            new PhoneTalk { lines = new[] { "c" }, when = "start" },
            new PhoneTalk { lines = new[] { "d" }, when = "middle" },
        };
        for (int seed = 1; seed <= 50; seed++)
        {
            var t = BossCallPlan.Times(talks, 360f, new System.Random(seed));
            foreach (int i in new[] { 0, 2 }) Assert.That(t[i], Is.InRange(Balance.BossCallStartFrom, Balance.BossCallStartTo));
            foreach (int i in new[] { 1, 3 })
                Assert.That(t[i], Is.InRange(360f * Balance.BossCallMiddleFrom, 360f * Balance.BossCallMiddleTo));
        }
    }

    [Test]
    public void BossCalls_Validate()
    {
        Assert.That(new PhoneTalk { lines = new[] { "a" } }.Validate(), Is.Null);
        Assert.That(new PhoneTalk().Validate(), Is.Not.Null, "без реплик");
        Assert.That(new PhoneTalk { lines = new[] { "a" }, when = "night" }.Validate(), Is.Not.Null, "when — только start или middle");
    }

    // Файлы Calls/Boss/night_N.json и Calls/Triggered/*.json читаются, звонки в них правильные. Начальство звонит
    // в начале 1-й, 2-й и 4-й ночи (пользователь, 2026-11-06), в остальные — не звонит (файла нет)
    [Test]
    public void BossCalls_JsonFilesAreValid()
    {
        string folder = Path.Combine(Application.streamingAssetsPath, "Calls");
        string triggered = Path.Combine(folder, "Triggered");
        var files = Directory.GetFiles(Path.Combine(folder, "Boss"), "*.json")
            .Concat(Directory.Exists(triggered) ? Directory.GetFiles(triggered, "*.json") : new string[0]).ToArray();
        Assert.That(files, Is.Not.Empty);
        foreach (var path in files)
        {
            var data = JsonUtility.FromJson<PhoneTalkFile>(File.ReadAllText(path));
            Assert.That(data?.calls, Is.Not.Null.And.Not.Empty, Path.GetFileName(path));
            foreach (var talk in data.calls) Assert.That(talk.Validate(), Is.Null, Path.GetFileName(path));
        }
        foreach (int n in new[] { 1, 2, 4 })
        {
            string path = Path.Combine(folder, "Boss", $"night_{n}.json");
            Assert.That(File.Exists(path), $"night_{n}.json");
            var calls = JsonUtility.FromJson<PhoneTalkFile>(File.ReadAllText(path)).calls;
            Assert.That(calls.Length, Is.EqualTo(1), $"ночь {n}: один звонок");
            Assert.That(calls[0].when, Is.EqualTo("start"), $"ночь {n}: в начале");
        }
        foreach (int n in new[] { 3, 5, 6, 7 })
            Assert.That(File.Exists(Path.Combine(folder, "Boss", $"night_{n}.json")), Is.False, $"ночь {n}: без звонка");
    }
}
