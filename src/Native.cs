using System.Runtime.InteropServices;

namespace Snapit;

static class Native
{
    public const int WM_HOTKEY = 0x0312;
    public const int WM_DPICHANGED = 0x02E0;
    public const uint MOD_ALT = 0x1, MOD_CONTROL = 0x2, MOD_SHIFT = 0x4, MOD_WIN = 0x8, MOD_NOREPEAT = 0x4000;

    public const int WS_EX_TOPMOST = 0x8;
    public const int WS_EX_TRANSPARENT = 0x20;
    public const int WS_EX_TOOLWINDOW = 0x80;
    public const int WS_EX_NOACTIVATE = 0x08000000;
    public const int CS_DROPSHADOW = 0x20000;

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT { public int Left, Top, Right, Bottom; }

    public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
    [DllImport("user32.dll")]
    public static extern bool UnregisterHotKey(IntPtr hWnd, int id);
    [DllImport("user32.dll")]
    public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);
    [DllImport("user32.dll")]
    public static extern bool IsWindowVisible(IntPtr hWnd);
    [DllImport("user32.dll")]
    public static extern bool IsIconic(IntPtr hWnd);
    [DllImport("user32.dll")]
    public static extern int GetWindowTextLength(IntPtr hWnd);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    public static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);
    [DllImport("user32.dll")]
    public static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);
    [DllImport("user32.dll")]
    public static extern IntPtr GetShellWindow();
    [DllImport("user32.dll")]
    public static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("dwmapi.dll")]
    public static extern int DwmGetWindowAttribute(IntPtr hwnd, int attr, out RECT value, int size);
    [DllImport("dwmapi.dll")]
    public static extern int DwmGetWindowAttribute(IntPtr hwnd, int attr, out int value, int size);
    [DllImport("dwmapi.dll")]
    public static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

    /// <summary>Coins arrondis façon Windows 11 (ignoré sous Windows 10).</summary>
    public static void RoundCorners(IntPtr hwnd, bool small = false)
    {
        try { int pref = small ? 3 : 2; DwmSetWindowAttribute(hwnd, 33, ref pref, sizeof(int)); } catch { }
    }

    /// <summary>Rectangles (coordonnées écran) des fenêtres visibles, de la plus haute à la plus basse.</summary>
    public static List<Rectangle> GetWindowRects()
    {
        var list = new List<Rectangle>();
        IntPtr shell = GetShellWindow();
        EnumWindows((h, l) =>
        {
            if (h == shell || !IsWindowVisible(h) || IsIconic(h)) return true;
            long ex = GetWindowLongPtr(h, -20).ToInt64();
            if ((ex & WS_EX_TOOLWINDOW) != 0) return true;
            if (DwmGetWindowAttribute(h, 14, out int cloaked, sizeof(int)) == 0 && cloaked != 0) return true;
            if (GetWindowTextLength(h) == 0) return true;

            RECT r;
            if (DwmGetWindowAttribute(h, 9, out r, Marshal.SizeOf<RECT>()) != 0)
                GetWindowRect(h, out r);
            var rc = Rectangle.FromLTRB(r.Left, r.Top, r.Right, r.Bottom);
            if (rc.Width > 20 && rc.Height > 20) list.Add(rc);
            return true;
        }, IntPtr.Zero);
        return list;
    }
}
