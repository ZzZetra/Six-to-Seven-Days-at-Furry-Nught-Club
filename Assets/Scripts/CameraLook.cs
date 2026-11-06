using UnityEngine;
using UnityEngine.InputSystem;

// Камера стоит в офисе и переключается между тремя направлениями: влево, вперёд, вправо.
// A / стрелка влево — на шаг левее, D / стрелка вправо — на шаг правее.
// Смотришь влево и жмёшь W / стрелку вверх — проходишь в проём и встаёшь у генератора;
// S / стрелка вниз (или D) — назад в офис.
// В офисе игрок сидит в кресле за столом во все стороны (высоту глаз ставит сборщик: на карте — SeatedEyeHeight).
// Когда смотришь вперёд, камера чуть отъезжает назад (откинулся в кресле) и смотрит немного вниз: стол, монитор и
// проход к дэнс полу в кадре, внизу видны подлокотники и помещается окно звонка (CallDialog). У генератора — стоя.
// Начало ночи (пока уходит загрузочный экран): игрок подался вперёд и чуть привстал, голова слегка запрокинута, угол
// обзора шире (экран «плющит»); за IntroTime он «садится» назад в кресло с маленьким рывком через край — будто
// проснулся и дёрнулся. Сзади начинать нельзя — там спинка кресла; голову вниз — видно пустое сиденье.
// Пока идёт появление, команды не принимаются.
public class CameraLook : MonoBehaviour
{
    const float TurnSharpness = 8f;   // больше — резче поворот
    const float MoveTime = 0.35f;     // с, примерное время подхода к генератору (SmoothDamp)
    const float InputAngle = 0.5f;    // с какого остатка угла принимаем следующую команду
    const float InputDistance = 0.02f; // м, то же для перемещения
    const float SnapAngle = 0.02f;    // с какого остатка фиксируем камеру точно
    const float SnapDistance = 0.001f;
    const float LookAngle = 30f;      // насколько можно отклониться от стороны и всё ещё «смотреть на неё»
    public const float SeatedEyeHeight = 0.85f; // м, глаза сидящего в кресле (стол низкий — 0.45; так виден рычаг под ним)
    public const float FieldOfView = 70f;       // градусов по вертикали: шире обычных 60, иначе монитор на столе не влезает
    public const float ForwardPullBack = 0.45f; // м, на сколько камера отъезжает назад, когда смотришь вперёд
    public const float ForwardPitch = 3f;       // градусов вниз, когда смотришь вперёд
    public const float RightPitch = 15f;        // градусов вниз, когда смотришь вправо: там рычаг двери под столом

    // Появление игрока в начале ночи
    const float IntroTime = 0.9f;   // с
    const float IntroForward = 0.05f; // м, насколько подался вперёд (потом «въезжает» назад в кресло)
    const float IntroRise = 0.02f;    // м, и насколько выше (потом садится; минус — ниже)
    const float IntroPitch = -4f;     // градусов наклона головы в начале: минус — вверх, плюс — вниз
    const float IntroFov = 22f;       // градусов: на столько шире угол обзора в начале
    const float IntroJerk = 2.2f;   // сила рывка через край в конце (0 — плавно, без рывка)

    public enum Side { Left, Forward, Right } // номера совпадают с index
    static readonly float[] Yaws = { -90f, 0f, 90f };

    public Transform generatorSpot; // где стоит камера у генератора; не задан — подойти нельзя
    public bool InputLocked;        // ввод  отключён (атака Новы): камеру поворачивают за игрока

    int index = 1;
    Vector3 officePosition;
    Vector3 basePosition; // в офисе или у генератора, без отъезда «вперёд»
    Vector3 lean;         // отъезд назад, когда смотришь вперёд
    Vector3 velocity;
    Quaternion baseRotation; // поворот без появления
    Camera cam;
    float introTime = -1f;   // с начала появления; меньше нуля — ждём, пока уйдёт загрузочный экран
    bool introDone;

    public bool AtGenerator { get; private set; }

    // Смотрит ли камера сейчас в эту сторону офиса (по фактическому повороту, у генератора — нет)
    public bool IsLooking(Side side) =>
        !AtGenerator && Mathf.Abs(Mathf.DeltaAngle(transform.eulerAngles.y, Yaws[(int)side])) < LookAngle;

    Vector3 TargetPosition => AtGenerator ? generatorSpot.position : officePosition;
    public static Vector3 ForwardLean => Vector3.back * ForwardPullBack;
    Vector3 TargetLean => !AtGenerator && index == 1 ? ForwardLean : Vector3.zero;
    Quaternion TargetRotation => AtGenerator ? generatorSpot.rotation
        : Quaternion.Euler(index == 1 ? ForwardPitch : index == 2 ? RightPitch : 0f, Yaws[index], 0);

    // Новую команду принимаем только когда камера уже встала на место
    bool Settled => introDone
                 && Quaternion.Angle(baseRotation, TargetRotation) < InputAngle
                 && Vector3.Distance(basePosition, TargetPosition) < InputDistance;

    void Start()
    {
        if (TryGetComponent(out cam)) cam.fieldOfView = FieldOfView;
        officePosition = transform.position;
        basePosition = officePosition;
        lean = TargetLean;
        baseRotation = TargetRotation;
        ApplyIntro(); // первый же видимый кадр — уже начало появления
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
        if (kb != null && !InputLocked && !GameFlow.InputBlocked)
        {
            if (kb.aKey.wasPressedThisFrame || kb.leftArrowKey.wasPressedThisFrame) StepLeft();
            else if (kb.dKey.wasPressedThisFrame || kb.rightArrowKey.wasPressedThisFrame) StepRight();
            else if (kb.wKey.wasPressedThisFrame || kb.upArrowKey.wasPressedThisFrame) StepForward();
            else if (kb.sKey.wasPressedThisFrame || kb.downArrowKey.wasPressedThisFrame) StepBack();
        }

        // Поворот: экспоненциальное замедление; точная посадка на невидимых глазу 0.02°, чтобы не было рывка в конце
        var rot = TargetRotation;
        baseRotation = Quaternion.Angle(baseRotation, rot) < SnapAngle
            ? rot
            : Quaternion.Slerp(baseRotation, rot, 1f - Mathf.Exp(-TurnSharpness * Time.deltaTime));

        // Перемещение: плавный разгон и торможение
        var pos = TargetPosition;
        if (Vector3.Distance(basePosition, pos) < SnapDistance)
        {
            basePosition = pos;
            velocity = Vector3.zero;
        }
        else basePosition = Vector3.SmoothDamp(basePosition, pos, ref velocity, MoveTime);

        // Отъезд «вперёд» — с той же скоростью, что и поворот, поэтому не задерживает следующую команду
        lean = Vector3.Lerp(lean, TargetLean, 1f - Mathf.Exp(-TurnSharpness * Time.deltaTime));

        if (!introDone)
        {
            if (introTime < 0f && !SceneLoader.Covering) introTime = 0f; // экран загрузки начал уходить
            else if (introTime >= 0f) introTime += Time.deltaTime;
        }
        ApplyIntro();
    }

    // Поза с учётом появления: k = 1 — начало (сзади, ниже, голова опущена, шире обзор), 0 — на месте
    void ApplyIntro()
    {
        float t = introTime < 0f ? 0f : Mathf.Clamp01(introTime / IntroTime);
        if (t >= 1f) introDone = true;
        float k = introDone ? 0f : 1f - EaseOutBack(t);          // с рывком через край
        float fovK = introDone ? 0f : Mathf.Pow(1f - t, 3f);     // угол обзора — плавно
        var yaw = Quaternion.Euler(0f, baseRotation.eulerAngles.y, 0f);
        transform.position = basePosition + lean + yaw * new Vector3(0f, IntroRise, IntroForward) * k;
        transform.rotation = baseRotation * Quaternion.Euler(IntroPitch * k, 0f, 0f);
        if (cam != null) cam.fieldOfView = FieldOfView + IntroFov * fovK;
    }

    static float EaseOutBack(float t)
    {
        float u = t - 1f;
        return 1f + (IntroJerk + 1f) * u * u * u + IntroJerk * u * u;
    }
}
