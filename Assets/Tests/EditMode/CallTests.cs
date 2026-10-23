using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

// Тесты звонков: расписание, подстановка переменных, проверка шаблонов, сборка вариантов ответа
// и все json в Assets/StreamingAssets/Calls. Верность ответов проверяется независимо: тестовые шаблоны
// пишут ответы в виде «фурри;время;дело», их можно разобрать и сверить с расписанием.
public class CallTests
{
    static string Folder => Path.Combine(Application.streamingAssetsPath, "Calls");

    static Schedule NewSchedule(int seed) => Schedule.Generate(ScheduleData.Default(), new System.Random(seed));

    static CallTemplate Template(CallType type, string client, string answer = "", string yes = "", string no = "") =>
        new CallTemplate { id = "test", type = type.ToString(), client = client, answer = answer, yes = yes, no = no,
                           thanks = "ok", thanksNo = "ok-no" };

    static int FurryIndex(Schedule s, string name) => s.Furries.First(f => s.Furry(f).name == name);
    static int TimeIndex(Schedule s, string time) => s.Times.First(t => s.Time(t) == time);

    // ── Расписание ──

    [Test]
    public void Schedule_AlwaysFollowsRules()
    {
        for (int seed = 1; seed <= 300; seed++)
            Assert.That(NewSchedule(seed).FollowsRules(), $"seed {seed}");
    }

    [Test]
    public void Schedule_IsDifferentEachNight()
    {
        var a = NewSchedule(1);
        var b = NewSchedule(2);
        bool same = a.Furries.All(f => a.Times.All(t => a.Activity(f, t) == b.Activity(f, t)));
        Assert.That(same, Is.False);
    }

    [Test]
    public void Schedule_FreeWordHasGender()
    {
        var s = NewSchedule(1);
        Assert.That(s.FreeWord(FurryIndex(s, "Нова")), Is.EqualTo("свободна"));
        Assert.That(s.FreeWord(FurryIndex(s, "Доли")), Is.EqualTo("свободен"));
    }

    // ── Текст ──

    [Test]
    public void Text_FillsVariablesAndGender()
    {
        var vars = new Dictionary<string, string> { ["furry"] = "Нова", ["time"] = "01:00" };
        Assert.That(CallText.Fill("{furry} {свободен|свободна} в {time}", vars, true), Is.EqualTo("Нова свободна в 01:00"));
        Assert.That(CallText.Fill("{свободен|свободна}", vars, false), Is.EqualTo("свободен"));
    }

    [Test]
    public void Text_CheckFindsMistakes()
    {
        var allowed = new[] { "furry", "time" };
        Assert.That(CallText.Check("{furry} в {time}", allowed, true), Is.Null);
        Assert.That(CallText.Check("{fury} в {time}", allowed, true), Does.Contain("fury"), "опечатка в переменной");
        Assert.That(CallText.Check("{свободен|свободна}", allowed, false), Is.Not.Null, "род без фурри");
        Assert.That(CallText.Check("{furry в {time}", allowed, true), Is.Not.Null, "непарные скобки");
    }

    [Test]
    public void Template_ValidateFindsMistakes()
    {
        Assert.That(Template(CallType.WhenFurry, "{furry}?", "В {time}.").Validate(), Is.Null);
        Assert.That(Template(CallType.WhenFurry, "{furry}?", "Скоро.").Validate(), Is.Not.Null, "в ответе нет {time}");
        Assert.That(Template(CallType.OrderFurry, "{furry}?").Validate(), Is.Not.Null, "нет yes/no");
        Assert.That(Template(CallType.VipFree, "{room} {furry}?", yes: "да", no: "нет").Validate(), Is.Not.Null,
            "в звонке про VIP нет фурри");
        var bad = Template(CallType.WhoFree, "{time}?", "{furry}");
        bad.type = "WhoIsFree";
        Assert.That(bad.Validate(), Does.Contain("type"));
        bad.type = "3";
        Assert.That(bad.Validate(), Does.Contain("type"), "число вместо типа");
    }

    // ── Варианты ответа: верный правда верный, неверные правда неверные ──

    static IEnumerable<(Schedule s, Call call)> Calls(CallTemplate template, int count = 200)
    {
        var rng = new System.Random(5);
        for (int i = 0; i < count; i++)
        {
            var s = NewSchedule(i + 1);
            var call = CallBuilder.Build(template, s, rng);
            Assert.That(call, Is.Not.Null, $"seed {i + 1}");
            Assert.That(call.Options.Length, Is.InRange(2, CallBuilder.MaxOptions));
            Assert.That(call.Options.Distinct().Count(), Is.EqualTo(call.Options.Length), "варианты не повторяются");
            Assert.That(call.Correct, Is.InRange(0, call.Options.Length - 1));
            Assert.That(call.Reward, Is.InRange(Balance.CallRewardMin, Balance.CallRewardMax));
            yield return (s, call);
        }
    }

    [Test]
    public void Build_WhatAtTime()
    {
        foreach (var (s, call) in Calls(Template(CallType.WhatAtTime, "{time}", "{furry};{activity}")))
        {
            int t = TimeIndex(s, call.ClientLine);
            for (int i = 0; i < call.Options.Length; i++)
            {
                var p = call.Options[i].Split(';');
                bool truth = s.Activity(FurryIndex(s, p[0]), t) == p[1];
                Assert.That(truth, Is.EqualTo(i == call.Correct), $"{call.ClientLine}: {call.Options[i]}");
            }
        }
    }

    [Test]
    public void Build_WhenFurry()
    {
        foreach (var (s, call) in Calls(Template(CallType.WhenFurry, "{furry};{activity}", "{time}")))
        {
            var q = call.ClientLine.Split(';');
            int f = FurryIndex(s, q[0]);
            for (int i = 0; i < call.Options.Length; i++)
            {
                bool truth = s.Activity(f, TimeIndex(s, call.Options[i])) == q[1];
                Assert.That(truth, Is.EqualTo(i == call.Correct), $"{call.ClientLine}: {call.Options[i]}");
            }
        }
    }

    [Test]
    public void Build_FurryAtTime()
    {
        foreach (var (s, call) in Calls(Template(CallType.FurryAtTime, "{furry};{time}", "{activity}")))
        {
            var q = call.ClientLine.Split(';');
            int f = FurryIndex(s, q[0]), t = TimeIndex(s, q[1]);
            for (int i = 0; i < call.Options.Length; i++)
                Assert.That(s.Describe(f, t) == call.Options[i], Is.EqualTo(i == call.Correct), $"{call.ClientLine}: {call.Options[i]}");
        }
    }

    [Test]
    public void Build_WhoFree()
    {
        foreach (var (s, call) in Calls(Template(CallType.WhoFree, "{time}", "{furry}")))
        {
            int t = TimeIndex(s, call.ClientLine);
            for (int i = 0; i < call.Options.Length; i++)
                Assert.That(s.IsFree(FurryIndex(s, call.Options[i]), t), Is.EqualTo(i == call.Correct), $"{call.ClientLine}: {call.Options[i]}");
        }
    }

    [Test]
    public void Build_OrderFurry_YesOnlyIfFree()
    {
        int yes = 0, no = 0;
        foreach (var (s, call) in Calls(Template(CallType.OrderFurry, "{furry};{time}", yes: "да", no: "нет")))
        {
            var q = call.ClientLine.Split(';');
            bool free = s.IsFree(FurryIndex(s, q[0]), TimeIndex(s, q[1]));
            Assert.That(call.Options, Is.EqualTo(new[] { "да", "нет" }));
            Assert.That(call.Correct, Is.EqualTo(free ? 0 : 1));
            if (free) yes++;
            else no++;
        }
        Assert.That(yes, Is.GreaterThan(50), "ответ «да» бывает часто");
        Assert.That(no, Is.GreaterThan(50), "ответ «нет» бывает часто");
    }

    // Клиент не знает правильного ответа: реагирует на то, что ему сказали, верно это или нет
    [Test]
    public void Build_ReactionFollowsTheChosenAnswer()
    {
        foreach (var (s, call) in Calls(Template(CallType.OrderFurry, "{furry};{time}", yes: "да", no: "нет")))
            Assert.That(call.Reactions, Is.EqualTo(new[] { "ok", "ok-no" }), "«да» — рад, «нет» — жаль, при любом расписании");
        foreach (var (s, call) in Calls(Template(CallType.VipFree, "{room};{time}", yes: "да", no: "нет")))
            Assert.That(call.Reactions, Is.EqualTo(new[] { "ok", "ok-no" }));

        // У вариантов с выбором реакция подставляет то, что выбрали: «Тогда к Стрею и приду»
        var who = Template(CallType.WhoFree, "{time}", "{furry}");
        who.thanks = "к {furry_dat}";
        foreach (var (s, call) in Calls(who))
            for (int i = 0; i < call.Options.Length; i++)
            {
                var f = s.Furry(FurryIndex(s, call.Options[i]));
                Assert.That(call.Reactions[i], Is.EqualTo("к " + (string.IsNullOrEmpty(f.dat) ? f.name : f.dat)));
            }

        var noReaction = Template(CallType.OrderFurry, "{furry}?", yes: "да", no: "нет");
        noReaction.thanksNo = "";
        Assert.That(noReaction.Validate(), Does.Contain("thanksNo"));
    }

    [Test]
    public void Build_VipFree()
    {
        foreach (var (s, call) in Calls(Template(CallType.VipFree, "{room};{time}", yes: "да", no: "нет")))
        {
            var q = call.ClientLine.Split(';');
            Assert.That(call.Correct, Is.EqualTo(s.RoomFree(q[0], TimeIndex(s, q[1])) ? 0 : 1));
        }
    }

    [Test]
    public void Build_UsesCasesAndGender()
    {
        var tpl = Template(CallType.OrderFurry, "Можно {furry_acc}?", yes: "{furry} {свободен|свободна}", no: "нет");
        var rng = new System.Random(1);
        var lines = Enumerable.Range(1, 200).Select(i => CallBuilder.Build(tpl, NewSchedule(i), rng)).ToList();
        Assert.That(lines.Any(c => c.ClientLine == "Можно Нову?"));
        Assert.That(lines.Any(c => c.Options[0] == "Нова свободна"));
        Assert.That(lines.Any(c => c.ClientLine == "Можно Доли?"), "без падежей — имя как есть");
        Assert.That(lines.Any(c => c.Options[0] == "Доли свободен"));
    }

    // ── План звонков ──

    [Test]
    public void Plan_SpreadOverTheShift()
    {
        for (int seed = 1; seed <= 100; seed++)
        {
            var times = CallPlan.Times(600f, new System.Random(seed));
            Assert.That(times.Length, Is.InRange(Balance.CallsPerNightMin, Balance.CallsPerNightMax));
            Assert.That(times.First(), Is.GreaterThanOrEqualTo(Balance.CallFirstDelay));
            Assert.That(times.Last(), Is.LessThanOrEqualTo(600f - Balance.CallLastMargin));
            for (int i = 1; i < times.Length; i++)
                Assert.That(times[i] - times[i - 1], Is.GreaterThan(Balance.CallRingTime + Balance.CallReactionTime));
        }
        Assert.That(CallPlan.Times(30f, new System.Random(1)), Is.Empty, "в короткой смене звонков нет");
    }

    // ── Файлы ──

    [Test]
    public void Json_ScheduleIsValid()
    {
        var data = JsonUtility.FromJson<ScheduleData>(File.ReadAllText(Path.Combine(Folder, "schedule.json")));
        Assert.That(data.Validate(), Is.Null);
        for (int seed = 1; seed <= 50; seed++)
            Assert.That(Schedule.Generate(data, new System.Random(seed)).FollowsRules());
    }

    [Test]
    public void Json_AllDialoguesAreValidAndBuild()
    {
        var files = Directory.GetFiles(Path.Combine(Folder, "Dialogues"), "*.json");
        Assert.That(files, Is.Not.Empty);
        var ids = new HashSet<string>();
        var rng = new System.Random(3);
        foreach (var path in files)
        {
            var tpl = JsonUtility.FromJson<CallTemplate>(File.ReadAllText(path));
            Assert.That(tpl.Validate(), Is.Null, path);
            Assert.That(ids.Add(tpl.id), $"{path}: id «{tpl.id}» повторяется");
            for (int seed = 1; seed <= 30; seed++)
            {
                var call = CallBuilder.Build(tpl, NewSchedule(seed), rng);
                Assert.That(call, Is.Not.Null, path);
                Assert.That(call.ClientLine + string.Concat(call.Options) + string.Concat(call.Reactions), Does.Not.Contain("{"),
                    $"{path}: осталась неподставленная переменная");
            }
        }
    }
}
