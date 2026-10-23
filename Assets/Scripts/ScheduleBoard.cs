using UnityEngine;
using UnityEngine.UI;

// Табличка «Программа на ночь» над столом справа: строки — время, столбцы — фурри. Клетки создаются
// при показе, потому что число фурри и часов берётся из schedule.json. Свободные — зелёным, VIP — золотым.
public class ScheduleBoard : MonoBehaviour
{
    static readonly Color FreeColor = new Color(0.45f, 0.95f, 0.5f);
    static readonly Color RoomColor = new Color(1f, 0.8f, 0.3f);
    static readonly Color HeaderColor = new Color(0.8f, 0.6f, 1f);
    static readonly Color StripeColor = new Color(1f, 1f, 1f, 0.06f);
    const float TimeColumn = 0.14f; // доля ширины под столбец времени

    public RectTransform grid;

    Font font;

    public void Show(Schedule s)
    {
        if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        for (int i = grid.childCount - 1; i >= 0; i--) Destroy(grid.GetChild(i).gameObject);

        float w = grid.rect.width, h = grid.rect.height;
        float timeW = w * TimeColumn, colW = (w - timeW) / s.FurryCount, rowH = h / (s.TimeCount + 1);

        for (int f = 0; f < s.FurryCount; f++)
            Cell(timeW + f * colW, 0f, colW, rowH, s.Furry(f).name, HeaderColor);
        for (int t = 0; t < s.TimeCount; t++)
        {
            float y = (t + 1) * rowH;
            if (t % 2 == 0) Stripe(y, w, rowH);
            Cell(0f, y, timeW, rowH, s.Time(t), HeaderColor);
            for (int f = 0; f < s.FurryCount; f++)
            {
                var color = s.IsFree(f, t) ? FreeColor : s.IsRoom(s.Activity(f, t)) ? RoomColor : Color.white;
                Cell(timeW + f * colW, y, colW, rowH, Capitalize(s.Describe(f, t)), color);
            }
        }
    }

    static string Capitalize(string text) =>
        string.IsNullOrEmpty(text) ? text : char.ToUpper(text[0]) + text.Substring(1);

    // Клетка: x, y — от левого верхнего угла таблицы
    void Cell(float x, float y, float w, float h, string text, Color color)
    {
        var go = new GameObject("Cell", typeof(RectTransform), typeof(Text));
        go.transform.SetParent(grid, false);
        Place((RectTransform)go.transform, x, y, w, h);
        var t = go.GetComponent<Text>();
        t.font = font;
        t.text = text;
        t.color = color;
        t.alignment = TextAnchor.MiddleCenter;
        t.resizeTextForBestFit = true;
        t.resizeTextMinSize = 14;
        t.resizeTextMaxSize = 44;
        t.raycastTarget = false;
    }

    void Stripe(float y, float w, float h)
    {
        var go = new GameObject("Stripe", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(grid, false);
        Place((RectTransform)go.transform, 0f, y, w, h);
        var img = go.GetComponent<Image>();
        img.color = StripeColor;
        img.raycastTarget = false;
    }

    static void Place(RectTransform r, float x, float y, float w, float h)
    {
        r.anchorMin = r.anchorMax = r.pivot = new Vector2(0f, 1f);
        r.anchoredPosition = new Vector2(x, -y);
        r.sizeDelta = new Vector2(w - 6f, h - 4f);
    }
}
