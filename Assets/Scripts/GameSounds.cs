using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;

// События игры, у которых есть звук. Порядок — как в паузе → «Звуки».
public enum SoundSlot
{
    Ambient, GeneratorStart, GeneratorLoop, GeneratorStop,
    DoorClose, DoorOpen, DoorHit, SoundButton, DanceFloorMusic,
    PhoneRing, CallRight, NightLoading, NightEnd, NightEndLock, MonitorReboot,
    DoliNear, StrayNear, DaliNear, NovaNear,
    DoliGrowl, FuelDrain, DirHunger, Screamer,
}

// Группы громкости (меню → «Настройки» → «Громкость»). Общая — AudioListener.volume, остальные умножают звуки группы
public enum VolumeGroup { Master, Music, Ambient, Generator, Effects }

// Звуки игры. У каждого события (SoundSlot) — свой звук; его можно сменить в паузе → «Звуки» прямо во время ночи,
// выбор хранится между запусками (PlayerPrefs «SSDFNC.Sound.<событие>»). На любое событие можно поставить любой файл.
// Звуки — файлы в StreamingAssets/Sounds (wav, mp3, ogg): новые, положенные туда, видны в списке сами.
// «Встроенный» — звук, сделанный в коде (гудок, звонок, рычание…), «нет» — тишина.
// Проигрывают: GameSounds.Play (разовый звук), GameSounds.Bind (свой AudioSource, например петля), Preview (в настройках).
public static class GameSounds
{
    public const string None = "";      // без звука
    public const string Builtin = "*";  // встроенный звук (сделан в коде)
    public const string NightScene = "SampleScene"; // загрузка этой сцены — «перед ночью»
    const string PrefsKey = "SSDFNC.Sound.";
    // Громкость: генератор (запуск, работа и глушение — одинаково, иначе на стыках скачет) и фон чуть громче него
    const float GeneratorVolume = 0.2f; // ≈ −36 дБ в игре
    const float AmbientVolume = 0.25f;  // ≈ −33 дБ

    public class SlotInfo
    {
        public readonly string Title, Default;
        public readonly bool Loop, HasBuiltin;
        public readonly float Volume;

        public SlotInfo(string title, string def, float volume = 1f, bool loop = false, bool builtin = false)
        {
            Title = title; Default = def; Volume = volume; Loop = loop; HasBuiltin = builtin;
        }
    }

    // Название в настройках, звук по умолчанию, громкость, петля, есть ли встроенный — тут
    public static readonly SlotInfo[] Slots =
    {
        new SlotInfo("Фон ночи (эмбиэнт)", "эмбиэнт.mp3", AmbientVolume, loop: true),
        new SlotInfo("Генератор: запуск", "генератор запуск.wav", GeneratorVolume),
        new SlotInfo("Генератор: работа", "генератор работа.wav", GeneratorVolume, loop: true),
        new SlotInfo("Генератор: глушение", "генератор глушение.wav", GeneratorVolume),
        new SlotInfo("Дверь закрылась", "дверь закрылась.mp3", 0.9f),
        new SlotInfo("Дверь открылась", None, 0.9f),
        new SlotInfo("Удар об закрытую дверь", "удар об дверь.mp3"),
        new SlotInfo("Красная кнопка (звук)", "звук кнопки.mp3", builtin: true),
        new SlotInfo("Дэнс пол (играет, пока горит)", "дэнс пол.mp3", 0.7f, loop: true),
        new SlotInfo("Телефон звонит", Builtin, 0.8f, loop: true, builtin: true),
        new SlotInfo("Верный ответ на звонок", "правильный ответ звонка.mp3"),
        new SlotInfo("Загрузка перед ночью", "загрузка перед ночью.mp3"),
        new SlotInfo("Конец ночи", "конец ночи.mp3"),
        new SlotInfo("Конец ночи: замок (уходим)", "замок 1.mp3"), // вместе с «Концом ночи»
        new SlotInfo("Сбой монитора", Builtin, builtin: true),
        new SlotInfo("Доли подошла", "колокольчики.wav"),
        new SlotInfo("Стрей подошёл", None),
        new SlotInfo("Дали подошла", None),
        new SlotInfo("Нова подошла", None),
        new SlotInfo("Доли рычит (подошла к двери или генератору, один раз)", Builtin, builtin: true),
        new SlotInfo("Слив бензина", Builtin, loop: true, builtin: true),
        new SlotInfo("Дир голоден", Builtin, builtin: true),
        new SlotInfo("Скример (смерть от фурри)", "скример.wav"), // один «буум» из «низкого бааам» (там их 14 подряд)
    };

    public static SlotInfo Info(SoundSlot slot) => Slots[(int)slot];

    // ── Громкость ──

    public static readonly string[] GroupNames = { "Общая", "Музыка мог-батла", "Фон ночи", "Генератор", "Остальные звуки" };
    const string VolumeKey = "SSDFNC.Volume.";

    // Громкость поменялась — играющие звуки меняют громкость сразу (без перезапуска)
    public static event Action VolumeChanged;

    public static VolumeGroup GroupOf(SoundSlot slot) =>
        slot == SoundSlot.Ambient ? VolumeGroup.Ambient
        : slot == SoundSlot.GeneratorStart || slot == SoundSlot.GeneratorLoop || slot == SoundSlot.GeneratorStop ? VolumeGroup.Generator
        : VolumeGroup.Effects;

    public static float Volume(VolumeGroup group) => Mathf.Clamp01(PlayerPrefs.GetFloat(VolumeKey + group, 1f));

    public static void SetVolume(VolumeGroup group, float value)
    {
        PlayerPrefs.SetFloat(VolumeKey + group, Mathf.Clamp01(value));
        PlayerPrefs.Save();
        if (group == VolumeGroup.Master) AudioListener.volume = Volume(VolumeGroup.Master);
        VolumeChanged?.Invoke();
    }

    // Громкость звука события: своя (Slots) × громкость группы
    public static float VolumeOf(SoundSlot slot) => Info(slot).Volume * Volume(GroupOf(slot));

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void ApplyMasterVolume() => AudioListener.volume = Volume(VolumeGroup.Master);

    // Выбор поменялся (в настройках) — привязанные источники меняют звук сразу
    public static event Action<SoundSlot> Changed;

    public static string Folder => Path.Combine(Application.streamingAssetsPath, "Sounds");

    static readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
    static readonly Dictionary<string, List<Action<AudioClip>>> loading = new Dictionary<string, List<Action<AudioClip>>>();
    static readonly Dictionary<SoundSlot, AudioClip> builtins = new Dictionary<SoundSlot, AudioClip>();
    static Runner runner;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() // перезагрузки домена при входе в Play нет — статика живёт между запусками
    {
        Changed = null;
        VolumeChanged = null;
        clips.Clear();
        loading.Clear();
        builtins.Clear();
        runner = null;
    }

    // Без AudioListener ничего не слышно, а он есть только на камере офиса: в Boot, меню и редакторе раскладок его
    // нет — звук загрузки перед ночью играл, но слышно было только конец, когда включался офис. Нет «ушей» в
    // загруженной сцене — ставим на её камеру (только на время игры, в сцену не сохраняется)
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void WatchListener()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        EnsureListener();
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => EnsureListener();

    static void EnsureListener()
    {
        if (UnityEngine.Object.FindAnyObjectByType<AudioListener>() != null) return;
        var cam = Camera.main;
        (cam != null ? cam.gameObject : new GameObject("AudioListener")).AddComponent<AudioListener>();
    }

    // ── Выбор ──

    public static string Choice(SoundSlot slot) => PlayerPrefs.GetString(PrefsKey + slot, Info(slot).Default);

    public static void SetChoice(SoundSlot slot, string choice)
    {
        PlayerPrefs.SetString(PrefsKey + slot, choice);
        PlayerPrefs.Save();
        Changed?.Invoke(slot);
    }

    public static void ResetAll()
    {
        foreach (SoundSlot slot in Enum.GetValues(typeof(SoundSlot)))
        {
            PlayerPrefs.DeleteKey(PrefsKey + slot);
            Changed?.Invoke(slot);
        }
        PlayerPrefs.Save();
    }

    public static string ChoiceName(string choice) =>
        choice == None ? "— нет —" : choice == Builtin ? "встроенный" : Path.GetFileNameWithoutExtension(choice);

    // Что можно выбрать: нет, встроенный (если есть) и все файлы из StreamingAssets/Sounds
    public static List<string> Options(SoundSlot slot)
    {
        var list = new List<string> { None };
        if (Info(slot).HasBuiltin) list.Add(Builtin);
        list.AddRange(Files());
        return list;
    }

    public static List<string> Files()
    {
        var files = new List<string>();
        if (!Directory.Exists(Folder)) return files;
        foreach (var path in Directory.GetFiles(Folder))
        {
            string ext = Path.GetExtension(path).ToLowerInvariant();
            if (ext == ".wav" || ext == ".mp3" || ext == ".ogg") files.Add(Path.GetFileName(path));
        }
        files.Sort(StringComparer.OrdinalIgnoreCase);
        return files;
    }

    // Звук, сделанный в коде: его ставят сами скрипты (звонок, гудок…), когда создают
    public static void SetBuiltin(SoundSlot slot, AudioClip clip) => builtins[slot] = clip;

    // ── Звук события ──

    // Клип выбранного звука (null — тишина). Файл грузится один раз, потом берётся из памяти
    public static void Clip(SoundSlot slot, Action<AudioClip> done)
    {
        string choice = Choice(slot);
        if (choice == None) done(null);
        else if (choice == Builtin) done(builtins.TryGetValue(slot, out var c) ? c : null);
        else Load(choice, done);
    }

    // Разовый звук события. ui — играет и на паузе / на экране «Ночь окончена» (там весь остальной звук стоит)
    public static void Play(SoundSlot slot, AudioSource source = null, bool ui = false)
    {
        float volume = VolumeOf(slot);
        Clip(slot, clip =>
        {
            if (clip == null) return;
            var s = source != null && !ui ? source : ui ? R.ui : R.sfx;
            if (s != null) s.PlayOneShot(clip, volume);
        });
    }

    // Два звука вместе (конец ночи + замок): второй — через delay с после начала первого (0 — одновременно).
    // ui — играют и на паузе / на экране «Ночь окончена»
    public static void PlayPair(SoundSlot first, SoundSlot second, float delay, bool ui = false)
    {
        Clip(first, a => Clip(second, b =>
        {
            var r = R;
            double at = AudioSettings.dspTime + 0.05;
            if (a != null)
            {
                r.seqFirst.ignoreListenerPause = r.seqSecond.ignoreListenerPause = ui;
                r.seqFirst.clip = a;
                r.seqFirst.volume = VolumeOf(first);
                r.seqFirst.PlayScheduled(at);
            }
            at += Math.Max(0f, delay);
            if (b == null) return;
            r.seqSecond.clip = b;
            r.seqSecond.volume = VolumeOf(second);
            r.seqSecond.PlayScheduled(at);
            r.secondAt = at;
        }));
    }

    // Уходим на другую сцену: звуки экрана «Ночь окончена» затихают за 0.25 с. Замок, если ещё не начался, — сразу
    // (ушли раньше, чем доиграл «Конец ночи», — всё равно запираем за собой); начавшийся доигрывает
    public static void StopUi()
    {
        if (runner == null) return;
        runner.StartCoroutine(runner.Fade(runner.seqFirst)); // «Конец ночи» (звук загрузки на ui не трогаем)
        if (runner.seqSecond.clip != null && AudioSettings.dspTime < runner.secondAt)
        {
            runner.seqSecond.Stop();
            runner.seqSecond.Play();
            runner.secondAt = AudioSettings.dspTime;
        }
    }

    // Привязать AudioSource к событию: клип и громкость ставятся сами и меняются, когда меняют выбор.
    // Играть / не играть — через binding.SetPlaying (клип может догрузиться позже — тогда заиграет сам)
    public static SoundBinding Bind(AudioSource source, SoundSlot slot)
    {
        var binding = source.gameObject.AddComponent<SoundBinding>();
        binding.Init(source, slot);
        return binding;
    }

    // Послушать в настройках (играет и на паузе). Каждый новый — вместо предыдущего.
    // group — с громкостью другой группы (так «Громкость» показывает музыку мог-батла коротким звуком)
    public static void Preview(SoundSlot slot, VolumeGroup? group = null)
    {
        var s = R.preview;
        s.Stop();
        float volume = Info(slot).Volume * Volume(group ?? GroupOf(slot));
        Clip(slot, clip =>
        {
            if (clip == null) return;
            s.clip = clip;
            s.volume = volume;
            s.Play();
        });
    }

    public static void StopPreview()
    {
        if (runner != null) runner.preview.Stop();
    }

    static void Load(string file, Action<AudioClip> done)
    {
        if (clips.TryGetValue(file, out var clip) && clip != null)
        {
            done(clip);
            return;
        }
        if (loading.TryGetValue(file, out var waiting))
        {
            waiting.Add(done);
            return;
        }
        loading[file] = new List<Action<AudioClip>> { done };
        R.StartCoroutine(LoadRoutine(file));
    }

    static IEnumerator LoadRoutine(string file)
    {
        string path = Path.Combine(Folder, file);
        AudioClip clip = null;
        if (File.Exists(path))
        {
            using (var req = UnityWebRequestMultimedia.GetAudioClip(new Uri(path).AbsoluteUri, TypeOf(file)))
            {
                ((DownloadHandlerAudioClip)req.downloadHandler).compressed = !file.EndsWith(".wav", StringComparison.OrdinalIgnoreCase);
                yield return req.SendWebRequest();
                if (req.result == UnityWebRequest.Result.Success)
                {
                    clip = DownloadHandlerAudioClip.GetContent(req);
                    clip.name = Path.GetFileNameWithoutExtension(file);
                    clips[file] = clip;
                    R.loaded.Add(clip); // ссылка из живого объекта — иначе выгрузка ресурсов при смене сцены удалит клип
                }
                else Debug.LogWarning($"[SSDFNC] Звук {file} не загрузился: {req.error}");
            }
        }
        else Debug.LogWarning($"[SSDFNC] Нет звука {file} в {Folder}");

        var waiting = loading[file];
        loading.Remove(file);
        foreach (var done in waiting) done(clip);
    }

    static AudioType TypeOf(string file)
    {
        switch (Path.GetExtension(file).ToLowerInvariant())
        {
            case ".wav": return AudioType.WAV;
            case ".ogg": return AudioType.OGGVORBIS;
            default: return AudioType.MPEG;
        }
    }

    // Невидимый объект на всю игру: грузит звуки и играет разовые (переживает смену сцены)
    static Runner R
    {
        get
        {
            if (runner == null)
            {
                var go = new GameObject("GameSounds");
                UnityEngine.Object.DontDestroyOnLoad(go);
                runner = go.AddComponent<Runner>();
                runner.sfx = NewSource(go, false);
                runner.ui = NewSource(go, true);
                runner.preview = NewSource(go, true);
                runner.seqFirst = NewSource(go, true);
                runner.seqSecond = NewSource(go, true);
            }
            return runner;
        }
    }

    static AudioSource NewSource(GameObject go, bool ignorePause)
    {
        var s = go.AddComponent<AudioSource>();
        s.playOnAwake = false;
        s.spatialBlend = 0f;
        s.ignoreListenerPause = ignorePause;
        return s;
    }

    public class Runner : MonoBehaviour
    {
        public AudioSource sfx, ui, preview, seqFirst, seqSecond;
        public List<AudioClip> loaded = new List<AudioClip>();
        public double secondAt; // когда начинается второй звук PlayPair (часы звука)

        public IEnumerator Fade(AudioSource s, float time = 0.25f)
        {
            float from = s.volume;
            for (float t = 0f; t < time && s.isPlaying; t += Time.unscaledDeltaTime)
            {
                s.volume = from * (1f - t / time);
                yield return null;
            }
            s.Stop();
            s.volume = from;
        }
    }
}

// AudioSource, привязанный к событию: свой клип и громкость из GameSounds, меняются сразу, как поменяли выбор
public class SoundBinding : MonoBehaviour
{
    public AudioSource Source { get; private set; }
    public SoundSlot Slot { get; private set; }
    public bool Wanted { get; private set; } // должен ли сейчас звучать

    int version;

    public void Init(AudioSource source, SoundSlot slot)
    {
        Source = source;
        Slot = slot;
        source.playOnAwake = false;
        GameSounds.Changed += OnChanged;
        GameSounds.VolumeChanged += OnVolume;
        Apply();
    }

    void OnDestroy()
    {
        GameSounds.Changed -= OnChanged;
        GameSounds.VolumeChanged -= OnVolume;
    }

    void OnVolume()
    {
        if (Source != null) Source.volume = GameSounds.VolumeOf(Slot);
    }

    void OnChanged(SoundSlot slot)
    {
        if (slot == Slot) Apply();
    }

    void Apply()
    {
        int v = ++version;
        var info = GameSounds.Info(Slot);
        GameSounds.Clip(Slot, clip =>
        {
            if (this == null || v != version) return; // объект удалён или выбор уже снова поменяли
            Source.Stop();
            Source.clip = clip;
            Source.volume = GameSounds.VolumeOf(Slot);
            Source.loop = info.Loop;
            if (Wanted && clip != null) Source.Play();
        });
    }

    // Включить / выключить звук; можно звать каждый кадр. На паузе (AudioListener.pause) isPlaying — false,
    // и Play начал бы звук с начала, поэтому на паузе не трогаем
    public void SetPlaying(bool on)
    {
        Wanted = on;
        if (AudioListener.pause && !Source.ignoreListenerPause) return;
        if (on && Source.clip != null && !Source.isPlaying) Source.Play();
        else if (!on && Source.isPlaying) Source.Stop();
    }
}
