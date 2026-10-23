using UnityEngine;
using UnityEngine.EventSystems;

// 3D-кнопка или рычаг: нажатие и отпускание левой кнопки мыши передаёт отпугивающему фактору.
// Клики по 3D-объектам ловит PhysicsRaycaster на камере, коллайдер может быть на детях.
public class PressControl : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    const float PressDepth = 0.008f; // м, насколько утапливается шляпка кнопки

    public Deterrent target;
    public Transform cap; // что утапливать при нажатии; не задано — ничего

    bool pressed;

    public void OnPointerDown(PointerEventData e)
    {
        if (e.button != PointerEventData.InputButton.Left || pressed) return;
        pressed = true;
        if (cap != null) cap.localPosition += Vector3.down * PressDepth;
        target.Press();
    }

    public void OnPointerUp(PointerEventData e)
    {
        if (!pressed) return;
        pressed = false;
        if (cap != null) cap.localPosition += Vector3.up * PressDepth;
        target.Release();
    }
}
