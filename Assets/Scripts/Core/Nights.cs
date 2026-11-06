using System;

// Одна ночь прохождения: кто из фурри участвует и с какими уровнями, как часто сбоит монитор, сколько звонков
public class NightConfig
{
    public readonly int Number;
    public readonly int[] Levels;                       // Дир, Стрей, Нова, Дали, Доли (как в FurryId); 0 — не участвует
    public readonly float MonitorRebootChancePerMinute;
    public readonly int CallsMin, CallsMax;             // звонков клиентов за ночь; звонки начальства — сверх них

    public NightConfig(int number, int[] levels, float monitorReboot, int callsMin, int callsMax)
    {
        Number = number;
        Levels = levels;
        MonitorRebootChancePerMinute = monitorReboot;
        CallsMin = callsMin;
        CallsMax = callsMax;
    }
}

// Ночи-уровни, как во FNAF (пользователь, 2026-11-05): 7 ночей по 6 минут (Balance.ShiftDuration). Ночь
// открывается, когда пройдена предыдущая; 7-я — ещё и если на счету не меньше Balance.Night7Money.
// С каждой ночью сложнее: больше фурри и выше уровни, с 3-й ночи монитор сбоит чаще, звонков больше (до 9).
// Уровни растут и внутри ночи: +Difficulty.RampPerQuarter каждую четверть смены.
// «Своя ночь» (вкладка меню) — не отсюда: уровни из NightSettings, сбои и звонки — из Balance.
public static class Nights
{
    public const int Count = 7;

    // ── Меняй здесь ──
    // Уровни: Дир, Стрей, Нова, Дали, Доли. Проверены симуляцией — docs/BALANCE_REPORT.md, «Ночи»
    static readonly NightConfig[] all =
    {
        //              ночь  Дир Стр Нов Дал Дол  сбой монитора / мин                  звонков
        new NightConfig(1, new[] { 0, 2, 1, 2, 0 }, Balance.MonitorRebootChancePerMinute, 3, 4), // Стрей, Дали, Нова
        new NightConfig(2, new[] { 3, 3, 1, 4, 0 }, Balance.MonitorRebootChancePerMinute, 4, 5), // + Дир
        // 3-я — новых нет, зато Стрей и Дали злее (пользователь: «чтобы не скучали»); на 4-й пришла Доли — они полегче
        new NightConfig(3, new[] { 5, 7, 1, 7, 0 }, Balance.MonitorRebootChanceHard,      4, 6), // монитор сбоит чаще
        new NightConfig(4, new[] { 5, 5, 2, 5, 4 }, Balance.MonitorRebootChanceHard,      5, 7), // все пятеро
        new NightConfig(5, new[] { 6, 7, 2, 7, 4 }, Balance.MonitorRebootChanceHard,      6, 8),
        new NightConfig(6, new[] { 8, 8, 3, 8, 6 }, Balance.MonitorRebootChanceHard,      7, 9),
        new NightConfig(7, new[] { 11, 10, 4, 10, 9 }, Balance.MonitorRebootChanceHard,  8, 9),
    };

    // Ночь по номеру 1–Count (номер за пределами — ближайшая)
    public static NightConfig Get(int number) => all[Math.Max(1, Math.Min(Count, number)) - 1];
}
