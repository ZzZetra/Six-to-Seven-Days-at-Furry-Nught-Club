using UnityEngine;
using UnityEngine.InputSystem;

// Камера стоит в офисе и переключается между тремя направлениями: влево, вперёд, вправо.
// A / стрелка влево — на шаг левее, D / стрелка вправо — на шаг правее.
// Смотришь влево и жмёшь W / стрелку вверх — проходишь в проём и встаёшь у генератора;
// S / стрелка вниз (или D) — назад в офис.
public class CameraLook : MonoBehaviour
{
    const float TurnSharpness = 8f;   // больше — резче поворот
    const float MoveTime = 0.35f;     // с, примерное время подхода к генератору (SmoothDamp)
    const float InputAngle = 0.5f;    // с какого остатка угла принимаем следующую команду
    const float InputDistance = 0.02f; // м, то же для перемещения
    const float SnapAngle = 0.02f;    // с какого остатка фиксируем камеру точно
    const float SnapDistance = 0.001f;
    const float LookAngle = 30f;      // насколько можно отклониться от стороны и всё ещё «смотреть на неё»

    public enum Side { Left, Forward, Right } // номера совпадают с index
    static readonly float[] Yaws = { -90f, 0f, 90f };

    public Transform generatorSpot; // где стоит камера у генератора; не задан — подойти нельзя
    public bool InputLocked;        // ввод отключён (атака Новы): камеру поворачивают за игрока

    int index = 1;
    Vector3 officePosition;
    Vector3 velocity;

    public bool AtGenerator { get; private set; }

    // Смотрит ли камера сейчас в эту сторону офиса (по фактическому повороту, у генератора — нет)
    public bool IsLooking(Side side) =>
        !AtGenerator && Mathf.Abs(Mathf.DeltaAngle(transform.eulerAngles.y, Yaws[(int)side])) < LookAngle;

    Vector3 TargetPosition => AtGenerator ? generatorSpot.position : officePosition;
    Quaternion TargetRotation => AtGenerator ? generatorSpot.rotation : Quaternion.Euler(0, Yaws[index], 0);

    // Новую команду принимаем только когда камера уже встала на место
    bool Settled => Quaternion.Angle(transform.rotation, TargetRotation) < InputAngle
                 && Vector3.Distance(transform.position, TargetPosition) < InputDistance;

    void Start()
    {
        officePosition = transform.position;
        transform.rotation = TargetRotation;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void StepLeft()
    {
        if (!Settled || AtGenerator) return;
        index = Mathf.Max(0, index - 1);
    }

    public void StepRight()
    {
        if (!Settled) return;
        if (AtGenerator) AtGenerator = false; // назад в офис, лицом к генератору
        else index = Mathf.Min(Yaws.Length - 1, index + 1);
    }

    // Пройти в комнату, на которую смотришь. Пока такая одна — генератор слева
    public void StepForward()
    {
        if (!Settled || AtGenerator) return;
        if (index == 0 && generatorSpot != null) AtGenerator = true;
    }

    // Повернуть к дэнс полу (вперёд). От генератора — сначала назад в офис
    public void FaceForward()
    {
        AtGenerator = false;
        index = 1;
    }

    public void StepBack()
    {
        if (!Settled || !AtGenerator) return;
        AtGenerator = false;
    }

    void Update()
    {
        var kb = Keyboard.current;
        if (kb != null && !InputLocked)
        {
            if (kb.aKey.wasPressedThisFrame || kb.leftArrowKey.wasPressedThisFrame) StepLeft();
            else if (kb.dKey.wasPressedThisFrame || kb.rightArrowKey.wasPressedThisFrame) StepRight();
            else if (kb.wKey.wasPressedThisFrame || kb.upArrowKey.wasPressedThisFrame) StepForward();
            else if (kb.sKey.wasPressedThisFrame || kb.downArrowKey.wasPressedThisFrame) StepBack();
        }

        // Поворот: экспоненциальное замедление; точная посадка на невидимых глазу 0.02°, чтобы не было рывка в конце
        var rot = TargetRotation;
        transform.rotation = Quaternion.Angle(transform.rotation, rot) < SnapAngle
            ? rot
            : Quaternion.Slerp(transform.rotation, rot, 1f - Mathf.Exp(-TurnSharpness * Time.deltaTime));

        // Перемещение: плавный разгон и торможение
        var pos = TargetPosition;
        if (Vector3.Distance(transform.position, pos) < SnapDistance)
        {
            transform.position = pos;
            velocity = Vector3.zero;
        }
        else transform.position = Vector3.SmoothDamp(transform.position, pos, ref velocity, MoveTime);
    }
}
