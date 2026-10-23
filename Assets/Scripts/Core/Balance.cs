using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("SSDFNC.Tests.EditMode")]

// Все числа игры в одном месте. Меняются здесь, в коде.
// Другие скрипты берут их отсюда (например Door.MaxClosedTime = Balance.DoorMaxClosedTime).
// Сложность (уровни ИИ фурри) — в Difficulty.cs.
public static class Balance
{
    // ── Смена ──
    public const float ShiftDuration = 90f;          // с; по дизайну 600 (10 минут)

    // ── Генератор ──
    public const float TankSize = 75f;               // бак, ед.
    public const float StartReserve = 145f;          // бензин в канистрах сверх бака
    public const float FuelPerShift = 150f;          // столько сжигает за смену
    public const float PourRate = 15f;               // ед./с, пока держишь «Залить»
    public const float GeneratorMaxHealth = 100f;
    public const float GeneratorReach = 3f;          // м, ближе — можно пользоваться

    // ── Дверь ──
    public const float DoorMaxClosedTime = 20f;      // с, дольше держать закрытой нельзя
    public const float DoorLockTime = 10f;           // с, блокировка после самооткрытия

    // ── Фурри: как часто бросают кубик (с). Разные, чтобы не ходили одновременно ──
    public const float StrayInterval = 4.9f;
    public const float NovaInterval = 6.1f;
    public const float DaliInterval = 5.3f;
    public const float DoliInterval = 4.6f;

    public const float PassageInterval = 2.5f;       // в проходе Г Дали и Доли бегут к генератору быстрее

    // ── У порога: дверь или дэнс пол ──
    public const float DanceFloorGrace = 3f;         // с на дэнс полу, прежде чем может напасть
    public const float DoorGrace = 5f;               // с у двери (к ней ещё надо повернуться)
    public const float DoorLeaveTime = 5f;           // с перед закрытой дверью — и уходит
    public const float DanceScareTime = 1.5f;        // с горящего дэнс пола — и Стрей убегает
    // Закрытая дверь глушит кнопку звука: Доли за закрытой дверью её не слышит.
    // Предложение по балансу (в концепте звук у двери с Доли — смерть всегда). false — как в концепте
    public const bool DoorMufflesSound = true;

    // ── Дали у генератора: глушит или сливает бензин ──
    public const float DaliStallChance = 0.5f;       // доля визитов, когда глушит; остальное — сливает
    public const float StallTime = 3f;               // с глушения, потом генератор глохнет (звук уже не спасёт)
    public const float DrainTime = 2f;               // с слива
    public const float DrainPerSecond = 5f;          // 10 бензина за 2 с, как в концепте

    // ── Доли у генератора: ломает, пока не спугнут звуком ──
    public const float BreakPerSecond = 10f;         // 20 HP за 2 с, как в концепте
    public const float BreakTime = 10f;              // с, дольше не задерживается: 10 с × 10 HP = сломан (концепт: «ломается за 10 с»)

    // ── Дир ──
    public const float DirFoodTimeSlow = 50f;        // с, на сколько хватает корма при уровне 1
    public const float DirFoodTimeFast = 18f;        // ... при уровне 20
    public const float DirGraceTime = 3f;            // с от сигнала «корм кончился» до нападения
    public const float DirFeedHoldTime = 1.5f;       // с держать «Покормить» на камере 7

    // ── Нова ──
    public const float NovaRestAfterBattle = 180f;   // с после проигранного ею мог-батла сидит дома

    // ── Мог-батл с Новой ──
    // Скилл-чеки и музыка — не здесь, а в раскладках Assets/StreamingAssets/MogCharts/*.json
    // (формат: docs/MOG_CHARTS.md). Фазы: Появление → Кружок → Полоска → В стороны → Уход или Смерть.
    public const float MogOutroTime = 2f;            // с, анимация ухода Новы или смерти
    // Запас мога: батл начинается с MogStartLives, попал — +MogLivesPerHit (не выше MogMaxLives),
    // промах — −1. На нуле Нова нападает сразу; дожил до конца хотя бы с 1 — выжил
    public const int MogStartLives = 3;
    public const int MogMaxLives = 4;
    public const int MogLivesPerHit = 1;             // поставь 4 — попадание сразу поднимает до максимума
    public const float MogZoneShrinkPerWin = 0.1f;   // Нова опытнее: зоны уже на 10% после каждой победы игрока
    public const float MogMinZoneScale = 0.5f;       // но не уже половины
}
