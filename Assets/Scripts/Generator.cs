using System;
using UnityEngine;
using UnityEngine.InputSystem;

// Генератор: бак с бензином, здоровье и запуск. Питает всё, кроме кнопки звука.
// Работает — жжёт бензин. Бензин кончился или генератор заглушили — встаёт, заводится ручкой.
// Здоровье на нуле — сломан до конца смены, починки нет.
// Ручкой и кнопкой «Залить» можно пользоваться только вблизи (ReachDistance), то есть подойдя к генератору.
// Для проверки (в редакторе и dev-сборке): F6 — заглушить, F7 — минус 20 HP, F8 — слить 10 бензина.
public class Generator : MonoBehaviour
{
    // Настройки генератора — меняются в Balance.cs
    public const float TankSize = Balance.TankSize;
    public const float StartReserve = Balance.StartReserve;
    public const float FuelPerShift = Balance.FuelPerShift;
    public const float PourRate = Balance.PourRate;
    public const float MaxHealth = Balance.GeneratorMaxHealth;
    public const float ReachDistance = Balance.GeneratorReach;

    public enum State { Running, Stalled, Broken }

    public State Current { get; private set; } = State.Running;
    public float Fuel { get; private set; } = TankSize;
    public float Reserve { get; private set; } = StartReserve;
    public float Health { get; private set; } = MaxHealth;
    public bool IsPowered => Current == State.Running;
    public bool Paused; // идёт мог-батл: бензин не тратится

    // Достаёт ли игрок до генератора. Точка генератора — центр основания корпуса
    public bool InReach(Vector3 eye) => Vector3.Distance(eye, transform.position) <= ReachDistance;

    // Питание появилось (true) или пропало (false)
    public event Action<bool> PowerChanged;

    // Расход в секунду: за смену уходит ровно FuelPerShift
    static float BurnRate => FuelPerShift / ShiftTimer.Duration;

    void Update()
    {
        if (Current == State.Running && !Paused)
            Drain(BurnRate * Time.deltaTime);

        var kb = Keyboard.current;
        if (Debug.isDebugBuild && kb != null)
        {
            if (kb.f6Key.wasPressedThisFrame) Stall();
            if (kb.f7Key.wasPressedThisFrame) Damage(20f);
            if (kb.f8Key.wasPressedThisFrame) Drain(10f);
        }
    }

    // Перелить бензин из канистр в бак
    public void Pour(float deltaTime)
    {
        float amount = Mathf.Min(PourRate * deltaTime, Reserve, TankSize - Fuel);
        if (amount <= 0f) return;
        Reserve -= amount;
        Fuel += amount;
    }

    // Убрать бензин из бака: обычный расход или слив фурри. Пустой бак — генератор глохнет.
    public void Drain(float amount)
    {
        Fuel = Mathf.Max(0f, Fuel - amount);
        if (Fuel <= 0f) Stall();
    }

    // Заглушить: генератор встаёт, нужен перезапуск ручкой
    public void Stall()
    {
        if (Current != State.Running) return;
        Current = State.Stalled;
        Debug.Log("[SSDFNC] Генератор заглох");
        PowerChanged?.Invoke(false);
    }

    public void Damage(float amount)
    {
        if (Current == State.Broken) return;
        Health = Mathf.Max(0f, Health - amount);
        if (Health > 0f) return;

        bool wasRunning = Current == State.Running;
        Current = State.Broken;
        Debug.Log("[SSDFNC] Генератор сломан");
        if (wasRunning) PowerChanged?.Invoke(false);
    }

    // Рывок ручки: заводится, только если заглох и в баке есть бензин
    public bool TryStart()
    {
        if (Current != State.Stalled || Fuel <= 0f) return false;
        Current = State.Running;
        Debug.Log("[SSDFNC] Генератор завёлся");
        PowerChanged?.Invoke(true);
        return true;
    }
}
