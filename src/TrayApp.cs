using System.Diagnostics;
using System.Reflection;

namespace Snapit;

sealed class TrayApp : ApplicationContext
{
    public static Icon AppIcon { get; private set; } = SystemIcons.Application;

    const int HK_FULL = 1, HK_REGION = 2, HK_PANEL = 3, HK_PRTSC = 4;
    const uint CTRL_SHIFT = Native.MOD_CONTROL | Native.MOD_SHIFT;

    readonly AppSettings settings;
    readonly NotifyIcon tray;
    readonly HotkeyWindow hotkeys;

    OverlayForm overlay;
    PreviewToast toast;
    CapturePanel panel;
    SettingsForm settingsForm;

    public TrayApp()
    {
        settings = AppSettings.Load();
        var trayIcon = LoadIcons();

        var menu = new ContextMenuStrip
        {
            Renderer = new ModernMenuRenderer(),
            ShowImageMargin = false,
            Font = Ui.F(9.5f),
            Padding = new Padding(2, 4, 2, 4),
        };
        menu.Items.Add(Item("Capturer une zone ou une fenêtre", "Ctrl+Maj+4", StartRegion));
        menu.Items.Add(Item("Capturer l’écran entier", "Ctrl+Maj+3", StartFullScreen));
        menu.Items.Add(Item("Panneau de capture…", "Ctrl+Maj+5", ShowPanel));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(Item("Ouvrir le dossier des captures", null, OpenFolder));
        menu.Items.Add(Item("Réglages…", null, ShowSettings));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(Item("Quitter Snapit", null, ExitApp));
        menu.Opened += (s, e) => Native.RoundCorners(menu.Handle, true);

        tray = new NotifyIcon
        {
            Icon = trayIcon,
            Text = "Snapit — Ctrl+Maj+4 pour capturer",
            ContextMenuStrip = menu,
            Visible = true,
        };
        tray.MouseClick += (s, e) => { if (e.Button == MouseButtons.Left) StartRegion(); };

        hotkeys = new HotkeyWindow();
        hotkeys.Pressed += OnHotkey;
        bool allOk = RegisterHotkeys();

        if (settings.FirstRun)
        {
            settings.FirstRun = false;
            settings.Save();
            if (allOk)
                tray.ShowBalloonTip(6000, "Snapit est prêt",
                    "Ctrl+Maj+4 : zone ou fenêtre\nCtrl+Maj+3 : écran entier\nCtrl+Maj+5 : panneau de capture",
                    ToolTipIcon.None);
        }
    }

    static ToolStripMenuItem Item(string text, string keys, Action action)
    {
        var it = new ToolStripMenuItem(text) { ShortcutKeyDisplayString = keys, Padding = new Padding(6, 5, 6, 5) };
        it.Click += (s, e) => action();
        return it;
    }

    static Icon LoadIcons()
    {
        try
        {
            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Snapit.icon.ico");
            if (stream != null)
            {
                AppIcon = new Icon(stream);
                return new Icon(AppIcon, SystemInformation.SmallIconSize);
            }
        }
        catch { }
        return SystemIcons.Application;
    }

    // ---------- Raccourcis ----------

    bool RegisterHotkeys()
    {
        hotkeys.UnregisterAll();
        var failed = new List<string>();
        if (!hotkeys.Register(HK_FULL, CTRL_SHIFT, Keys.D3)) failed.Add("Ctrl+Maj+3");
        if (!hotkeys.Register(HK_REGION, CTRL_SHIFT, Keys.D4)) failed.Add("Ctrl+Maj+4");
        if (!hotkeys.Register(HK_PANEL, CTRL_SHIFT, Keys.D5)) failed.Add("Ctrl+Maj+5");
        bool prtFailed = settings.UsePrintScreen && !hotkeys.Register(HK_PRTSC, 0, Keys.PrintScreen);

        if (failed.Count > 0)
            tray.ShowBalloonTip(5000, "Raccourci indisponible",
                string.Join(", ", failed) + " est déjà utilisé par une autre application.", ToolTipIcon.Warning);
        else if (prtFailed)
            tray.ShowBalloonTip(7000, "Touche Impr. écran déjà prise",
                "Windows l’utilise pour l’Outil Capture. Pour la confier à Snapit : Paramètres › Accessibilité › Clavier › désactiver « Utiliser la touche Impr. écran… ».",
                ToolTipIcon.Info);
        return failed.Count == 0 && !prtFailed;
    }

    void OnHotkey(int id)
    {
        switch (id)
        {
            case HK_FULL: StartFullScreen(); break;
            case HK_REGION:
            case HK_PRTSC: StartRegion(); break;
            case HK_PANEL: ShowPanel(); break;
        }
    }

    // ---------- Captures ----------

    /// <summary>Ferme l'aperçu / le panneau avant de capturer pour qu'ils n'apparaissent pas sur l'image.</summary>
    void AfterClear(Action action)
    {
        bool hadSomething = toast != null || panel != null;
        toast?.Close();
        panel?.Close();
        if (hadSomething) Delay(160, action); else action();
    }

    void StartRegion()
    {
        if (overlay != null) { overlay.Activate(); return; }
        AfterClear(() =>
        {
            if (overlay != null) return;
            Bitmap shot;
            try { shot = Capturer.CaptureVirtualScreen(); }
            catch (Exception ex) { ShowError(ex); return; }

            overlay = new OverlayForm(shot, Native.GetWindowRects());
            overlay.Captured += OnCaptured;
            overlay.FormClosed += (s, e) => overlay = null;
            overlay.Show();
            overlay.Activate();
        });
    }

    void StartFullScreen()
    {
        if (overlay != null) return;
        AfterClear(() =>
        {
            try { OnCaptured(Capturer.Capture(Screen.FromPoint(Cursor.Position).Bounds)); }
            catch (Exception ex) { ShowError(ex); }
        });
    }

    void OnCaptured(Bitmap bmp)
    {
        if (settings.PlaySound) Sound.Shutter();

        string path = null;
        if (settings.SaveToFile)
        {
            try { path = Capturer.SaveUnique(bmp, settings.SaveFolder); }
            catch (Exception ex) { tray.ShowBalloonTip(4000, "Enregistrement impossible", ex.Message, ToolTipIcon.Error); }
        }
        if (settings.CopyToClipboard) ClipboardHelper.SetImage(bmp);

        if (settings.ShowPreview) ShowToast(bmp, path);
        else bmp.Dispose();
    }

    void ShowToast(Bitmap bmp, string path)
    {
        toast?.Close();
        var t = new PreviewToast(bmp, path);
        t.OpenRequested += OpenEditor;
        t.FormClosed += (s, e) => { if (toast == t) toast = null; };
        toast = t;
        t.Show();
    }

    void OpenEditor(Bitmap bmp, string path)
    {
        var ed = new EditorForm(bmp, path, settings);
        ed.Show();
        ed.Activate();
    }

    // ---------- Fenêtres ----------

    void ShowPanel()
    {
        if (overlay != null) return;
        if (panel != null) { panel.Activate(); return; }
        toast?.Close();

        panel = new CapturePanel();
        panel.RunCapture += (mode, delay) =>
        {
            Action run = mode == "screen" ? StartFullScreen : StartRegion;
            Delay(160, () =>
            {
                if (delay > 0) Countdown.Run(delay, run);
                else run();
            });
        };
        panel.OpenSettings += ShowSettings;
        panel.OpenFolder += OpenFolder;
        panel.FormClosed += (s, e) => panel = null;
        panel.Show();
        panel.Activate();
    }

    void ShowSettings()
    {
        if (settingsForm != null) { settingsForm.Activate(); return; }
        settingsForm = new SettingsForm(settings);
        settingsForm.Saved += () => RegisterHotkeys();
        settingsForm.FormClosed += (s, e) => settingsForm = null;
        settingsForm.Show();
        settingsForm.Activate();
    }

    void OpenFolder()
    {
        try
        {
            Directory.CreateDirectory(settings.SaveFolder);
            Process.Start(new ProcessStartInfo("explorer.exe", "\"" + settings.SaveFolder + "\"") { UseShellExecute = true });
        }
        catch (Exception ex) { ShowError(ex); }
    }

    void ShowError(Exception ex) =>
        tray.ShowBalloonTip(4000, "Snapit", ex.Message, ToolTipIcon.Error);

    void ExitApp()
    {
        hotkeys.Dispose();
        tray.Visible = false;
        tray.Dispose();
        ExitThread();
    }

    public static void Delay(int ms, Action action)
    {
        var t = new System.Windows.Forms.Timer { Interval = Math.Max(1, ms) };
        t.Tick += (s, e) => { t.Stop(); t.Dispose(); action(); };
        t.Start();
    }
}
