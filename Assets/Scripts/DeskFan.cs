using UnityEngine;

// Вентилятор на столе ресепшена (из карты): лопасти крутятся вокруг своей оси. Объект — ось вентилятора (сборщик
// ставит его в центр лопастей, локальная Z — куда дует), лопасти из карты — внутри. На паузе стоит (Time.deltaTime = 0).
public class DeskFan : MonoBehaviour
{
    const float Speed = 1080f; // градусов в секунду (3 оборота)

    void Update() => transform.Rotate(0f, 0f, Speed * Time.deltaTime, Space.Self);
}
