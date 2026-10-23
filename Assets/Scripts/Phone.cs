using System;
using UnityEngine;
using UnityEngine.EventSystems;

// Телефон на столе: когда звонят — звенит, мигает лампа и дрожит трубка. Клик ЛКМ по телефону — взять трубку.
// Коллайдеры на детях (корпус, трубка), клики ловит PhysicsRaycaster на камере.
public class Phone : MonoBehaviour, IPointerDownHandler
{
    const float BlinkRate = 4f;     // миганий лампы в секунду
    const float ShakeAngle = 6f;    // градусов, дрожь трубки
    const float LiftHeight = 0.05f; // м, трубка приподнята, пока идёт разговор

    public Renderer lamp;
    public Transform handset;
    public AudioSource ring;
    public Color lampOff = new Color(0.2f, 0.05f, 0.05f);
    public Color lampOn = new Color(1f, 0.15f, 0.1f);

    public bool Ringing { get; private set; }
    public event Action PickedUp;

    Vector3 handsetPos;
    Quaternion handsetRot;

    void Awake()
    {
        if (ring.clip == null) ring.clip = Tones.Ring("PhoneRing"); // звук создаётся в коде, в сцене его нет
        handsetPos = handset.localPosition;
        handsetRot = handset.localRotation;
        lamp.material.color = lampOff;
    }

    public void StartRinging()
    {
        Ringing = true;
        ring.Play();
    }

    // Перестать звенеть; offHook — трубка снята (идёт разговор)
    public void StopRinging(bool offHook)
    {
        Ringing = false;
        ring.Stop();
        lamp.material.color = offHook ? lampOn : lampOff;
        handset.localRotation = handsetRot;
        handset.localPosition = handsetPos + (offHook ? Vector3.up * LiftHeight : Vector3.zero);
    }

    // Пауза звонка (мог-батл): звук и мигание стоят
    public void Pause(bool paused)
    {
        if (!Ringing) return;
        if (paused) ring.Pause();
        else ring.UnPause();
    }

    void Update()
    {
        if (!Ringing || !ring.isPlaying) return;
        bool on = Mathf.Repeat(Time.time * BlinkRate, 1f) < 0.5f;
        lamp.material.color = on ? lampOn : lampOff;
        handset.localRotation = handsetRot * Quaternion.Euler(0f, 0f, ShakeAngle * Mathf.Sin(Time.time * 60f));
    }

    public void OnPointerDown(PointerEventData e)
    {
        if (e.button == PointerEventData.InputButton.Left && Ringing) PickedUp?.Invoke();
    }
}
