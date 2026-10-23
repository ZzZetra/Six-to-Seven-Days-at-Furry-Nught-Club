using UnityEngine;
using UnityEngine.EventSystems;

// Кнопка, которую держат: IsHeld = true, пока левая кнопка мыши зажата на ней.
// Вешается рядом с обычным Button, тот отвечает за подсветку.
public class HoldButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    public bool IsHeld { get; private set; }

    public void OnPointerDown(PointerEventData e)
    {
        if (e.button == PointerEventData.InputButton.Left) IsHeld = true;
    }

    public void OnPointerUp(PointerEventData e) => IsHeld = false;
    public void OnPointerExit(PointerEventData e) => IsHeld = false;
    void OnDisable() => IsHeld = false;
}
