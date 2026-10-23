using System;
using System.Linq;

// Ход мог-батла по раскладке (MogChart) — без сцены, чтобы гонять в тестах и в будущем приложении настройки.
// Время t — секунды от начала музыки. Чеки идут по очереди:
//   метка стартует от левого края в start и ровно в hit оказывается в центре зоны;
//   ПРОБЕЛ в зоне — попал, мимо — промах;
//   не нажал, пока метка не дошла до края, или уже начался следующий чек — тоже промах.
// Запас мога (Lives): начинается с Balance.MogStartLives, попал — плюс (не выше MogMaxLives), промах — минус 1.
// Ноль — батл кончается сразу, Нова нападает. Дошёл до конца хотя бы с 1 — победа.
public class MogTimeline
{
    public readonly MogChart Chart;
    readonly MogCheck[] checks;
    readonly float zoneScale;

    public int Index { get; private set; } // текущий или следующий чек
    public int Lives { get; private set; } // запас мога
    public int Hits { get; private set; }
    public int Misses { get; private set; }
    public event Action<MogCheck, bool> CheckResolved;

    // zoneScale < 1 — зоны уже (Нова опытнее)
    public MogTimeline(MogChart chart, float zoneScale = 1f)
    {
        Chart = chart;
        checks = chart.checks.OrderBy(c => c.start).ToArray();
        this.zoneScale = zoneScale;
        Lives = Balance.MogStartLives;
    }

    public int Count => checks.Length;
    public bool Lost => Lives <= 0;
    public bool Done => Lost || Index >= checks.Length;
    public MogCheck Current => Done ? null : checks[Index];
    public bool PlayerWon => !Lost;
    public bool Active(float t) => !Done && t >= Current.start;
    public float ZoneWidth => Done ? 0f : Current.zone * zoneScale;

    // Где метка: 0 — левый край, 1 — правый
    public float MarkerPos(float t)
    {
        if (Done) return 0f;
        var c = Current;
        return Math.Max(0f, (t - c.start) / (c.hit - c.start) * c.pos);
    }

    public bool InZone(float t) => Active(t) && Math.Abs(MarkerPos(t) - Current.pos) <= ZoneWidth / 2f;

    // Каждый кадр: промах, если метка ушла за край или пора следующему чеку
    public void Update(float t)
    {
        if (!Active(t)) return;
        bool nextStarted = Index + 1 < checks.Length && t >= checks[Index + 1].start;
        if (MarkerPos(t) >= 1f || nextStarted) Resolve(false);
    }

    public void Press(float t)
    {
        if (Active(t)) Resolve(InZone(t));
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

    // Какой это по счёту чек своего типа и сколько их всего в раскладке: «Полоска 2/5»
    public (int number, int total) ProgressInType()
    {
        if (Done) return (0, 0);
        var type = Current.Type;
        int number = checks.Take(Index + 1).Count(c => c.Type == type);
        int total = checks.Count(c => c.Type == type);
        return (number, total);
    }

    public static int PhaseNumber(MogCheckType type) => type == MogCheckType.Circle ? 2 : type == MogCheckType.Bar ? 3 : 4;

    public static string PhaseName(MogCheckType type) =>
        type == MogCheckType.Circle ? "Кружок" : type == MogCheckType.Bar ? "Полоска" : "В стороны";
}
