using UnityEngine;
using UnityEngine.UI;

// Дверь в правом проёме (в коридор 2). Рычаг: нажал — закрылась, нажал ещё раз — открылась (Toggle).
// Держать закрытой дольше MaxClosedTime нельзя: откроется сама и заблокируется на LockTime.
// Нужен генератор: без питания рычаг не работает, а закрытая дверь открывается.
public class Door : Deterrent
{
    public const ActivationMode Activation = ActivationMode.Toggle;
    public const float MaxClosedTime = Balance.DoorMaxClosedTime; // меняется в Balance.cs
    public const float LockTime = Balance.DoorLockTime;
    const float MoveSharpness = 12f;        // скорость створки и рычага

    static readonly Quaternion LeverOpen = Quaternion.Euler(0f, 0f, 30f);    // рычаг вверх
    static readonly Quaternion LeverClosed = Quaternion.Euler(0f, 0f, 150f); // рычаг вниз
    static readonly Color OpenColor = new Color(0.2f, 0.85f, 0.3f);
    static readonly Color ClosedColor = new Color(0.9f, 0.15f, 0.15f);
    static readonly Color LockedColor = new Color(1f, 0.55f, 0.1f);
    static readonly Color NoPowerColor = new Color(0.4f, 0.4f, 0.4f);

    public Transform slab;      // створка
    public Vector3 slabClosed;  // её локальная позиция, когда дверь закрыта
    public Vector3 slabOpen;    // и когда открыта (спрятана в стене)
    public Transform lever;     // ось рычага
    public Image lamp;
    public Text label;

    protected override ActivationMode Mode => Activation;

    public bool IsClosed => IsActive;
    public float ClosedFor { get; private set; }
    public float LockedFor { get; private set; }
    public bool IsLocked => LockedFor > 0f;

    protected override bool CanActivate => base.CanActivate && !IsLocked;

    void Update()
    {
        if (IsClosed)
        {
            ClosedFor += Time.deltaTime;
            if (ClosedFor >= MaxClosedTime)
            {
                SetActive(false);
                LockedFor = LockTime;
                Debug.Log($"[SSDFNC] Дверь передержали: открылась сама, блокировка {LockTime} с");
            }
        }
        else if (IsLocked)
            LockedFor = Mathf.Max(0f, LockedFor - Time.deltaTime);

        float k = 1f - Mathf.Exp(-MoveSharpness * Time.deltaTime);
        slab.localPosition = Vector3.Lerp(slab.localPosition, IsClosed ? slabClosed : slabOpen, k);
        lever.localRotation = Quaternion.Slerp(lever.localRotation, IsClosed ? LeverClosed : LeverOpen, k);

        var (text, color) =
            !HasPower ? ("НЕТ ПИТАНИЯ", NoPowerColor) :
            IsClosed ? ($"ЗАКРЫТО {Mathf.CeilToInt(MaxClosedTime - ClosedFor)}", ClosedColor) :
            IsLocked ? ($"БЛОК {Mathf.CeilToInt(LockedFor)}", LockedColor) :
            ("ОТКРЫТО", OpenColor);
        label.text = text;
        label.color = color;
        lamp.color = color;
    }

    protected override void OnActiveChanged(bool closed)
    {
        ClosedFor = 0f;
        Debug.Log(closed ? "[SSDFNC] Дверь закрыта" : "[SSDFNC] Дверь открыта");
    }
}
