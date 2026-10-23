using UnityEngine;

// Заглушка фурри в офисе: цветная капсула с именем. Её показывает NightDirector, когда фурри
// у двери, на дэнс полу, в проходе Г или у генератора. В остальных комнатах фурри видно только на камерах.
public class FurryAvatar : MonoBehaviour
{
    static readonly Vector3 Eye = new Vector3(0f, 1.6f, 0f);

    public Transform label; // табличка с именем, поворачивается к глазам в офисе
    public Transform body;  // капсула: её крутит и двигает анимация (атака Новы)

    public void Show(Vector3 position)
    {
        transform.position = position;
        if (!gameObject.activeSelf) gameObject.SetActive(true);
        if (label != null) label.rotation = Quaternion.LookRotation(label.position - Eye);
    }

    public void Hide()
    {
        if (gameObject.activeSelf) gameObject.SetActive(false);
    }
}
