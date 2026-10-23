public enum DifficultyPreset { Easy, Normal, Hard, Custom }

// Сложность. Уровни ИИ 0–20, как во FNAF: каждые Interval секунд фурри бросает кубик 1–20
// и делает ход, если выпало не больше его уровня. 0 — фурри не участвует, 20 — ходит всегда.
// У Дира уровень задаёт, как быстро он съедает корм.
public static class Difficulty
{
    // ── Меняй здесь ──
    public static readonly DifficultyPreset Preset = DifficultyPreset.Normal;

    // Свои уровни для Preset = Custom (как Custom Night во FNAF). Порядок: Дир, Стрей, Нова, Дали, Доли
    public static readonly int[] Custom = { 6, 5, 1, 6, 4 };

    // +RampPerQuarter к уровню каждую четверть смены (во FNAF уровень растёт в 2, 3 и 4 часа ночи)
    public const int RampPerQuarter = 1;

    // Порядок: Дир, Стрей, Нова, Дали, Доли (как в FurryId).
    // Подобраны симуляцией (меню SSDFNC → Balance Report) под смену 10 минут, см. docs/FURRY_AI.md
    public static int[] Levels(DifficultyPreset preset)
    {
        switch (preset)
        {
            case DifficultyPreset.Easy: return new[] { 3, 2, 1, 3, 2 };
            case DifficultyPreset.Normal: return new[] { 6, 5, 1, 6, 4 };
            case DifficultyPreset.Hard: return new[] { 10, 9, 3, 10, 8 };
            default: return (int[])Custom.Clone();
        }
    }
}
