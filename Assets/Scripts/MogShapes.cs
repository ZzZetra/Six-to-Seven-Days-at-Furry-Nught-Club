using UnityEngine;

// Спрайты для скилл-чеков, нарисованные в коде (картинок в проекте нет): кольцо и круг, белые —
// цвет задаёт Image. Рисуются один раз и переиспользуются.
public static class MogShapes
{
    const int Size = 256;

    static Sprite ring, thinRing, border, disc;

    public static Sprite Ring
    {
        get
        {
            if (ring == null) ring = Make(0.82f);
            return ring;
        }
    }

    // Тонкое кольцо: сжимающееся кольцо кнопок и стрелок
    public static Sprite ThinRing
    {
        get
        {
            if (thinRing == null) thinRing = Make(0.93f);
            return thinRing;
        }
    }

    // Белая обводка кнопки (как у кружков в osu): сжимающееся кольцо смыкается ровно с ней
    public static Sprite Border
    {
        get
        {
            if (border == null) border = Make(0.86f);
            return border;
        }
    }

    public static Sprite Disc
    {
        get
        {
            if (disc == null) disc = Make(0f);
            return disc;
        }
    }

    // inner — внутренний радиус кольца как доля внешнего (0 — сплошной круг). Края сглажены на пиксель
    static Sprite Make(float inner)
    {
        var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
        };
        var pixels = new Color32[Size * Size];
        float r = Size / 2f;
        for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                float d = Mathf.Sqrt((x + 0.5f - r) * (x + 0.5f - r) + (y + 0.5f - r) * (y + 0.5f - r)) / r;
                float a = Mathf.Clamp01((1f - d) * r);
                if (inner > 0f) a *= Mathf.Clamp01((d - inner) * r);
                pixels[y * Size + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
        tex.SetPixels32(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f), 100f);
    }
}
