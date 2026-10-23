using System;
using UnityEngine;
using UnityEngine.UI;

// Окно звонка внизу экрана (под столом, когда смотришь вперёд): кто звонит, реплика, подсказка
// или варианты ответа. Видно только когда смотришь вперёд (SetVisible), но разговор при этом не сбрасывается.
public class CallDialog : MonoBehaviour
{
    const float OptionSpacing = 490f; // px между центрами кнопок ответа

    public CanvasGroup group;
    public Text caller;
    public Text line;
    public Text hint;
    public Button[] options;

    public bool Open { get; private set; }
    public bool Visible => group.alpha > 0f;
    public event Action<int> Chosen;

    Text[] optionTexts;

    void Awake()
    {
        optionTexts = new Text[options.Length];
        for (int i = 0; i < options.Length; i++)
        {
            int index = i;
            optionTexts[i] = options[i].GetComponentInChildren<Text>();
            options[i].onClick.AddListener(() => Chosen?.Invoke(index));
        }
        Close();
    }

    public void ShowLine(string who, string text, string hintText, Color color)
    {
        Open = true;
        caller.text = who;
        line.text = text;
        line.color = color;
        hint.text = hintText ?? "";
        hint.gameObject.SetActive(!string.IsNullOrEmpty(hintText));
        foreach (var b in options) b.gameObject.SetActive(false);
    }

    public void ShowOptions(string[] texts)
    {
        hint.gameObject.SetActive(false);
        for (int i = 0; i < options.Length; i++)
        {
            bool on = i < texts.Length;
            options[i].gameObject.SetActive(on);
            if (!on) continue;
            optionTexts[i].text = $"{i + 1}. {texts[i]}";
            var rt = (RectTransform)options[i].transform;
            rt.anchoredPosition = new Vector2((i - (texts.Length - 1) / 2f) * OptionSpacing, rt.anchoredPosition.y);
        }
    }

    public void Close()
    {
        Open = false;
        SetVisible(false);
    }

    // Видно, только если разговор идёт и игрок смотрит вперёд
    public void SetVisible(bool visible)
    {
        bool on = Open && visible;
        group.alpha = on ? 1f : 0f;
        group.interactable = on;
        group.blocksRaycasts = on;
    }
}
