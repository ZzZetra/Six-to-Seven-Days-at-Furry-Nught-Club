using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Пауза → «Звуки»: у каждого события игры свой звук (GameSounds). ◄ ► — сменить звук (новый сразу звучит и сразу
// играет в игре), ♪ — послушать ещё раз, «По умолчанию» — вернуть всё как было. Выбор сохраняется между запусками.
// Строки строятся при открытии из GameSounds.Slots и файлов StreamingAssets/Sounds — новое событие или новый файл
// видны без пересборки сцены. Сборщик офиса кладёт этот объект (растянутый на весь экран) в затемнение паузы.
public class SoundSettingsPanel : MonoBehaviour
{
    const float CardWidth = 1500f, CardHeight = 960f;
    const float RowHeight = 54f, RowWidth = 1400f;
    const int TitleFont = 30, ValueFont = 30, ButtonFont = 32;

    static readonly Color CardColor = new Color(0.1f, 0.07f, 0.15f, 0.98f);
    static readonly Color RowColor = new Color(1f, 1f, 1f, 0.05f);
    static readonly Color TitleColor = new Color(0.85f, 0.8f, 0.95f);
    static readonly Color ValueColor = new Color(1f, 0.77f, 0.08f);
    static readonly Color OffColor = new Color(0.55f, 0.5f, 0.6f);

    public event Action Closed;

    class Row
    {
        public SoundSlot slot;
        public Text value;
    }

    readonly List<Row> rows = new List<Row>();
    RectTransform content;
    ScrollRect scroll;
    Font font;

    public bool IsOpen => gameObject.activeSelf;

    public void Open()
    {
        if (content == null) Build();
        gameObject.SetActive(true);
        foreach (var row in rows) Show(row);
        scroll.verticalNormalizedPosition = 1f;
    }

    public void Close()
    {
        GameSounds.StopPreview();
        gameObject.SetActive(false);
        Closed?.Invoke();
    }

    void Step(Row row, int delta)
    {
        var options = GameSounds.Options(row.slot);
        int i = Mathf.Max(0, options.IndexOf(GameSounds.Choice(row.slot)));
        i = (i + delta + options.Count) % options.Count;
        GameSounds.SetChoice(row.slot, options[i]);
        Show(row);
        GameSounds.Preview(row.slot);
    }

    void Show(Row row)
    {
        string choice = GameSounds.Choice(row.slot);
        row.value.text = GameSounds.ChoiceName(choice);
        row.value.color = choice == GameSounds.None ? OffColor : ValueColor;
    }

    void ResetAll()
    {
        GameSounds.ResetAll();
        GameSounds.StopPreview();
        foreach (var row in rows) Show(row);
    }

    // ── Окно ──

    void Build()
    {
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        var root = (RectTransform)transform;

        var card = NewImage("Card", root, CardColor);
        Place(card.rectTransform, Vector2.zero, new Vector2(CardWidth, CardHeight));

        var title = NewText("Title", card.transform, 64, new Color(0.8f, 0.6f, 1f));
        title.text = "ЗВУКИ";
        Place(title.rectTransform, new Vector2(0, 420), new Vector2(1000, 90));
        var hint = NewText("Hint", card.transform, 24, OffColor);
        hint.text = "◄ ► — сменить звук (сразу слышно и сразу в игре), ♪ — послушать ещё раз. " +
                    "Свои звуки — в папку StreamingAssets/Sounds.";
        Place(hint.rectTransform, new Vector2(0, 362), new Vector2(1400, 40));

        // Прокручиваемый список событий
        var view = NewImage("View", card.transform, new Color(0f, 0f, 0f, 0.25f));
        Place(view.rectTransform, new Vector2(0, -10), new Vector2(RowWidth + 40, 680));
        view.gameObject.AddComponent<RectMask2D>();
        content = new GameObject("Rows", typeof(RectTransform)).GetComponent<RectTransform>();
        content.SetParent(view.transform, false);
        content.anchorMin = new Vector2(0.5f, 1f);
        content.anchorMax = new Vector2(0.5f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.sizeDelta = new Vector2(RowWidth, GameSounds.Slots.Length * RowHeight + 20);
        content.anchoredPosition = Vector2.zero;
        scroll = view.gameObject.AddComponent<ScrollRect>();
        scroll.viewport = view.rectTransform;
        scroll.content = content;
        scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 40f;

        for (int i = 0; i < GameSounds.Slots.Length; i++) BuildRow((SoundSlot)i, i);

        NewButton("Reset", card.transform, "По умолчанию", new Vector2(-260, -415), new Vector2(420, 80), 34).onClick.AddListener(ResetAll);
        NewButton("Back", card.transform, "Назад", new Vector2(260, -415), new Vector2(420, 80), 34).onClick.AddListener(Close);
    }

    void BuildRow(SoundSlot slot, int index)
    {
        var bg = NewImage($"Row{slot}", content, index % 2 == 0 ? RowColor : Color.clear);
        var rt = bg.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.sizeDelta = new Vector2(RowWidth, RowHeight);
        rt.anchoredPosition = new Vector2(0, -10 - index * RowHeight);

        var row = new Row { slot = slot };
        float left = -RowWidth / 2f;
        var name = NewText("Name", bg.transform, TitleFont, TitleColor);
        name.text = GameSounds.Info(slot).Title;
        name.alignment = TextAnchor.MiddleLeft;
        Place(name.rectTransform, new Vector2(left + 20 + 290, 0), new Vector2(580, RowHeight));

        NewButton("Prev", bg.transform, "◄", new Vector2(left + 660, 0), new Vector2(64, 44), ButtonFont).onClick.AddListener(() => Step(row, -1));
        row.value = NewText("Value", bg.transform, ValueFont, ValueColor);
        Place(row.value.rectTransform, new Vector2(left + 960, 0), new Vector2(520, RowHeight));
        NewButton("Next", bg.transform, "►", new Vector2(left + 1260, 0), new Vector2(64, 44), ButtonFont).onClick.AddListener(() => Step(row, +1));
        NewButton("Play", bg.transform, "♪", new Vector2(left + 1345, 0), new Vector2(70, 44), ButtonFont).onClick.AddListener(() => GameSounds.Preview(slot));
        rows.Add(row);
    }

    Image NewImage(string name, Transform parent, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.color = color;
        return img;
    }

    Text NewText(string name, Transform parent, int size, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);
        var t = go.GetComponent<Text>();
        t.font = font;
        t.fontSize = size;
        t.color = color;
        t.alignment = TextAnchor.MiddleCenter;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow; // Text молча теряет строку, если она не влезла по высоте
        t.raycastTarget = false;
        return t;
    }

    // Как кнопки меню (MainMenuBuilder.NewButton): белая картинка, цвет задаёт ColorTint
    Button NewButton(string name, Transform parent, string label, Vector2 pos, Vector2 size, int fontSize)
    {
        var img = NewImage(name, parent, Color.white);
        Place(img.rectTransform, pos, size);
        var btn = img.gameObject.AddComponent<Button>();
        var colors = btn.colors;
        colors.normalColor = new Color(0.18f, 0.12f, 0.24f);
        colors.highlightedColor = new Color(0.45f, 0.25f, 0.6f);
        colors.pressedColor = new Color(0.3f, 0.15f, 0.4f);
        colors.selectedColor = colors.normalColor;
        colors.disabledColor = new Color(0.11f, 0.08f, 0.14f);
        btn.colors = colors;
        var text = NewText("Label", img.transform, fontSize, Color.white);
        var rt = text.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        text.text = label;
        return btn;
    }

    static void Place(RectTransform rt, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        rt.anchoredPosition = pos;
    }
}
