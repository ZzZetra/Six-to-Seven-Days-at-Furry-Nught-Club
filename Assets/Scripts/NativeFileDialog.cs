using System;
using System.Runtime.InteropServices;

// Окно Windows «Открыть файл» для редактора раскладок (отдельная программа). В Unity — то же окно редактора
// (EditorUtility.OpenFilePanel). Вернёт полный путь или null, если окно закрыли.
public static class NativeFileDialog
{
    // extensions — через запятую, без точек: "ogg,wav,mp3"
    public static string Open(string title, string kind, string extensions, string folder = "")
    {
#if UNITY_EDITOR
        string path = UnityEditor.EditorUtility.OpenFilePanel(title, folder ?? "", extensions);
        return string.IsNullOrEmpty(path) ? null : path;
#elif UNITY_STANDALONE_WIN
        string pattern = string.Join(";", Array.ConvertAll(extensions.Split(','), e => "*." + e.Trim()));
        var ofn = new OpenFileName();
        ofn.structSize = Marshal.SizeOf(ofn);
        ofn.dlgOwner = GetActiveWindow();
        ofn.filter = $"{kind} ({pattern})\0{pattern}\0\0";
        ofn.file = new string('\0', 1024);
        ofn.maxFile = ofn.file.Length;
        ofn.fileTitle = new string('\0', 256);
        ofn.maxFileTitle = ofn.fileTitle.Length;
        ofn.initialDir = folder;
        ofn.title = title;
        ofn.flags = Explorer | FileMustExist | PathMustExist | NoChangeDir;
        return GetOpenFileName(ofn) ? ofn.file.TrimEnd('\0') : null;
#else
        return null;
#endif
    }

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
    const int Explorer = 0x00080000, FileMustExist = 0x00001000, PathMustExist = 0x00000800, NoChangeDir = 0x00000008;

    // OPENFILENAMEW из comdlg32: порядок и типы полей — как в Windows
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    class OpenFileName
    {
        public int structSize;
        public IntPtr dlgOwner = IntPtr.Zero;
        public IntPtr instance = IntPtr.Zero;
        public string filter;
        public string customFilter;
        public int maxCustFilter;
        public int filterIndex;
        public string file;
        public int maxFile;
        public string fileTitle;
        public int maxFileTitle;
        public string initialDir;
        public string title;
        public int flags;
        public short fileOffset;
        public short fileExtension;
        public string defExt;
        public IntPtr custData = IntPtr.Zero;
        public IntPtr hook = IntPtr.Zero;
        public string templateName;
        public IntPtr reservedPtr = IntPtr.Zero;
        public int reservedInt;
        public int flagsEx;
    }

    [DllImport("comdlg32.dll", EntryPoint = "GetOpenFileNameW", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern bool GetOpenFileName([In, Out] OpenFileName ofn);

    [DllImport("user32.dll")]
    static extern IntPtr GetActiveWindow();
#endif
}
