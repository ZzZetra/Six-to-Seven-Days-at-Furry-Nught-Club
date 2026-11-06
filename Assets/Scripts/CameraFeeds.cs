using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

// Кадры камер монитора: StreamingAssets/Cameras/<камера>/<кто в кадре>.jpg|png|mp4 (кладёт Tools/Cameras/import_cameras.py,
// можно и руками). Камера — как на кнопке монитора (2–8, Г). Кто в кадре — имена фурри через « и »: «Стрей и Доли.jpg»,
// пустая комната — «пусто.jpg». Кадр подходит, если в комнате ровно эти фурри; нет такого — монитор пишет текстом.
// Картинки грузятся один раз, видео (mp4, по кругу) играет VideoPlayer монитора по пути к файлу.
public static class CameraFeeds
{
    public class Feed
    {
        public string path;
        public bool video;
        public HashSet<FurryId> who;
    }

    static readonly Dictionary<string, FurryId> Names = new Dictionary<string, FurryId>(StringComparer.OrdinalIgnoreCase)
    {
        { "дир", FurryId.Dir }, { "стрей", FurryId.Stray }, { "нова", FurryId.Nova }, { "дали", FurryId.Dali }, { "доли", FurryId.Doli },
    };
    static readonly string[] Empty = { "пусто", "пустая", "никого" };
    static readonly string[] Separators = { " и ", ",", "+", "&" };

    static Dictionary<string, List<Feed>> index;
    static readonly Dictionary<string, Texture2D> textures = new Dictionary<string, Texture2D>();

    public static string Folder => Path.Combine(Application.streamingAssetsPath, "Cameras");

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() // новые файлы видны с нового запуска Play
    {
        index = null;
        textures.Clear();
    }

    // Кадр для комнаты, где сейчас ровно эти фурри; нет — null
    public static Feed Find(Room room, IEnumerable<FurryId> present)
    {
        if (index == null) Scan();
        if (!index.TryGetValue(Rooms.CameraLabel(room), out var feeds)) return null;
        var set = new HashSet<FurryId>(present);
        return feeds.FirstOrDefault(f => f.who.SetEquals(set));
    }

    public static Texture2D Texture(Feed feed)
    {
        if (textures.TryGetValue(feed.path, out var tex) && tex != null) return tex;
        tex = new Texture2D(2, 2, TextureFormat.RGB24, false) { name = Path.GetFileNameWithoutExtension(feed.path) };
        if (!tex.LoadImage(File.ReadAllBytes(feed.path))) Debug.LogWarning($"[SSDFNC] Камера: не читается {feed.path}");
        textures[feed.path] = tex;
        return tex;
    }

    static void Scan()
    {
        index = new Dictionary<string, List<Feed>>(StringComparer.OrdinalIgnoreCase);
        if (!Directory.Exists(Folder)) return;
        foreach (var dir in Directory.GetDirectories(Folder))
        {
            var list = new List<Feed>();
            foreach (var path in Directory.GetFiles(dir))
            {
                string ext = Path.GetExtension(path).ToLowerInvariant();
                if (ext != ".jpg" && ext != ".jpeg" && ext != ".png" && ext != ".mp4") continue;
                var who = Parse(Path.GetFileNameWithoutExtension(path));
                if (who == null)
                {
                    Debug.LogWarning($"[SSDFNC] Камера: не понял, кто в кадре: {Path.GetFileName(dir)}/{Path.GetFileName(path)}");
                    continue;
                }
                list.Add(new Feed { path = path, video = ext == ".mp4", who = who });
            }
            index[Path.GetFileName(dir)] = list;
        }
        Debug.Log($"[SSDFNC] Кадры камер: {index.Sum(p => p.Value.Count)} (" +
                  string.Join(", ", index.Where(p => p.Value.Count > 0).Select(p => $"{p.Key}: {p.Value.Count}")) + ")");
    }

    static HashSet<FurryId> Parse(string name)
    {
        var who = new HashSet<FurryId>();
        string n = name.Trim().ToLowerInvariant();
        if (Empty.Contains(n)) return who;
        foreach (var part in n.Split(Separators, StringSplitOptions.RemoveEmptyEntries))
        {
            if (!Names.TryGetValue(part.Trim(), out var id)) return null;
            who.Add(id);
        }
        return who.Count > 0 ? who : null;
    }
}
