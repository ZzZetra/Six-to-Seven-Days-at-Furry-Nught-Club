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
    public void Checks_AreSortedByStart_AndPhasesComeFromGroups()
    {
        var chart = Chart(
            new MogCheck { type = "sides", start = 6f, hit = 7f },
            new MogCheck { type = "circle", start = 0f, hit = 1f },
            new MogCheck { type = "circle", start = 2f, hit = 3f },
            new MogCheck { type = "Bar", start = 4f, hit = 5f });
        var tl = new MogTimeline(chart);
        Assert.That(tl.PhaseCount, Is.EqualTo(5), "Появление + кружки + полоска + стрелка + конец");
        Assert.That(tl.Current.Type, Is.EqualTo(MogCheckType.Circle));
        Assert.That(tl.Phase, Is.EqualTo(2));
        Assert.That(tl.ProgressInGroup(), Is.EqualTo((1, 2)));
        tl.Press(1f);
        tl.Press(3f);
        Assert.That(tl.Current.Type, Is.EqualTo(MogCheckType.Bar), "тип без учёта регистра");
        Assert.That(tl.Phase, Is.EqualTo(3));
        Assert.That(tl.ProgressInGroup(), Is.EqualTo((1, 1)));
        tl.Press(5f);
        Assert.That(tl.Current.Type, Is.EqualTo(MogCheckType.Arrow), "старое sides — стрелка");
        Assert.That(tl.Phase, Is.EqualTo(4));
    }

    // ── Виды скилл-чеков ──

    [Test]
    public void Circle_NeedleReachesZoneOnBeat_PassingTheZoneIsAMiss()
    {
        var c = new MogCheck { type = "circle", hit = 3f, pos = 0.5f, zone = 0.2f, speed = 0.5f };
        Assert.That(c.StartTime, Is.EqualTo(2f).Within(1e-5), "полоборота со скоростью 0.5 об/с — за 1 с до бита");
        var tl = new MogTimeline(Chart(c));
        Assert.That(tl.NeedleAngle(3f), Is.EqualTo(0.5f).Within(1e-5));
        Assert.That(tl.InZone(3.15f), "0.575 — ещё в зоне ±0.1");
        Assert.That(tl.InZone(2.7f), Is.False, "0.35 — до зоны");
        tl.Update(3.19f);
        Assert.That(tl.Misses, Is.EqualTo(0));
        tl.Update(3.25f); // 0.625 — стрелка прошла зону
        Assert.That(tl.Misses, Is.EqualTo(1));
    }

    [Test]
    public void Button_HitWindowAroundBeat_EarlyClickIsAMiss()
    {
        MogTimeline One() => new MogTimeline(Chart(new MogCheck { type = "button", hit = 2f, approach = 1f, x = 0.3f, y = 0.7f }));
        var tl = One();
        Assert.That(tl.Active(0.9f), Is.False, "кнопка появляется за approach до бита");
        Assert.That(tl.RingProgress(1.5f), Is.EqualTo(0.5f).Within(1e-5));
        tl.Press(2f + Balance.MogHitWindow * 0.9f);
        Assert.That(tl.Hits, Is.EqualTo(1));
        tl = One();
        tl.Press(1.5f);
        Assert.That(tl.Misses, Is.EqualTo(1), "нажал рано");
        tl = One();
        tl.Update(2f + Balance.MogHitWindow + 0.01f);
        Assert.That(tl.Misses, Is.EqualTo(1), "не успел");
    }

    [Test]
    public void DenseChecks_NextRingShrinkingDoesNotKillCurrent()
    {
        // Бит каждые 0.5 с, кольцо сжимается 1 с: следующая кнопка «началась» раньше, чем прошёл бит текущей
        var tl = new MogTimeline(Chart(
            new MogCheck { type = "button", hit = 3.5f, approach = 1f },
            new MogCheck { type = "arrow", hit = 4f, approach = 1f, dir = "down" },
            new MogCheck { type = "circle", hit = 4.5f, pos = 0.5f, zone = 0.2f, speed = 0.6f }));
        tl.Update(3.1f);
        Assert.That(tl.Misses, Is.EqualTo(0), "следующая стрелка уже сжимается, но в неё ещё не попасть");
        tl.Press(3.5f);
        Assert.That(tl.Hits, Is.EqualTo(1));
        Assert.That(tl.RingProgress(3.5f), Is.EqualTo(0.5f).Within(1e-5), "кольцо стрелки показывается уже наполовину сжатым");
        tl.Swipe(4f, MogDir.Down);
        Assert.That(tl.Hits, Is.EqualTo(2));
        Assert.That(tl.NeedleAngle(4.2f), Is.GreaterThan(0.3f), "стрелка кружка уже бежит по кругу");
        tl.Press(4.5f);
        Assert.That(tl.Hits, Is.EqualTo(3));

        // Длинная полоска (до 5 с) не нажата, а у кнопки на 3 с открылось окно — полоска промах, кнопка играется
        tl = new MogTimeline(Chart(
            new MogCheck { type = "bar", start = 0f, end = 5f, pos = 0.5f, zone = 0.2f, speed = 0.5f },
            new MogCheck { type = "button", hit = 3f, approach = 1f }));
        tl.Update(2.5f);
        Assert.That(tl.Misses, Is.EqualTo(0));
        tl.Update(3f - Balance.MogHitWindow);
        Assert.That(tl.Misses, Is.EqualTo(1));
        tl.Press(3f);
        Assert.That(tl.Hits, Is.EqualTo(1));
    }

    [Test]
    public void Arrow_WaitsForRightSwipe_UntilRingCloses()
    {
        // Стрелка появляется в 1 с (approach 1) и ждёт взмаха вверх до 2 с (плюс запас окна)
        MogTimeline One() => new MogTimeline(Chart(new MogCheck { type = "arrow", hit = 2f, dir = "up" }));
        var tl = One();
        tl.Press(1.5f);
        Assert.That(tl.Hits + tl.Misses, Is.EqualTo(0), "ПРОБЕЛ на стрелку не действует");
        tl.Swipe(1.1f, MogDir.Left);
        tl.Swipe(1.2f, MogDir.Down);
        Assert.That(tl.Hits + tl.Misses, Is.EqualTo(0), "не в ту сторону — не считается, ждёт дальше");
        tl.Swipe(1.3f, MogDir.Up);
        Assert.That(tl.Hits, Is.EqualTo(1), "махнул в её сторону задолго до конца — попал");

        tl = One();
        tl.Swipe(0.9f, MogDir.Up);
        Assert.That(tl.Hits + tl.Misses, Is.EqualTo(0), "до появления взмах не действует");

        tl = One();
        tl.Update(2f);
        Assert.That(tl.Misses, Is.EqualTo(0), "кольцо сомкнулось — ещё запас окна");
        tl.Update(2f + Balance.MogHitWindow + 0.01f);
        Assert.That(tl.Misses, Is.EqualTo(1), "не успел — промах");
        tl.Swipe(2.3f, MogDir.Up);
        Assert.That(tl.Hits, Is.EqualTo(0), "после промаха взмах уже не считается");
    }

    [Test]
    public void Bar_CanStartFromRight()
    {
        var right = new MogCheck { type = "bar", start = 0f, end = 4f, speed = 1f, pos = 0.3f, zone = 0.2f, side = "right" };
        Assert.That(right.BarPos(0f), Is.EqualTo(1f).Within(1e-5), "до старта метка у правого края");
        Assert.That(right.BarPos(0.25f), Is.EqualTo(0.75f).Within(1e-5), "едет влево");
        Assert.That(right.BarPos(1.5f), Is.EqualTo(0.5f).Within(1e-5), "отскок от левого края — едет обратно");
        var tl = new MogTimeline(Chart(right));
        Assert.That(tl.InZone(0.7f), "зона на 0.3 от левого края — метка там через 0.7 с");
        Assert.That(tl.InZone(0.3f), Is.False);

        var old = new MogCheck { type = "bar", start = 0f, hit = 1f, pos = 0.25f, side = "right" };
        Assert.That(old.BarPos(1f), Is.EqualTo(0.25f).Within(1e-5), "без speed: от правого края до зоны ровно к hit");

        var chart = Chart(right);
        var back = UnityEngine.JsonUtility.FromJson<MogChart>(chart.ToJson());
        Assert.That(back.checks[0].FromRight, "side переживает сохранение");
        Assert.That(Chart(new MogCheck { type = "bar", start = 0f, end = 2f, speed = 1f, side = "up" }).Validate(), Does.Contain("side"));
    }

    [Test]
    public void Bar_BouncesAtEdges_AndChangesSpeed()
    {
        var bounce = new MogCheck { type = "bar", start = 0f, end = 4f, speed = 1f };
        Assert.That(bounce.BarPos(0.5f), Is.EqualTo(0.5f).Within(1e-5));
        Assert.That(bounce.BarPos(1.5f), Is.EqualTo(0.5f).Within(1e-5), "дошла до края и едет назад");
        Assert.That(bounce.BarPos(2.25f), Is.EqualTo(0.25f).Within(1e-5), "отскок от левого края");

        var keys = new MogCheck
        {
            type = "bar", start = 0f, end = 5f, speed = 0.5f,
            speedKeys = new[] { new MogSpeedKey { at = 1f, speed = 0.25f }, new MogSpeedKey { at = 2f, speed = -0.5f } },
        };
        Assert.That(keys.BarPos(1f), Is.EqualTo(0.5f).Within(1e-5));
        Assert.That(keys.BarPos(2f), Is.EqualTo(0.75f).Within(1e-5), "медленнее после 1 с");
        Assert.That(keys.BarPos(2.5f), Is.EqualTo(0.5f).Within(1e-5), "с 2 с — назад");

        var tl = new MogTimeline(Chart(new MogCheck { type = "bar", start = 0f, end = 3f, speed = 1f, pos = 0.5f, zone = 0.2f }));
        tl.Update(1.5f);
        Assert.That(tl.Misses, Is.EqualTo(0), "после отскока полоска ещё идёт");
        tl.Press(1.5f);
        Assert.That(tl.Hits, Is.EqualTo(1), "на обратном пути метка снова в зоне");
    }

    [Test]
    public void Validate_ChecksEveryType()
    {
        Assert.That(Chart(new MogCheck { type = "circle", hit = 1f, start = 1f, pos = 0.5f }).Validate(), Does.Contain("speed"));
        Assert.That(Chart(new MogCheck { type = "button", hit = 1f, approach = 0.1f }).Validate(), Does.Contain("approach"));
        Assert.That(Chart(new MogCheck { type = "arrow", hit = 1f, dir = "back" }).Validate(), Does.Contain("dir"));
        Assert.That(Chart(new MogCheck { type = "button", hit = 1f, x = 1.5f }).Validate(), Does.Contain("x"));
        Assert.That(Chart(new MogCheck { type = "arrow", hit = 1f, dir = "down" },
                          new MogCheck { type = "circle", hit = 3f, pos = 0.3f, speed = 0.5f }).Validate(), Is.Null);
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
    public void Default_IsFiveCirclesFiveBarsFiveArrows()
    {
        var chart = MogChart.Default();
        Assert.That(chart.Validate(), Is.Null);
        var tl = new MogTimeline(chart);
        Assert.That(tl.Count, Is.EqualTo(15));
        Assert.That(tl.PhaseCount, Is.EqualTo(5), "Появление → кружки → полоски → стрелки → конец");
        var order = chart.checks.Select(c => c.Type).Distinct().ToArray();
        Assert.That(order, Is.EqualTo(new[] { MogCheckType.Circle, MogCheckType.Bar, MogCheckType.Arrow }));
        Assert.That(chart.checks.Count(c => c.Type == MogCheckType.Circle), Is.EqualTo(5));
        Assert.That(chart.checks.Count(c => c.Type == MogCheckType.Bar), Is.EqualTo(5));
        Assert.That(chart.checks.Count(c => c.Type == MogCheckType.Arrow), Is.EqualTo(5));
        for (int i = 1; i < chart.checks.Length; i++)
            Assert.That(chart.checks[i].StartTime, Is.GreaterThanOrEqualTo(chart.checks[i - 1].EndTime(Balance.MogHitWindow)),
                "чеки не накладываются");
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
            Assert.That(file.checks[i].StartTime, Is.EqualTo(code.checks[i].StartTime).Within(1e-4));
            Assert.That(file.checks[i].hit, Is.EqualTo(code.checks[i].hit).Within(1e-4));
            Assert.That(file.checks[i].pos, Is.EqualTo(code.checks[i].pos).Within(1e-4));
            Assert.That(file.checks[i].Speed, Is.EqualTo(code.checks[i].Speed).Within(1e-4));
            Assert.That(file.checks[i].Direction, Is.EqualTo(code.checks[i].Direction));
        }
    }

    // Редактор сохраняет через ToJson: всё читается обратно, числа с точкой даже на русской Windows
    [Test]
    public void Json_SaveAndLoadKeepsEveryType()
    {
        var culture = System.Threading.Thread.CurrentThread.CurrentCulture;
        System.Threading.Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("ru-RU");
        try
        {
            var chart = new MogChart
            {
                name = "Проверка \"кавычек\"", music = "Music/song.ogg", offset = 0.125f,
                checks = new[]
                {
                    new MogCheck { type = "circle", hit = 2.5f, pos = 0.3f, zone = 0.15f, speed = 0.75f },
                    new MogCheck { type = "button", hit = 4f, x = 0.2f, y = 0.8f, approach = 0.6f },
                    new MogCheck { type = "arrow", hit = 5.5f, dir = "down", x = 0.7f, approach = 0.9f },
                    new MogCheck { type = "bar", start = 7f, end = 10f, hit = 8f, pos = 0.4f, zone = 0.2f, speed = 0.5f,
                        speedKeys = new[] { new MogSpeedKey { at = 8.5f, speed = -0.25f } } },
                },
            };
            string json = chart.ToJson();
            Assert.That(json, Does.Contain("0.125").And.Not.Contain("0,125"));
            var back = JsonUtility.FromJson<MogChart>(json);
            Assert.That(back.Validate(), Is.Null);
            Assert.That(back.name, Is.EqualTo(chart.name));
            Assert.That(back.music, Is.EqualTo(chart.music));
            for (int i = 0; i < chart.checks.Length; i++)
            {
                MogCheck a = chart.checks[i], b = back.checks[i];
                Assert.That(b.Type, Is.EqualTo(a.Type));
                Assert.That(b.StartTime, Is.EqualTo(a.StartTime).Within(1e-3), a.type);
                Assert.That(b.hit, Is.EqualTo(a.hit).Within(1e-3));
                Assert.That(b.Direction, Is.EqualTo(a.Direction));
                Assert.That(b.x, Is.EqualTo(a.x).Within(1e-3));
                Assert.That(b.BarPos(9f), Is.EqualTo(a.BarPos(9f)).Within(1e-3));
            }
        }
        finally
        {
            System.Threading.Thread.CurrentThread.CurrentCulture = culture;
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
