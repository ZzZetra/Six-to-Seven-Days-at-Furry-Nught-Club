using UnityEngine;
using UnityEngine.UI;

// Окно мог-батла (MogBattle) на оверлейном канвасе: полоска, кружок, кнопка, стрелка, запас мога и надпись
// фазы в правом верхнем углу. Одно и то же окно в офисе (атака Новы) и в главном меню (проверка скилл-чеков).
public static class MogBattleBuilder
{
    static readonly Color Purple = new Color(0.8f, 0.6f, 1f);
    static readonly Color Green = new Color(0.2f, 0.85f, 0.3f);
    static readonly Color Track = new Color(0.25f, 0.25f, 0.3f);
    // Где окно скилл-чеков (канвас 1920×1080, от центра) и его размер: ниже середины, чтобы было видно Нову
    static readonly Vector2 PanelPos = new Vector2(0, -250), PanelSize = new Vector2(1200, 480);

    public static MogBattle Build(GameObject hud)
    {
        var battle = hud.GetComponent<MogBattle>();
        if (battle == null) battle = hud.AddComponent<MogBattle>();
        var music = hud.GetComponent<AudioSource>();
        if (music == null) music = hud.AddComponent<AudioSource>();
        music.playOnAwake = false;
        music.spatialBlend = 0f;
        battle.music = music;

        // Окно без фона — скилл-чеки прямо в воздухе (пользователь, 2026-11-06: «рамку убери»), и пониже экрана,
        // чтобы было видно Нову. Картинка окна выключена: оно только держит место и включается целиком
        var panel = TestPlatformBuilder.NewImage("MogBattlePanel", hud.transform, new Color(0f, 0f, 0f, 0f));
        TestPlatformBuilder.Place(panel.rectTransform, PanelPos, PanelSize);
        panel.enabled = false;
        battle.panel = panel.gameObject;
        battle.title = Text("Title", panel.transform, 56, new Vector2(0, 195), new Vector2(1100, 76), Purple);
        battle.title.text = "МОГ-БАТЛ С НОВОЙ";
        battle.hint = Text("Hint", panel.transform, 32, new Vector2(0, 148), new Vector2(1100, 48), new Color(0.85f, 0.85f, 0.9f));

        // Область, где стоят кнопки и стрелки (x, y скилл-чека — доли этой области)
        var area = new GameObject("PlayArea", typeof(RectTransform)).GetComponent<RectTransform>();
        area.SetParent(panel.transform, false);
        TestPlatformBuilder.Place(area, new Vector2(0, -25), new Vector2(1100, 300));
        battle.playArea = area;

        // Полоска
        var bar = TestPlatformBuilder.NewImage("Bar", area, Track);
        TestPlatformBuilder.Place(bar.rectTransform, Vector2.zero, new Vector2(950, 60));
        battle.barRoot = bar.gameObject;
        var zone = TestPlatformBuilder.NewImage("Zone", bar.transform, Green);
        TestPlatformBuilder.Stretch(zone.rectTransform);
        battle.zone = zone.rectTransform;
        var marker = TestPlatformBuilder.NewImage("Marker", bar.transform, Color.white);
        marker.rectTransform.sizeDelta = new Vector2(14, 100);
        battle.marker = marker.rectTransform;

        // Кружок: серое кольцо, зелёная дуга, стрелка от центра
        var circle = new GameObject("Circle", typeof(RectTransform)).GetComponent<RectTransform>();
        circle.SetParent(area, false);
        TestPlatformBuilder.Place(circle, Vector2.zero, new Vector2(290, 290));
        battle.circleRoot = circle.gameObject;
        battle.circleRing = TestPlatformBuilder.NewImage("Ring", circle, Track);
        TestPlatformBuilder.Stretch(battle.circleRing.rectTransform);
        battle.circleZone = TestPlatformBuilder.NewImage("Zone", circle, Green);
        TestPlatformBuilder.Stretch(battle.circleZone.rectTransform);
        var needle = TestPlatformBuilder.NewImage("Needle", circle, new Color(1f, 0.35f, 0.3f));
        var n = needle.rectTransform;
        n.anchorMin = n.anchorMax = new Vector2(0.5f, 0.5f);
        n.pivot = new Vector2(0.5f, 0f);
        n.anchoredPosition = Vector2.zero;
        n.sizeDelta = new Vector2(10, 150);
        battle.needle = n;

        // Кнопка как в osu: круг с белой обводкой и сжимающееся кольцо (спрайты ставит MogBattle)
        battle.buttonRoot = Holder("Button", area, 130);
        battle.buttonDisc = Fill("Disc", battle.buttonRoot, new Color(0.65f, 0.35f, 0.95f));
        battle.buttonBorder = Fill("Border", battle.buttonRoot, Color.white);
        battle.buttonRing = Fill("Ring", battle.buttonRoot, Color.white);

        // Стрелка: такой же круг со знаком направления и такое же кольцо
        battle.arrowRoot = Holder("Arrow", area, 130);
        battle.arrowDisc = Fill("Disc", battle.arrowRoot, new Color(0.45f, 0.3f, 0.05f));
        battle.arrowBorder = Fill("Border", battle.arrowRoot, Color.white);
        battle.arrowGlyph = TestPlatformBuilder.NewText("Glyph", battle.arrowRoot, 96);
        battle.arrowGlyph.text = "→";
        battle.arrowGlyph.color = new Color(1f, 0.85f, 0.35f);
        battle.arrowGlyph.fontStyle = FontStyle.Bold;
        TestPlatformBuilder.Stretch(battle.arrowGlyph.rectTransform);
        battle.arrowRing = Fill("Ring", battle.arrowRoot, Color.white);

        // Вспышка на месте решённой кнопки или стрелки
        battle.burst = Holder("Burst", area, 130);
        battle.burstDisc = Fill("Disc", battle.burst, Color.white);
        battle.burstBorder = Fill("Border", battle.burst, Color.white);

        foreach (var g in panel.GetComponentsInChildren<Graphic>(true)) g.raycastTarget = false;
        panel.gameObject.SetActive(false);
        return battle;
    }

    static Text Text(string name, Transform parent, int size, Vector2 pos, Vector2 box, Color color)
    {
        var t = TestPlatformBuilder.NewText(name, parent, size);
        t.color = color;
        TestPlatformBuilder.Place(t.rectTransform, pos, box);
        var outline = t.gameObject.AddComponent<Outline>(); // фона нет — тёмная обводка, чтобы читалось поверх сцены
        outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
        outline.effectDistance = new Vector2(2f, -2f);
        return t;
    }

    // Картинка на всю площадь родителя
    static Image Fill(string name, Transform parent, Color color)
    {
        var img = TestPlatformBuilder.NewImage(name, parent, color);
        TestPlatformBuilder.Stretch(img.rectTransform);
        return img;
    }

    static RectTransform Holder(string name, Transform parent, float size)
    {
        var rt = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        TestPlatformBuilder.Place(rt, Vector2.zero, new Vector2(size, size));
        return rt;
    }
}
