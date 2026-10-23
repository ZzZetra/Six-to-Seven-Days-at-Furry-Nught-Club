using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

// Тесты мог-батла: раскладки (MogChart) и ход по ним (MogTimeline), плюс проверка всех json
// в Assets/StreamingAssets/MogCharts — если раскладку поправили руками с ошибкой, тест это покажет.
public class MogTests
{
    static MogChart Chart(params MogCheck[] checks) => new MogChart { name = "тест", checks = checks };

    static MogCheck Bar(float start, float hit, float pos = 0.5f, float zone = 0.2f) =>
        new MogCheck { type = "bar", start = start, hit = hit, pos = pos, zone = zone };

    [Test]
    public void Marker_ReachesZoneCenterExactlyOnHit()
    {
        var tl = new MogTimeline(Chart(Bar(1f, 2f, pos: 0.6f)));
        Assert.That(tl.MarkerPos(1f), Is.EqualTo(0f).Within(1e-5));
        Assert.That(tl.MarkerPos(2f), Is.EqualTo(0.6f).Within(1e-5));
        Assert.That(tl.MarkerPos(0.5f), Is.EqualTo(0f), "до start метка стоит у левого края");
    }

    [Test]
    public void Press_OnBeat_IsHit_EarlyPress_IsMiss()
    {
        var tl = new MogTimeline(Chart(Bar(0f, 1f), Bar(2f, 3f)));
        tl.Press(1f);
        Assert.That(tl.Hits, Is.EqualTo(1));
        tl.Press(2.1f); // метка у самого края — мимо зоны
        Assert.That(tl.Misses, Is.EqualTo(1));
        Assert.That(tl.Done);
    }

    [Test]
    public void Press_BeforeCheckStarts_DoesNothing()
    {
        var tl = new MogTimeline(Chart(Bar(5f, 6f)));
        tl.Press(1f);
        Assert.That(tl.Hits + tl.Misses, Is.EqualTo(0));
        Assert.That(tl.Index, Is.EqualTo(0));
    }

    [Test]
    public void NoPress_MissWhenMarkerLeavesBar()
    {
        var tl = new MogTimeline(Chart(Bar(0f, 1f, pos: 0.5f)));
        tl.Update(1.9f);
        Assert.That(tl.Misses, Is.EqualTo(0));
        tl.Update(2f); // метка дошла до правого края
        Assert.That(tl.Misses, Is.EqualTo(1));
    }

    [Test]
    public void NoPress_MissWhenNextCheckStarts()
    {
        var tl = new MogTimeline(Chart(Bar(0f, 1f, pos: 0.3f), Bar(1.5f, 2.5f)));
        tl.Update(1.5f);
        Assert.That(tl.Misses, Is.EqualTo(1));
        Assert.That(tl.Index, Is.EqualTo(1));
    }

    [Test]
    public void ZoneScale_MakesZoneNarrower()
    {
        var check = Bar(0f, 1f, pos: 0.5f, zone: 0.2f);
        // 0.08 правее центра: в обычную зону (±0.1) попадает, в суженную вдвое (±0.05) — нет
        float t = (0.5f + 0.08f) / 0.5f;
        Assert.That(new MogTimeline(Chart(check)).InZone(t));
        Assert.That(new MogTimeline(Chart(check), 0.5f).InZone(t), Is.False);
    }

    // n полосок подряд: на i-й бит в момент 2i + 1. Числа запаса — из Balance, чтобы тесты пережили настройку
    static MogTimeline Bars(int n) => new MogTimeline(Chart(Enumerable.Range(0, n)
        .Select(i => Bar(2f * i, 2f * i + 1f)).ToArray()));

    static void Hit(MogTimeline tl) => tl.Press(2f * tl.Index + 1f);
    static void Miss(MogTimeline tl) => tl.Press(2f * tl.Index + 0.05f);

    [Test]
    public void Lives_HitAddsUpToMax()
    {
        var tl = Bars(20);
        Assert.That(tl.Lives, Is.EqualTo(Balance.MogStartLives));
        Hit(tl);
        Assert.That(tl.Lives, Is.EqualTo(System.Math.Min(Balance.MogMaxLives, Balance.MogStartLives + Balance.MogLivesPerHit)));
        for (int i = 0; i < Balance.MogMaxLives; i++) Hit(tl);
        Assert.That(tl.Lives, Is.EqualTo(Balance.MogMaxLives), "выше максимума не растёт");
    }

    [Test]
    public void Lives_MissTakesOne_ZeroEndsBattleAtOnce()
    {
        var tl = Bars(20);
        for (int i = 0; i < Balance.MogStartLives - 1; i++) Miss(tl);
        Assert.That(tl.Lives, Is.EqualTo(1));
        Assert.That(tl.Done, Is.False);
        Miss(tl);
        Assert.That(tl.Lives, Is.EqualTo(0));
        Assert.That(tl.Done, "на нуле батл кончается сразу, остальные чеки не играются");
        Assert.That(tl.Index, Is.EqualTo(Balance.MogStartLives));
        Assert.That(tl.PlayerWon, Is.False);
        tl.Update(100f);
        Assert.That(tl.Misses, Is.EqualTo(Balance.MogStartLives), "после конца промахи не считаются");
    }

    [Test]
    public void Lives_OneLeftAtTheEnd_IsSurvival()
    {
        var tl = Bars(Balance.MogStartLives - 1); // промахнулся во всех — остался ровно 1
        while (!tl.Done) Miss(tl);
        Assert.That(tl.Lives, Is.EqualTo(1));
        Assert.That(tl.PlayerWon, "дожил до конца с 1 — выжил");
    }

    [Test]
    public void Lives_AfterHitsMissesStillCount()
    {
        var tl = Bars(20);
        Hit(tl);
        int lives = tl.Lives;
        for (int i = 0; i < lives - 1; i++) Miss(tl);
        Assert.That(tl.Lives, Is.EqualTo(1));
        Hit(tl);
        Miss(tl);
        Assert.That(tl.Lives, Is.EqualTo(System.Math.Min(Balance.MogMaxLives, 1 + Balance.MogLivesPerHit) - 1));
    }

    [Test]
    public void Checks_AreSortedByStart_AndProgressCountsByType()
    {
        var chart = Chart(
            new MogCheck { type = "sides", start = 6f, hit = 7f },
            new MogCheck { type = "circle", start = 0f, hit = 1f },
            new MogCheck { type = "circle", start = 2f, hit = 3f },
            new MogCheck { type = "Bar", start = 4f, hit = 5f });
        var tl = new MogTimeline(chart);
        Assert.That(tl.Current.Type, Is.EqualTo(MogCheckType.Circle));
        Assert.That(tl.ProgressInType(), Is.EqualTo((1, 2)));
        tl.Press(1f);
        tl.Press(3f);
        Assert.That(tl.Current.Type, Is.EqualTo(MogCheckType.Bar), "тип без учёта регистра");
        Assert.That(tl.ProgressInType(), Is.EqualTo((1, 1)));
        Assert.That(MogTimeline.PhaseNumber(MogCheckType.Sides), Is.EqualTo(4));
    }

    [Test]
    public void Validate_RejectsBrokenChecks()
    {
        Assert.That(Chart().Validate(), Is.Not.Null, "пустая раскладка");
        Assert.That(Chart(Bar(2f, 2f)).Validate(), Is.Not.Null, "hit не позже start");
        Assert.That(Chart(Bar(0f, 1f, zone: 0f)).Validate(), Is.Not.Null);
        Assert.That(Chart(Bar(0f, 1f, pos: 1.5f)).Validate(), Is.Not.Null);
        Assert.That(Chart(Bar(0f, 1f)).Validate(), Is.Null);
    }

    [Test]
    public void Default_IsFiveCirclesFiveBarsFiveSides()
    {
        var chart = MogChart.Default();
        Assert.That(chart.Validate(), Is.Null);
        var tl = new MogTimeline(chart);
        Assert.That(tl.Count, Is.EqualTo(15));
        int[] perType = new int[3];
        MogCheckType last = MogCheckType.Circle;
        foreach (var c in chart.checks)
        {
            Assert.That(c.Type, Is.GreaterThanOrEqualTo(last), "фазы идут по порядку: кружок → полоска → в стороны");
            last = c.Type;
            perType[(int)c.Type]++;
        }
        Assert.That(perType, Is.EqualTo(new[] { 5, 5, 5 }));
    }

    // ── Файлы раскладок ──

    static string Folder => Path.Combine(Application.streamingAssetsPath, "MogCharts");

    [Test]
    public void Json_MissingFieldsTakeDefaults()
    {
        var chart = JsonUtility.FromJson<MogChart>("{\"checks\":[{\"start\":1,\"hit\":2}]}");
        Assert.That(chart.checks[0].zone, Is.EqualTo(0.22f).Within(1e-5));
        Assert.That(chart.checks[0].pos, Is.EqualTo(0.6f).Within(1e-5));
        Assert.That(chart.checks[0].Type, Is.EqualTo(MogCheckType.Bar));
        Assert.That(chart.Validate(), Is.Null);
    }

    [Test]
    public void Json_DefaultFileMatchesCode()
    {
        var file = JsonUtility.FromJson<MogChart>(File.ReadAllText(Path.Combine(Folder, "default.json")));
        var code = MogChart.Default();
        Assert.That(file.checks.Length, Is.EqualTo(code.checks.Length));
        for (int i = 0; i < code.checks.Length; i++)
        {
            Assert.That(file.checks[i].Type, Is.EqualTo(code.checks[i].Type));
            Assert.That(file.checks[i].start, Is.EqualTo(code.checks[i].start).Within(1e-4));
            Assert.That(file.checks[i].hit, Is.EqualTo(code.checks[i].hit).Within(1e-4));
            Assert.That(file.checks[i].pos, Is.EqualTo(code.checks[i].pos).Within(1e-4));
        }
    }

    [Test]
    public void Json_AllChartsAreValid_AndMusicExists()
    {
        var files = Directory.GetFiles(Folder, "*.json", SearchOption.AllDirectories);
        Assert.That(files, Is.Not.Empty);
        foreach (var path in files)
        {
            var chart = JsonUtility.FromJson<MogChart>(File.ReadAllText(path));
            Assert.That(chart.Validate(), Is.Null, path);
            if (!string.IsNullOrEmpty(chart.music))
                Assert.That(File.Exists(Path.Combine(Folder, chart.music)), $"{path}: нет файла музыки {chart.music}");
        }
    }
}
