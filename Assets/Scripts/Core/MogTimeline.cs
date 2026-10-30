using System;
using System.Linq;

// Ход мог-батла по раскладке (MogChart) — без сцены, чтобы гонять в тестах, в проверке и в редакторе раскладок.
// Время t — секунды от начала музыки. Чеки идут по очереди, у каждого вида своё попадание:
//   кружок  — ПРОБЕЛ, пока стрелка в зоне; стрелка прошла зону — промах;
//   полоска — ПРОБЕЛ, пока метка в зоне; полоска пропала (end) — промах;
//   кнопка  — клик по кнопке в окне ±Balance.MogHitWindow вокруг бита; раньше — промах, не успел — промах;
//   стрелка — ждёт взмаха: провести мышью в её сторону в любой момент, пока кольцо не сомкнулось
//             (с запасом Balance.MogHitWindow); не в ту сторону — не считается, ждёт дальше; не успел — промах.
// На экране один чек: следующий показывается, когда этот решён (кольцо у него к тому времени уже сжимается).
// В следующий уже можно попасть (MogCheck.ReadyTime), а этот не решён — промах.
// Запас мога (Lives): начинается с Balance.MogStartLives, попал — плюс (не выше MogMaxLives), промах — минус 1.
// Ноль — батл кончается сразу, Нова нападает. Дошёл до конца хотя бы с 1 — победа.
// Фазы: чеки одного вида подряд — одна фаза; плюс Появление в начале и Уход/Смерть в конце.
public class MogTimeline
{
    public readonly MogChart Chart;
    readonly MogCheck[] checks;
    readonly int[] group;     // номер группы (фазы скилл-чеков) у каждого чека
    readonly float zoneScale;

    public int Index { get; private set; } // текущий или следующий чек
    public int Lives { get; private set; } // запас мога
    public int Hits { get; private set; }
    public int Misses { get; private set; }
    public int GroupCount { get; }
    public event Action<MogCheck, bool> CheckResolved;

    // zoneScale < 1 — зоны и окно попадания уже (Нова опытнее)
    public MogTimeline(MogChart chart, float zoneScale = 1f)
    {
        Chart = chart;
        checks = chart.checks.OrderBy(c => c.StartTime).ToArray();
        this.zoneScale = zoneScale;
        Lives = Balance.MogStartLives;
        group = new int[checks.Length];
        for (int i = 0; i < checks.Length; i++)
            group[i] = i == 0 ? 0 : checks[i].Type == checks[i - 1].Type ? group[i - 1] : group[i - 1] + 1;
        GroupCount = checks.Length == 0 ? 0 : group[checks.Length - 1] + 1;
    }

    public int Count => checks.Length;
    public bool Lost => Lives <= 0;
    public bool Done => Lost || Index >= checks.Length;
    public MogCheck Current => Done ? null : checks[Index];
    public bool PlayerWon => !Lost;
    public bool Active(float t) => !Done && t >= Current.StartTime;
    public float ZoneWidth => Done ? 0f : Current.zone * zoneScale;  // кружок, полоска
    public float HitWindow => Balance.MogHitWindow * zoneScale;     // кнопка, стрелка

    // Полоска: где метка, 0 — левый край, 1 — правый
    public float MarkerPos(float t) => Done ? 0f : Current.BarPos(t);

    // Кружок: угол стрелки, доля оборота от верха по часовой
    public float NeedleAngle(float t) => Done ? 0f : Math.Max(0f, (t - Current.StartTime) * Current.Speed);

    // Кнопка и стрелка: 1 — кольцо только появилось (самое большое), 0 — сомкнулось с кнопкой
    public float RingProgress(float t)
    {
        if (Done) return 0f;
        var c = Current;
        return Math.Max(0f, Math.Min(1f, (c.hit - t) / c.approach));
    }

    // Попадёт ли нажатие (или взмах) прямо сейчас
    public bool InZone(float t)
    {
        if (!Active(t)) return false;
        var c = Current;
        switch (c.Type)
        {
            case MogCheckType.Circle: return Math.Abs(NeedleAngle(t) - c.pos) <= ZoneWidth / 2f;
            case MogCheckType.Bar: return Math.Abs(MarkerPos(t) - c.pos) <= ZoneWidth / 2f;
            case MogCheckType.Arrow: return t <= c.hit + HitWindow;
            default: return Math.Abs(t - c.hit) <= HitWindow;
        }
    }

    // Каждый кадр: промах, если чек кончился, а попадания не было, или в следующий уже можно попасть
    public void Update(float t)
    {
        if (!Active(t)) return;
        var c = Current;
        bool nextReady = Index + 1 < checks.Length && t >= checks[Index + 1].ReadyTime(HitWindow);
        bool over;
        switch (c.Type)
        {
            case MogCheckType.Circle: over = NeedleAngle(t) > c.pos + ZoneWidth / 2f; break;
            case MogCheckType.Bar: over = t >= c.BarEnd; break;
            default: over = t > c.hit + HitWindow; break;
        }
        if (over || nextReady) Resolve(false);
    }

    // ПРОБЕЛ (кружок, полоска) или клик по кнопке. На стрелку не действует
    public void Press(float t)
    {
        if (Active(t) && Current.Type != MogCheckType.Arrow) Resolve(InZone(t));
    }

    // Провёл мышью в сторону dir. Действует только на стрелку: в её сторону и вовремя — попал,
    // в другую — ничего, стрелка ждёт дальше
    public void Swipe(float t, MogDir dir)
    {
        if (Active(t) && Current.Type == MogCheckType.Arrow && dir == Current.Direction && InZone(t)) Resolve(true);
    }

    void Resolve(bool hit)
    {
        var c = Current;
        if (hit)
        {
            Hits++;
            Lives = Math.Min(Balance.MogMaxLives, Lives + Balance.MogLivesPerHit);
        }
        else
        {
            Misses++;
            Lives--;
        }
        Index++;
        CheckResolved?.Invoke(c, hit);
    }

    // Фаза текущего чека (1 — Появление, потом группы, последняя — Уход/Смерть) и сколько фаз всего
    public int Phase => Done ? PhaseCount : group[Index] + 2;
    public int PhaseCount => GroupCount + 2;

    // Какой это по счёту чек в своей группе и сколько в ней: «Полоска 2/5»
    public (int number, int total) ProgressInGroup()
    {
        if (Done) return (0, 0);
        int g = group[Index];
        int first = Array.IndexOf(group, g);
        return (Index - first + 1, group.Count(x => x == g));
    }

    public static string PhaseName(MogCheckType type)
    {
        switch (type)
        {
            case MogCheckType.Circle: return "Кружок";
            case MogCheckType.Button: return "Кнопка";
            case MogCheckType.Arrow: return "Стрелки";
            default: return "Полоска";
        }
    }
}
