using UnityEngine;

// Фурри в офисе: модель из Blender (Нова, Стрей, Доли, Дали; нет модели — цветная капсула). Её показывает
// NightDirector, когда фурри у двери, на дэнс полу, в проходе Г или у генератора. В остальных комнатах фурри видно
// только на камерах. Имён над фурри нет (пользователь, 2026-11-06). Модель смотрит вдоль +Z объекта.
//   Show(место)  — стоит там лицом к игроку (поворачивается за камерой, куда бы игрок ни ушёл);
//   Show(точка)  — ровно как точка: Стрей у двери выглядывает из-за края проёма (наклонён в него);
//   ShowHere()   — там, где поставил сборщик: рука Доли в вентиляции.
public class FurryAvatar : MonoBehaviour
{
    public void Show(Vector3 position)
    {
        var cam = Camera.main;
        var to = cam != null ? cam.transform.position - position : Vector3.zero;
        to.y = 0f;
        if (to.sqrMagnitude > 0.0001f) transform.SetPositionAndRotation(position, Quaternion.LookRotation(to));
        else transform.position = position;
        ShowHere();
    }

    public void Show(Transform pose)
    {
        transform.SetPositionAndRotation(pose.position, pose.rotation);
        ShowHere();
    }

    public void ShowHere()
    {
        if (!gameObject.activeSelf) gameObject.SetActive(true);
    }

    public void Hide()
    {
        if (gameObject.activeSelf) gameObject.SetActive(false);
    }
}
