using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Загрузочный экран между сценами: при запуске игры (сцена Boot → меню), перед ночью, «Заново», выход в меню.
// SceneLoader.Load("SampleScene", "Начинаем ночь…"). Экран строит себя сам и живёт всю игру (DontDestroyOnLoad),
// поверх всего. Сцена грузится в фоне (LoadSceneAsync), экран держится хотя бы minTime, чтобы не мигал;
// внизу — случайный совет. Пауза и экран конца ночи перед загрузкой снимаются (GameFlow.Reset).
// Перед ночью играет звук «Загрузка перед ночью» (GameSounds): экран держится, пока звук не подойдёт к концу, и
// уходит так, чтобы исчезнуть ровно с последним звуком (длина ухода подстраивается). Пока экран закрывает всё
// (Covering), игрок в офисе ещё не «появился» — CameraLook начинает появление, когда экран начинает уходить.
public class SceneLoader : MonoBehaviour
{
    const float MinShowTime = 1.4f;  // с, меньше экран не показывается
    const float FadeTime = 0.3f;
    const float ActivationTime = 0.25f;         // с, примерно столько включается сцена офиса (запас под звук)
    const float MinFadeOut = 0.15f, MaxFadeOut = 1f; // с, пределы ухода экрана под конец звука

    static readonly string[] Tips =
    {
        "Дир ест примерно каждые полминуты. Камера 7 — его ресторан.",
        "Дверь можно держать закрытой не дольше 20 секунд — потом она откроется сама.",
        "Дали боится кнопки звука. А Доли у двери от звука только злится.",
        "Генератор заглох? Подойди к нему (W) и дёрни ручку стартера.",
        "Стрей на дэнс полу? Зажги пол — и он уйдёт.",
        "Отвечая на звонок, смотри на табличку «Программа на ночь».",
        "Мог-батл: попал — запас мога растёт, промахнулся — падает. На нуле — всё.",
        "Монитор иногда перезагружается. Это всего 3 секунды, не паникуй.",
        "Esc — пауза. Даже во время мог-батла.",
        "Закончился бензин в баке — подойди к генератору и держи «Залить».",
    };

    static SceneLoader instance;
    public static bool IsLoading { get; private set; }
    // Экран загрузки закрывает всё (ещё не начал уходить)
    public static bool Covering => instance != null && instance.group.alpha > 0.99f;

    // Play в редакторе без перезагрузки домена: статика с прошлого запуска не должна мешать
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        instance = null;
        IsLoading = false;
    }

    AudioSource nightSound;
    double soundEnd;   // когда кончится звук перед ночью (часы звука); 0 — звука нет
    bool soundPending; // звук ещё грузится
    CanvasGroup group;
    Text caption, tip, percent;
    RectTransform fill;
    RectTransform spinner;
    float fillWidth;

    public static void Load(string scene, string text, float minTime = MinShowTime)
    {
        if (IsLoading) return;
        if (instance == null)
        {
            var go = new GameObject("SceneLoader");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<SceneLoader>();
            instance.Build();
        }
        Debug.Log($"[SSDFNC] Загрузка: {scene}");
        GameSounds.StopUi(); // недоигравшие «Конец ночи» и замок
        instance.soundEnd = 0;
        instance.StartCoroutine(instance.Run(scene, text, minTime));
        if (scene == GameSounds.NightScene) instance.PlayNightSound(); // после Run: там IsLoading = true
    }

    IEnumerator Run(string scene, string text, float minTime)
    {
        IsLoading = true;
        GameFlow.Reset();
        caption.text = text;
        tip.text = "Совет: " + Tips[Random.Range(0, Tips.Length)];
        SetProgress(0f);
        group.blocksRaycasts = true;
        for (float t = 0f; t < FadeTime; t += Time.unscaledDeltaTime)
        {
            group.alpha = t / FadeTime;
            yield return null;
        }
        group.alpha = 1f;

        var op = SceneManager.LoadSceneAsync(scene);
        op.allowSceneActivation = false;
        float elapsed = 0f, shown = 0f;
        double started = AudioSettings.dspTime;
        while (true)
        {
            elapsed += Time.unscaledDeltaTime;
            float need = minTime; // со звуком — держимся, пока от него не останется время на включение и уход
            if (soundEnd > 0) need = Mathf.Max(need, (float)(soundEnd - started) - ActivationTime - FadeTime);
            float target = Mathf.Min(Mathf.Clamp01(op.progress / 0.9f), elapsed / need);
            shown = Mathf.MoveTowards(shown, target, Time.unscaledDeltaTime * 2.5f);
            SetProgress(shown);
            bool soundReady = !soundPending || elapsed > 2f;
            if (op.progress >= 0.9f && elapsed >= need && shown >= 0.999f && soundReady) break;
            yield return null;
        }
        op.allowSceneActivation = true;
        while (!op.isDone) yield return null;
        yield return null; // кадр на Awake и Start новой сцены
        yield return null; // и на первую отрисовку (на ней бывает подвисание — уход экрана проскочил бы за один кадр)

        // Уходим так, чтобы исчезнуть ровно с концом звука
        float fadeOut = soundEnd > 0
            ? Mathf.Clamp((float)(soundEnd - AudioSettings.dspTime), MinFadeOut, MaxFadeOut)
            : FadeTime;
        for (float t = 0f; t < fadeOut; t += Time.unscaledDeltaTime)
        {
            group.alpha = 1f - t / fadeOut;
            yield return null;
        }
        group.alpha = 0f;
        group.blocksRaycasts = false;
        IsLoading = false;
    }

    void Update()
    {
        if (group.alpha > 0f) spinner.Rotate(0f, 0f, -300f * Time.unscaledDeltaTime);
    }

    // Звук перед ночью — свой источник (играет и на паузе), время конца — по часам звука
    void PlayNightSound()
    {
        if (nightSound == null)
        {
            nightSound = gameObject.AddComponent<AudioSource>();
            nightSound.playOnAwake = false;
            nightSound.ignoreListenerPause = true;
        }
        nightSound.Stop();
        soundPending = true;
        float volume = GameSounds.VolumeOf(SoundSlot.NightLoading);
        GameSounds.Clip(SoundSlot.NightLoading, clip =>
        {
            soundPending = false;
            if (clip == null || !IsLoading) return;
            double at = AudioSettings.dspTime + 0.02;
            nightSound.clip = clip;
            nightSound.volume = volume;
            nightSound.PlayScheduled(at);
            soundEnd = at + (double)clip.samples / clip.frequency;
        });
    }

    void SetProgress(float k)
    {
        fill.sizeDelta = new Vector2(fillWidth * k, fill.sizeDelta.y);
        percent.text = $"{k * 100f:0}%";
    }

    // Экран: тёмный фон, название, подпись, полоска с процентами, крутилка, совет внизу
    void Build()
    {
        GameSounds.Clip(SoundSlot.NightLoading, _ => { }); // загрузить заранее: тогда звук начнётся сразу с экраном
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000;
        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        gameObject.AddComponent<GraphicRaycaster>(); // клики на время загрузки не проходят в сцену
        group = gameObject.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        group.blocksRaycasts = false;

        var bg = NewImage("Background", transform, new Color(0.07f, 0.04f, 0.1f));
        Stretch(bg.rectTransform);

        var title = NewText("Title", transform, 72, new Vector2(0, 170), new Vector2(1600, 200), Color.white);
        title.text = "Six to Seven Days\nat the Furry Night Club";
        caption = NewText("Caption", transform, 44, new Vector2(0, -10), new Vector2(1400, 70), new Color(1f, 0.77f, 0.08f));

        fillWidth = 900f;
        var back = NewImage("Bar", transform, new Color(0.18f, 0.12f, 0.24f));
        back.rectTransform.sizeDelta = new Vector2(fillWidth + 8, 26);
        back.rectTransform.anchoredPosition = new Vector2(0, -100);
        var f = NewImage("Fill", back.transform, new Color(0.69f, 0.49f, 1f));
        fill = f.rectTransform;
        fill.anchorMin = fill.anchorMax = fill.pivot = new Vector2(0f, 0.5f);
        fill.anchoredPosition = new Vector2(4, 0);
        fill.sizeDelta = new Vector2(0, 18);
        percent = NewText("Percent", transform, 32, new Vector2(0, -150), new Vector2(300, 50), new Color(0.75f, 0.7f, 0.85f));

        var spin = NewImage("Spinner", transform, new Color(0.69f, 0.49f, 1f));
        spin.sprite = MogShapes.Ring;
        spin.type = Image.Type.Filled;
        spin.fillMethod = Image.FillMethod.Radial360;
        spin.fillAmount = 0.28f;
        spinner = spin.rectTransform;
        spinner.anchorMin = spinner.anchorMax = new Vector2(1f, 0f);
        spinner.sizeDelta = new Vector2(90, 90);
        spinner.anchoredPosition = new Vector2(-110, 110);

        tip = NewText("Tip", transform, 32, new Vector2(0, -400), new Vector2(1500, 90), new Color(0.8f, 0.75f, 0.9f));
        tip.fontStyle = FontStyle.Italic;
    }

    static Image NewImage(string name, Transform parent, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.color = color;
        return img;
    }

    static Text NewText(string name, Transform parent, int size, Vector2 pos, Vector2 box, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);
        var t = go.GetComponent<Text>();
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.fontSize = size;
        t.alignment = TextAnchor.MiddleCenter;
        t.color = color;
        t.raycastTarget = false;
        var rt = t.rectTransform;
        rt.anchoredPosition = pos;
        rt.sizeDelta = box;
        return t;
    }

    static void Stretch(RectTransform r)
    {
        r.anchorMin = Vector2.zero;
        r.anchorMax = Vector2.one;
        r.offsetMin = r.offsetMax = Vector2.zero;
    }
}
