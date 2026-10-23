using System;
using System.Collections.Generic;
using System.Linq;

// Готовый звонок: что сказал клиент, варианты ответа, какой из них верный и как клиент отреагирует на каждый.
// Клиент не знает правильного ответа (иначе бы не звонил): он верит тому, что ему сказали. Поэтому
// Reactions[i] — реакция на вариант i, а верно или нет, видно только по деньгам.
public class Call
{
    public string TemplateId;
    public CallType Type;
    public string Caller;
    public string ClientLine;
    public string[] Options;
    public string[] Reactions;
    public int Correct;
    public int Reward;
}

// Собирает звонок по шаблону и расписанию: выбирает, о чём спросить, находит верный ответ и
// неверные варианты. Неверные тоже берутся из расписания (другой час, другой фурри, другое дело),
// поэтому выглядят правдоподобно, но точно не совпадают ни с одним верным ответом.
public static class CallBuilder
{
    public const int MaxOptions = 3;

    // Кто, когда и что — значения переменных для текста
    struct Binding
    {
        public int Furry;      // -1 — фурри нет (VipFree)
        public int Time;
        public string Activity;
        public string Room;
    }

    // null — по этому расписанию шаблону нечего спросить (например, нет VIP-комнат); бери другой
    public static Call Build(CallTemplate template, Schedule s, Random rng)
    {
        if (!template.TryGetType(out var type)) return null;
        return CallTypes.IsYesNo(type) ? BuildYesNo(template, type, s, rng) : BuildChoice(template, type, s, rng);
    }

    static Call BuildChoice(CallTemplate tpl, CallType type, Schedule s, Random rng)
    {
        Binding question;
        List<Binding> right, candidates;
        switch (type)
        {
            case CallType.WhatAtTime:
            {
                int t = rng.Next(s.TimeCount);
                question = new Binding { Furry = -1, Time = t };
                right = s.BusyAt(t).Select(f => new Binding { Furry = f, Time = t, Activity = s.Activity(f, t) }).ToList();
                candidates = (from f in s.Furries
                              from a in Repertoire(s, f)
                              where s.Activity(f, t) != a
                              select new Binding { Furry = f, Time = t, Activity = a }).ToList();
                break;
            }
            case CallType.WhenFurry:
            {
                var busy = (from f in s.Furries from t in s.Times where !s.IsFree(f, t) select (f, t)).ToList();
                var (f0, t0) = busy[rng.Next(busy.Count)];
                string a = s.Activity(f0, t0);
                question = new Binding { Furry = f0, Time = t0, Activity = a };
                right = s.Times.Where(t => s.Activity(f0, t) == a)
                    .Select(t => new Binding { Furry = f0, Time = t, Activity = a }).ToList();
                candidates = s.Times.Where(t => s.Activity(f0, t) != a)
                    .Select(t => new Binding { Furry = f0, Time = t, Activity = a }).ToList();
                break;
            }
            case CallType.FurryAtTime:
            {
                int f = rng.Next(s.FurryCount), t = rng.Next(s.TimeCount);
                string truth = s.Describe(f, t);
                question = new Binding { Furry = f, Time = t, Activity = truth };
                right = new List<Binding> { question };
                candidates = Repertoire(s, f).Append(s.FreeWord(f)).Distinct().Where(a => a != truth)
                    .Select(a => new Binding { Furry = f, Time = t, Activity = a }).ToList();
                break;
            }
            default: // WhoFree
            {
                int t = rng.Next(s.TimeCount);
                question = new Binding { Furry = -1, Time = t };
                right = s.FreeAt(t).Select(f => new Binding { Furry = f, Time = t }).ToList();
                candidates = s.BusyAt(t).Select(f => new Binding { Furry = f, Time = t }).ToList();
                break;
            }
        }
        if (right.Count == 0) return null;

        var correct = right[rng.Next(right.Count)];
        // Клиент спросил только время — фурри и дело в его реплике (если они там есть) — из верного ответа
        if (type == CallType.WhatAtTime || type == CallType.WhoFree) question = correct;

        // Неверные: текст не совпадает ни с одним верным ответом и с другими вариантами
        var rightTexts = new HashSet<string>(right.Select(b => Fill(tpl.answer, s, b)));
        var chosen = new List<(string text, Binding b)> { (Fill(tpl.answer, s, correct), correct) };
        foreach (var b in candidates.OrderBy(_ => rng.Next()))
        {
            if (chosen.Count >= MaxOptions) break;
            string text = Fill(tpl.answer, s, b);
            if (!rightTexts.Contains(text) && chosen.All(o => o.text != text)) chosen.Add((text, b));
        }
        if (chosen.Count < 2) return null;

        // Перемешать, запомнив, где верный. Реакция — на выбранный вариант: «Тогда к Нове и приду»
        string correctText = chosen[0].text;
        chosen = chosen.OrderBy(_ => rng.Next()).ToList();
        return Make(tpl, type, s, rng, question,
            chosen.Select(o => o.text).ToArray(),
            chosen.Select(o => Fill(tpl.thanks, s, o.b)).ToArray(),
            chosen.FindIndex(o => o.text == correctText));
    }

    static Call BuildYesNo(CallTemplate tpl, CallType type, Schedule s, Random rng)
    {
        Binding question;
        bool truth;
        bool wantTrue = rng.Next(2) == 0; // поровну «да» и «нет», если в расписании есть оба случая
        if (type == CallType.OrderFurry)
        {
            var cells = (from f in s.Furries from t in s.Times select (f, t)).ToList();
            var pool = cells.Where(c => s.IsFree(c.f, c.t) == wantTrue).ToList();
            if (pool.Count == 0) pool = cells;
            var (f0, t0) = pool[rng.Next(pool.Count)];
            question = new Binding { Furry = f0, Time = t0, Activity = s.Describe(f0, t0) };
            truth = s.IsFree(f0, t0);
        }
        else // VipFree
        {
            if (s.Data.rooms.Length == 0) return null;
            var cells = (from r in s.Data.rooms from t in s.Times select (r, t)).ToList();
            var pool = cells.Where(c => s.RoomFree(c.r, c.t) == wantTrue).ToList();
            if (pool.Count == 0) pool = cells;
            var (r0, t0) = pool[rng.Next(pool.Count)];
            question = new Binding { Furry = -1, Time = t0, Room = r0 };
            truth = s.RoomFree(r0, t0);
        }
        // Сказал «да» — клиент рад и придёт, сказал «нет» — жаль; верно это было или нет, клиент не знает
        var options = new[] { Fill(tpl.yes, s, question), Fill(tpl.no, s, question) };
        var reactions = new[] { Fill(tpl.thanks, s, question), Fill(tpl.thanksNo, s, question) };
        return Make(tpl, type, s, rng, question, options, reactions, truth ? 0 : 1);
    }

    static Call Make(CallTemplate tpl, CallType type, Schedule s, Random rng, Binding b,
        string[] options, string[] reactions, int correct) =>
        new Call
        {
            TemplateId = tpl.id,
            Type = type,
            Caller = string.IsNullOrWhiteSpace(tpl.caller) ? "Клиент" : tpl.caller,
            ClientLine = Fill(tpl.client, s, b),
            Options = options,
            Reactions = reactions,
            Correct = correct,
            Reward = tpl.reward > 0 ? tpl.reward : rng.Next(Balance.CallRewardMin, Balance.CallRewardMax + 1),
        };

    // Дела, которые у фурри бывают: свои и VIP-комнаты
    static IEnumerable<string> Repertoire(Schedule s, int f) => s.Furry(f).activities.Concat(s.Data.rooms).Distinct();

    static string Fill(string text, Schedule s, Binding b)
    {
        var vars = new Dictionary<string, string> { ["time"] = s.Time(b.Time) };
        if (b.Activity != null) vars["activity"] = b.Activity;
        if (b.Room != null) vars["room"] = b.Room;
        bool female = false;
        if (b.Furry >= 0)
        {
            var f = s.Furry(b.Furry);
            female = f.female;
            vars["furry"] = f.name;
            vars["furry_gen"] = Or(f.gen, f.name);
            vars["furry_dat"] = Or(f.dat, f.name);
            vars["furry_acc"] = Or(f.acc, f.name);
            vars["furry_ins"] = Or(f.ins, f.name);
        }
        return CallText.Fill(text, vars, female);
    }

    static string Or(string form, string name) => string.IsNullOrWhiteSpace(form) ? name : form;
}
