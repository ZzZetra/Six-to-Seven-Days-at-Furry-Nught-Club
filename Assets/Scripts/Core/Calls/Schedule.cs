using System;
using System.Collections.Generic;
using System.Linq;

// Расписание ночной программы клуба — табличка над столом, по ней отвечают на звонки.
// Каждую ночь новое: Schedule.Generate собирает его из ScheduleData (Assets/StreamingAssets/Calls/schedule.json,
// формат — docs/CALLS.md) случайно, но по правилам:
//   в каждый час хотя бы один фурри свободен и хотя бы один занят;
//   у каждого фурри есть и дела, и хотя бы один свободный час;
//   VIP-комната в один час занята не больше чем одним фурри.
[Serializable]
public class ScheduleFurry
{
    public string name = "";
    public bool female;
    // Падежи для текста звонков: {furry_gen} — «у Новы», {furry_dat} — «к Нове», {furry_acc} — «на Нову»,
    // {furry_ins} — «с Новой». Пусто — подставится имя как есть (Дали, Доли не склоняются)
    public string gen = "";
    public string dat = "";
    public string acc = "";
    public string ins = "";
    public string[] activities = new string[0];
}

[Serializable]
public class ScheduleData
{
    public string[] times = new string[0];     // строки таблицы: «22:00», «23:00»…
    public string[] rooms = new string[0];     // VIP-комнаты: одна комната — один фурри в час
    public float freeChance = 0.35f;           // доля свободных клеток
    public float roomChance = 0.2f;            // из занятых — доля VIP
    public ScheduleFurry[] furries = new ScheduleFurry[0];

    // Текст ошибки или null, если всё хорошо
    public string Validate()
    {
        if (times == null || times.Length < 2) return "нужно хотя бы 2 времени в «times»";
        if (furries == null || furries.Length < 3) return "нужно хотя бы 3 фурри в «furries»";
        foreach (var f in furries)
        {
            if (string.IsNullOrWhiteSpace(f.name)) return "у фурри пустое «name»";
            if (f.activities == null || f.activities.Length == 0) return $"у {f.name} нет ни одного дела в «activities»";
        }
        if (furries.Select(f => f.name).Distinct().Count() != furries.Length) return "имена фурри повторяются";
        if (freeChance <= 0f || freeChance >= 1f) return "«freeChance» должен быть больше 0 и меньше 1";
        if (roomChance < 0f || roomChance > 1f) return "«roomChance» должен быть от 0 до 1";
        if (rooms == null) rooms = new string[0];
        return null;
    }

    // Такое же, как Assets/StreamingAssets/Calls/schedule.json — на случай, если файла нет
    public static ScheduleData Default() => new ScheduleData
    {
        times = new[] { "22:00", "23:00", "00:00", "01:00", "02:00", "03:00" },
        rooms = new[] { "VIP-1", "VIP-2" },
        furries = new[]
        {
            new ScheduleFurry { name = "Нова", female = true, gen = "Новы", dat = "Нове", acc = "Нову", ins = "Новой",
                activities = new[] { "DJ-сет", "мог-батл" } },
            new ScheduleFurry { name = "Стрей", gen = "Стрея", dat = "Стрею", acc = "Стрея", ins = "Стреем",
                activities = new[] { "встреча гостей", "шоу на сцене" } },
            new ScheduleFurry { name = "Дали", female = true, activities = new[] { "бар", "коктейль-шоу" } },
            new ScheduleFurry { name = "Доли", activities = new[] { "шоу на сцене", "бар" } },
            new ScheduleFurry { name = "Дир", gen = "Дира", dat = "Диру", acc = "Дира", ins = "Диром",
                activities = new[] { "ужин с гостем", "сон" } },
        },
    };
}

public class Schedule
{
    public const string Free = "";

    public readonly ScheduleData Data;
    readonly string[,] cells; // [фурри, время], Free — свободен

    public Schedule(ScheduleData data, string[,] cells)
    {
        Data = data;
        this.cells = cells;
    }

    public int TimeCount => Data.times.Length;
    public int FurryCount => Data.furries.Length;
    public string Time(int t) => Data.times[t];
    public ScheduleFurry Furry(int f) => Data.furries[f];
    public string Activity(int f, int t) => cells[f, t];
    public bool IsFree(int f, int t) => cells[f, t] == Free;
    public bool IsRoom(string activity) => Data.rooms.Contains(activity);
    public bool RoomFree(string room, int t) => Furries.All(f => cells[f, t] != room);

    public IEnumerable<int> Furries => Enumerable.Range(0, FurryCount);
    public IEnumerable<int> Times => Enumerable.Range(0, TimeCount);
    public IEnumerable<int> FreeAt(int t) => Furries.Where(f => IsFree(f, t));
    public IEnumerable<int> BusyAt(int t) => Furries.Where(f => !IsFree(f, t));

    // Что у фурри в этот час: дело или «свободен» / «свободна»
    public string Describe(int f, int t) => IsFree(f, t) ? FreeWord(f) : cells[f, t];
    public string FreeWord(int f) => Furry(f).female ? "свободна" : "свободен";

    public static Schedule Generate(ScheduleData data, Random rng)
    {
        int furries = data.furries.Length, times = data.times.Length;
        for (int attempt = 0; attempt < 500; attempt++)
        {
            var cells = new string[furries, times];
            for (int t = 0; t < times; t++)
            {
                var usedRooms = new HashSet<string>();
                for (int f = 0; f < furries; f++)
                {
                    var acts = data.furries[f].activities;
                    if (rng.NextDouble() < data.freeChance) cells[f, t] = Free;
                    else if (data.rooms.Length > 0 && rng.NextDouble() < data.roomChance)
                    {
                        var room = data.rooms[rng.Next(data.rooms.Length)];
                        cells[f, t] = usedRooms.Add(room) ? room : acts[rng.Next(acts.Length)];
                    }
                    else cells[f, t] = acts[rng.Next(acts.Length)];
                }
            }
            var schedule = new Schedule(data, cells);
            if (schedule.FollowsRules()) return schedule;
        }
        throw new InvalidOperationException("не получилось составить расписание по schedule.json: проверь «freeChance»");
    }

    public bool FollowsRules()
    {
        foreach (int t in Times)
        {
            if (!FreeAt(t).Any() || !BusyAt(t).Any()) return false;
            foreach (var room in Data.rooms)
                if (Furries.Count(f => cells[f, t] == room) > 1) return false;
        }
        foreach (int f in Furries)
            if (Times.All(t => IsFree(f, t)) || Times.All(t => !IsFree(f, t))) return false;
        return true;
    }
}
