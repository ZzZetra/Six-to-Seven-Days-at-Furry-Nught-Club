using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// Меню → «Настройки» → «Громкость»: ползунок на каждую группу (GameSounds.GroupNames: общая, музыка мог-батла, фон ночи,
// генератор, остальные звуки), рядом — проценты. Меняется сразу и сохраняется (PlayerPrefs «SSDFNC.Volume.<группа>»).
// Отпустил ползунок — на SampleTime с звучит звук этой группы, чтобы было слышно, что получилось.
public class VolumeSettingsPanel : MonoBehaviour
{
    const float SampleTime = 3f; // с, сколько звучит пример

    public Slider[] sliders; // по VolumeGroup
    public Text[] values;

    static readonly SoundSlot[] Samples =
        { SoundSlot.CallRight, SoundSlot.CallRight, SoundSlot.Ambient, SoundSlot.GeneratorLoop, SoundSlot.DoorClose };

    void Awake()
    {
        for (int i = 0; i < sliders.Length; i++)
        {
            var group = (VolumeGroup)i;
            sliders[i].minValue = 0f;
            sliders[i].maxValue = 1f;
            sliders[i].onValueChanged.AddListener(v =>
            {
                GameSounds.SetVolume(group, v);
                Show(group);
            });
            var trigger = sliders[i].gameObject.AddComponent<UnityEngine.EventSystems.EventTrigger>();
            var up = new UnityEngine.EventSystems.EventTrigger.Entry { eventID = UnityEngine.EventSystems.EventTriggerType.PointerUp };
            up.callback.AddListener(_ => Sample(group));
            trigger.triggers.Add(up);
        }
    }

    void OnEnable()
    {
        for (int i = 0; i < sliders.Length; i++)
        {
            sliders[i].SetValueWithoutNotify(GameSounds.Volume((VolumeGroup)i));
            Show((VolumeGroup)i);
        }
    }

    void OnDisable() => GameSounds.StopPreview();

    void Show(VolumeGroup group) => values[(int)group].text = $"{Mathf.RoundToInt(GameSounds.Volume(group) * 100f)}%";

    // Музыки мог-батла в меню нет — для неё (с её громкостью) и для общей звучит «верный ответ», он короткий
    void Sample(VolumeGroup group)
    {
        GameSounds.Preview(Samples[(int)group], group == VolumeGroup.Music ? VolumeGroup.Music : (VolumeGroup?)null);
        StopAllCoroutines();
        StartCoroutine(StopLater());
    }

    IEnumerator StopLater()
    {
        yield return new WaitForSecondsRealtime(SampleTime);
        GameSounds.StopPreview();
    }
}
