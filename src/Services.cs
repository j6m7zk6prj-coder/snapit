using System.Drawing.Imaging;
using System.Media;
using System.Text;
using System.Text.Json;
using Microsoft.Win32;

namespace Snapit;

public sealed class AppSettings
{
    public string SaveFolder { get; set; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "Captures d’écran");
    public bool CopyToClipboard { get; set; } = true;
    public bool SaveToFile { get; set; } = true;
    public bool ShowPreview { get; set; } = true;
    public bool PlaySound { get; set; } = true;
    public bool UsePrintScreen { get; set; } = true;
    public bool FirstRun { get; set; } = true;

    static string Dir => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Snapit");
    static string FilePath => Path.Combine(Dir, "settings.json");

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath)) ?? new AppSettings();
        }
        catch { }
        return new AppSettings();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Dir);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { }
    }

    const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

    public static bool StartWithWindows
    {
        get
        {
            try { using var k = Registry.CurrentUser.OpenSubKey(RunKey); return k?.GetValue("Snapit") != null; }
            catch { return false; }
        }
        set
        {
            try
            {
                using var k = Registry.CurrentUser.OpenSubKey(RunKey, true) ?? Registry.CurrentUser.CreateSubKey(RunKey);
                if (value) k.SetValue("Snapit", "\"" + Environment.ProcessPath + "\" --startup");
                else k.DeleteValue("Snapit", false);
            }
            catch { }
        }
    }
}

static class Capturer
{
    public static Bitmap Capture(Rectangle r)
    {
        var bmp = new Bitmap(r.Width, r.Height, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bmp))
            g.CopyFromScreen(r.X, r.Y, 0, 0, r.Size, CopyPixelOperation.SourceCopy);
        return bmp;
    }

    public static Bitmap CaptureVirtualScreen() => Capture(SystemInformation.VirtualScreen);

    public static string SaveUnique(Image img, string folder)
    {
        Directory.CreateDirectory(folder);
        var now = DateTime.Now;
        string baseName = $"Capture d’écran {now:yyyy-MM-dd} à {now:HH.mm.ss}";
        string path = Path.Combine(folder, baseName + ".png");
        int i = 2;
        while (File.Exists(path)) path = Path.Combine(folder, $"{baseName} ({i++}).png");
        img.Save(path, ImageFormat.Png);
        return path;
    }
}

static class ClipboardHelper
{
    public static bool SetImage(Image img)
    {
        for (int i = 0; i < 6; i++)
        {
            try { Clipboard.SetImage(img); return true; }
            catch (System.Runtime.InteropServices.ExternalException) { Thread.Sleep(50); }
        }
        return false;
    }
}

/// <summary>Petit bruit d'obturateur synthétisé (pas de fichier audio à embarquer).</summary>
static class Sound
{
    static SoundPlayer player;

    public static void Shutter()
    {
        try
        {
            if (player == null)
            {
                player = new SoundPlayer(new MemoryStream(BuildWav()));
                player.Load();
            }
            player.Play();
        }
        catch { }
    }

    static byte[] BuildWav()
    {
        const int rate = 44100;
        int n = (int)(rate * 0.17);
        var rnd = new Random(7);
        var samples = new short[n];
        double lp = 0;
        for (int i = 0; i < n; i++)
        {
            double t = i / (double)rate;
            double env = Math.Exp(-t * 75) + (t > 0.075 ? 0.75 * Math.Exp(-(t - 0.075) * 85) : 0);
            double noise = rnd.NextDouble() * 2 - 1;
            lp += (noise - lp) * 0.35; // léger filtrage pour un son plus doux
            samples[i] = (short)(Math.Clamp(lp * env * 0.9, -1, 1) * 32767);
        }

        using var ms = new MemoryStream();
        using var w = new BinaryWriter(ms);
        w.Write(Encoding.ASCII.GetBytes("RIFF"));
        w.Write(36 + n * 2);
        w.Write(Encoding.ASCII.GetBytes("WAVE"));
        w.Write(Encoding.ASCII.GetBytes("fmt "));
        w.Write(16);
        w.Write((short)1);      // PCM
        w.Write((short)1);      // mono
        w.Write(rate);
        w.Write(rate * 2);
        w.Write((short)2);
        w.Write((short)16);
        w.Write(Encoding.ASCII.GetBytes("data"));
        w.Write(n * 2);
        foreach (var s in samples) w.Write(s);
        w.Flush();
        return ms.ToArray();
    }
}

sealed class HotkeyWindow : NativeWindow, IDisposable
{
    readonly List<int> ids = new();
    public event Action<int> Pressed;

    public HotkeyWindow() => CreateHandle(new CreateParams());

    public bool Register(int id, uint modifiers, Keys key)
    {
        bool ok = Native.RegisterHotKey(Handle, id, modifiers | Native.MOD_NOREPEAT, (uint)key);
        if (ok) ids.Add(id);
        return ok;
    }

    public void UnregisterAll()
    {
        foreach (var id in ids) Native.UnregisterHotKey(Handle, id);
        ids.Clear();
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == Native.WM_HOTKEY) Pressed?.Invoke(m.WParam.ToInt32());
        base.WndProc(ref m);
    }

    public void Dispose()
    {
        UnregisterAll();
        DestroyHandle();
    }
}
