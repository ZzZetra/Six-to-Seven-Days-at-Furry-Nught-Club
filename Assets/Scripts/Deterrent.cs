using System;
using UnityEngine;

// Как срабатывает отпугивающий фактор:
// Pulse  — нажал, сработал один раз;
// Hold   — работает, пока держишь;
// Toggle — нажал — включился, нажал ещё раз — выключился.
public enum ActivationMode { Pulse, Hold, Toggle }

// Отпугивающий фактор: дэнс пол, дверь, кнопка звука. Нажатия приходят от PressControl.
// Режим каждый фактор задаёт константой Activation в своём классе.
// Без питания от генератора не работает (если NeedsPower), а включённый — выключается.
// Фурри (когда появятся) смотрят на IsActive и подписываются на Fired / ActiveChanged.
public abstract class Deterrent : MonoBehaviour
{
    public Generator generator; // откуда питание

    protected abstract ActivationMode Mode { get; }
    protected virtual bool NeedsPower => true;

    public bool IsActive { get; private set; }
    public event Action Fired;               // Pulse: сработал
    public event Action<bool> ActiveChanged; // Hold / Toggle: включился (true) или выключился (false)

    public bool HasPower => !NeedsPower || generator == null || generator.IsPowered;
    protected virtual bool CanActivate => HasPower;

    public void Press()
    {
        switch (Mode)
        {
            case ActivationMode.Pulse:
                if (!CanActivate) return;
                OnFired();
                Fired?.Invoke();
                break;
            case ActivationMode.Hold:
                SetActive(true);
                break;
            case ActivationMode.Toggle:
                SetActive(!IsActive);
                break;
        }
    }

    public void Release()
    {
        if (Mode == ActivationMode.Hold) SetActive(false);
    }

    // Выключить можно всегда, включить — только если CanActivate
    protected void SetActive(bool on)
    {
        if (on == IsActive || (on && !CanActivate)) return;
        IsActive = on;
        OnActiveChanged(on);
        ActiveChanged?.Invoke(on);
    }

    protected virtual void OnEnable()
    {
        if (generator != null) generator.PowerChanged += OnPowerChanged;
    }

    protected virtual void OnDisable()
    {
        if (generator != null) generator.PowerChanged -= OnPowerChanged;
    }

    void OnPowerChanged(bool powered)
    {
        if (!powered && NeedsPower) SetActive(false);
    }

    protected virtual void OnFired() { }
    protected virtual void OnActiveChanged(bool on) { }
}
