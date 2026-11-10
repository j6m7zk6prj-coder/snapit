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
    MainWindow main;
    bool restoreMain, hintShown;
    SettingsForm settingsForm;

    readonly Control invoker = new Control();
    readonly RegisteredWaitHandle showWait;

    public TrayApp(EventWaitHandle showEvent, bool silent)
    {
        invoker.CreateControl();
        _ = invoker.Handle;
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
        menu.Items.Insert(0, Item("Ouvrir Snapit", "Ctrl+Maj+5", ShowMain));
        menu.Items.Insert(1, new ToolStripSeparator());
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
        tray.MouseClick += (s, e) => { if (e.Button == MouseButtons.Left) ShowMain(); };

        hotkeys = new HotkeyWindow();
        hotkeys.Pressed += OnHotkey;
        bool allOk = RegisterHotkeys();

        // Relancer Snapit (menu Démarrer, double-clic sur l'exe) = afficher le panneau.
        showWait = ThreadPool.RegisterWaitForSingleObject(showEvent, (st, timedOut) =>
        {
            try { invoker.BeginInvoke(new Action(ShowMain)); } catch { }
        }, null, Timeout.Infinite, false);

        if (settings.FirstRun)
        {
            settings.FirstRun = false;
            settings.Save();
            // Le lancement au démarrage de Windows est activé par défaut, comme une appli système.
            AppSettings.StartWithWindows = true;
        }

        // Ouverture manuelle : on montre tout de suite quelque chose (comme Capture d'écran sur Mac).
        if (!silent) ShowMain();
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
            case HK_PANEL: ShowMain(); break;
        }
    }

    // ---------- Captures ----------

    /// <summary>Ferme l'aperçu / le panneau avant de capturer pour qu'ils n'apparaissent pas sur l'image.</summary>
    void AfterClear(Action action)
    {
        bool hadSomething = toast != null;
        toast?.Close();
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
            catch (Exception ex) { ShowError(ex); RestoreMain(); return; }

            overlay = new OverlayForm(shot, Native.GetWindowRects());
            overlay.Captured += OnCaptured;
            overlay.FormClosed += (s, e) => { overlay = null; RestoreMain(); };
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
            RestoreMain();
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
        if (path != null) main?.RefreshRecent();

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
        ed.FormClosed += (s, e) => main?.RefreshRecent();
        ed.Show();
        ed.Activate();
    }

    // ---------- Fenêtres ----------

    MainWindow GetMain()
    {
        if (main != null && !main.IsDisposed) return main;
        main = new MainWindow(settings);
        main.CaptureRequested += (mode, delay) =>
        {
            Action run = mode == "screen" ? StartFullScreen : StartRegion;
            restoreMain = true;
            main.Hide();                      // la fenêtre ne doit pas apparaître sur la capture
            Delay(220, () =>
            {
                if (delay > 0) Countdown.Run(delay, run);
                else run();
            });
        };
        main.OpenFolderRequested += OpenFolder;
        main.SettingsRequested += ShowSettings;
        main.OpenFileRequested += OpenFile;
        main.HiddenToTray += () =>
        {
            if (hintShown) return;
            hintShown = true;
            tray.ShowBalloonTip(4000, "Snapit reste prêt",
                "Ctrl+Maj+4 pour capturer à tout moment. Cliquez sur l’icône Snapit pour rouvrir la fenêtre.", ToolTipIcon.None);
        };
        return main;
    }

    void ShowMain()
    {
        if (overlay != null) return;
        restoreMain = false;
        GetMain().ShowAndActivate();
    }

    /// <summary>Après une capture lancée depuis la fenêtre : on la fait revenir.</summary>
    void RestoreMain()
    {
        if (!restoreMain) return;
        restoreMain = false;
        Delay(120, () =>
        {
            if (main == null || main.IsDisposed) return;
            main.Show();
            if (main.WindowState == FormWindowState.Minimized) main.WindowState = FormWindowState.Normal;
            main.RefreshRecent();
        });
    }

    void OpenFile(string path)
    {
        try
        {
            Bitmap bmp;
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var img = Image.FromStream(fs))
                bmp = new Bitmap(img);
            OpenEditor(bmp, path);
        }
        catch (Exception ex) { ShowError(ex); }
    }

    void ShowSettings()
    {
        if (settingsForm != null) { settingsForm.Activate(); return; }
        settingsForm = new SettingsForm(settings);
        settingsForm.Saved += () => { RegisterHotkeys(); main?.RefreshRecent(); };
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
        showWait?.Unregister(null);
        main?.ReallyClose();
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
