using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

// Ручка стартера: клик — рывок на себя, в крайней точке генератор пробует завестись.
// Работает только вблизи генератора (Generator.InReach).
// Ручка тянется вдоль своей оси Z. Клики по 3D-объектам ловит PhysicsRaycaster на камере.
public class GeneratorHandle : MonoBehaviour, IPointerDownHandler
{
    const float PullDistance = 0.25f; // м
    const float PullTime = 0.12f;     // с, рывок
    const float ReturnTime = 0.3f;    // с, возврат

    public Generator generator;

    bool pulling;

    public void OnPointerDown(PointerEventData e)
    {
        if (e.button != PointerEventData.InputButton.Left || pulling) return;
        if (!generator.InReach(Camera.main.transform.position)) return;
        StartCoroutine(Pull());
    }

    IEnumerator Pull()
    {
        pulling = true;
        var start = transform.localPosition;
        var end = start + transform.localRotation * Vector3.forward * PullDistance;

        yield return Move(start, end, PullTime);
        generator.TryStart();
        yield return Move(end, start, ReturnTime);

        pulling = false;
    }

    IEnumerator Move(Vector3 from, Vector3 to, float time)
    {
        for (float t = 0f; t < 1f; t += Time.deltaTime / time)
        {
            transform.localPosition = Vector3.Lerp(from, to, t);
            yield return null;
        }
        transform.localPosition = to;
    }
}
