using UnityEngine;
using UnityEngine.InputSystem;

// Пробел — действие в ту сторону, куда смотришь (CameraLook.IsLooking):
// вперёд — дэнс пол: горит, пока держишь пробел и смотришь вперёд; отвернулся — гаснет;
// вправо — рычаг двери: каждое нажатие закрывает или открывает.
// Влево и у генератора пробел ничего не делает. Мышью по кнопке и рычагу тоже можно.
public class SpaceAction : MonoBehaviour
{
    public CameraLook look;
    public DanceFloor danceFloor;
    public Door door;

    bool holdingDance;

    void Update()
    {
        var kb = Keyboard.current;
        bool held = kb != null && kb.spaceKey.isPressed;
        bool pressed = kb != null && kb.spaceKey.wasPressedThisFrame;

        bool dance = held && look.IsLooking(CameraLook.Side.Forward);
        if (dance != holdingDance)
        {
            holdingDance = dance;
            if (dance) danceFloor.Press();
            else danceFloor.Release();
        }

        if (pressed && look.IsLooking(CameraLook.Side.Right)) door.Press();
    }
}
