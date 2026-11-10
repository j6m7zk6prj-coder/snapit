using System.Diagnostics;
using System.Drawing.Drawing2D;

namespace Snapit;

/// <summary>
/// Fenêtre principale qui reste ouverte : boutons de capture + captures récentes.
/// Elle se cache juste le temps d'une capture puis revient.
/// Fermer (X) la range dans la zone de notification ; les raccourcis restent actifs.
/// </summary>
sealed class MainWindow : Form
{
    public event Action<string, int> CaptureRequested; // mode ("region"/"screen"), délai en s
    public event Action OpenFolderRequested, SettingsRequested, HiddenToTray;
    public event Action<string> OpenFileRequested;

    sealed class Hit { public string Id; public Rectangle R; public Recent Item; }
    sealed class Recent { public string Path; public DateTime Time; public Bitmap Thumb; }

    static readonly int[] Delays = { 0, 3, 5, 10 };
    const int Columns = 4, MaxRecent = 8;

    readonly AppSettings settings;
    readonly List<Hit> hits = new();
    readonly List<Recent> recent = new();
    readonly Font titleFont, tileFont, smallFont, sectionFont, linkFont;
    readonly ContextMenuStrip thumbMenu;

    int delayIndex;
    Hit hover, menuHit;
    bool reallyClose;
    Size cell;

    int S(int v) => Ui.S(this, v);

    public MainWindow(AppSettings settings)
    {
        this.settings = settings;

        Text = "Snapit";
        Icon = TrayApp.AppIcon;
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.None;
        BackColor = Ui.Bg;
        KeyPreview = true;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);

        titleFont = Ui.F(15f, FontStyle.Bold);
        tileFont = Ui.F(10.5f, FontStyle.Bold);
        smallFont = Ui.F(8.5f);
        sectionFont = Ui.F(10f, FontStyle.Bold);
        linkFont = Ui.F(9f);

        thumbMenu = new ContextMenuStrip { Renderer = new ModernMenuRenderer(), ShowImageMargin = false, Font = Ui.F(9.5f) };
        thumbMenu.Items.Add("Annoter…", null, (s, e) => { if (menuHit?.Item != null) OpenFileRequested?.Invoke(menuHit.Item.Path); });
        thumbMenu.Items.Add("Copier", null, (s, e) => CopyFile(menuHit?.Item?.Path));
        thumbMenu.Items.Add("Afficher dans l’Explorateur", null, (s, e) => Reveal(menuHit?.Item?.Path));
        thumbMenu.Items.Add(new ToolStripSeparator());
        thumbMenu.Items.Add("Supprimer", null, (s, e) => DeleteFile(menuHit?.Item?.Path));
        foreach (ToolStripItem it in thumbMenu.Items) it.Padding = new Padding(6, 4, 6, 4);
        thumbMenu.Opened += (s, e) => Native.RoundCorners(thumbMenu.Handle, true);

        DoLayout();
        RefreshRecent();
    }

    public int Delay => Delays[delayIndex];

    /// <summary>Ferme vraiment (quand on quitte Snapit).</summary>
    public void ReallyClose()
    {
        reallyClose = true;
        Close();
    }

    public void ShowAndActivate()
    {
        if (!Visible) Show();
        if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;
        Native.SetForegroundWindow(Handle);
        Activate();
    }

    // ---------- Mise en page ----------

    void DoLayout()
    {
        hits.Clear();
        int W = S(600), pad = S(22), gap = S(12);
        int y = S(18);

        y += S(34); // titre "Snapit"
        y += S(26); // sous-titre

        int tw = (W - pad * 2 - gap * 2) / 3, th = S(122);
        hits.Add(new Hit { Id = "region", R = new Rectangle(pad, y, tw, th) });
        hits.Add(new Hit { Id = "screen", R = new Rectangle(pad + tw + gap, y, tw, th) });
        hits.Add(new Hit { Id = "timer", R = new Rectangle(pad + (tw + gap) * 2, y, tw, th) });
        y += th + S(28);

        // en-tête "Captures récentes" + liens
        int lh = S(26);
        var settingsSize = S(26);
        hits.Add(new Hit { Id = "settings", R = new Rectangle(W - pad - settingsSize, y, settingsSize, lh) });
        int fw = TextRenderer.MeasureText("Ouvrir le dossier", linkFont).Width + S(16);
        hits.Add(new Hit { Id = "folder", R = new Rectangle(W - pad - settingsSize - S(6) - fw, y, fw, lh) });
        sectionY = y;
        y += lh + S(10);

        int cw = (W - pad * 2 - gap * (Columns - 1)) / Columns;
        cell = new Size(cw, (int)(cw * 0.62f));
        gridTop = y;
        int rows = 2;
        for (int i = 0; i < MaxRecent; i++)
        {
            int c = i % Columns, r = i / Columns;
            hits.Add(new Hit { Id = "thumb" + i, R = new Rectangle(pad + c * (cw + gap), y + r * (cell.Height + S(30)), cw, cell.Height) });
        }
        y += rows * (cell.Height + S(30));
        y += S(8);
        footerY = y;
        y += S(30);

        ClientSize = new Size(W, y);
    }

    int sectionY, gridTop, footerY;

    public void RefreshRecent()
    {
        foreach (var r in recent) r.Thumb?.Dispose();
        recent.Clear();
        try
        {
            var dir = new DirectoryInfo(settings.SaveFolder);
            if (dir.Exists)
            {
                var files = dir.EnumerateFiles()
                    .Where(f => f.Extension.Equals(".png", StringComparison.OrdinalIgnoreCase) ||
                                f.Extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(f => f.LastWriteTime)
                    .Take(MaxRecent);
                foreach (var f in files)
                    recent.Add(new Recent { Path = f.FullName, Time = f.LastWriteTime, Thumb = MakeThumb(f.FullName, cell.Width, cell.Height) });
            }
        }
        catch { }

        for (int i = 0; i < MaxRecent; i++)
        {
            var h = hits.FirstOrDefault(x => x.Id == "thumb" + i);
            if (h != null) h.Item = i < recent.Count ? recent[i] : null;
        }
        Invalidate();
    }

    static Bitmap MakeThumb(string path, int w, int h)
    {
        try
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var src = Image.FromStream(fs);
            float k = Math.Max((float)w / src.Width, (float)h / src.Height);
            int sw = Math.Max(1, (int)(w / k)), sh = Math.Max(1, (int)(h / k));
            var srcRect = new Rectangle((src.Width - sw) / 2, 0, sw, sh); // haut de l'image, comme un aperçu Finder
            var b = new Bitmap(w, h);
            using var g = Graphics.FromImage(b);
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.Clear(Color.White);
            g.DrawImage(src, new Rectangle(0, 0, w, h), srcRect, GraphicsUnit.Pixel);
            return b;
        }
        catch { return null; }
    }

    // ---------- Dessin ----------

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        Ui.HighQuality(g);
        g.Clear(BackColor);
        int pad = S(22);

        TextRenderer.DrawText(g, "Snapit", titleFont, new Point(pad - S(2), S(16)), Ui.TextColor);
        TextRenderer.DrawText(g, "Choisissez quoi capturer. La fenêtre se cache pendant la capture puis revient.",
            linkFont, new Point(pad, S(52)), Ui.Secondary);

        foreach (var h in hits)
        {
            switch (h.Id)
            {
                case "region":
                    Tile(g, h, "region", "Zone ou fenêtre", "Ctrl + Maj + 4", false);
                    break;
                case "screen":
                    Tile(g, h, "screen", "Écran entier", "Ctrl + Maj + 3", false);
                    break;
                case "timer":
                    Tile(g, h, "timer", Delay == 0 ? "Minuteur : non" : $"Minuteur : {Delay} s",
                        Delay == 0 ? "Cliquer pour activer" : "Appliqué à la prochaine capture", Delay > 0);
                    break;
                case "folder":
                    if (h == hover) Ui.FillRound(g, Color.FromArgb(228, 228, 234), h.R, S(6));
                    TextRenderer.DrawText(g, "Ouvrir le dossier", linkFont, h.R, Ui.Accent,
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
                    break;
                case "settings":
                    if (h == hover) Ui.FillRound(g, Color.FromArgb(228, 228, 234), h.R, S(6));
                    Glyphs.Draw(g, "gear", RectangleF.Inflate(h.R, -S(4), -S(4)), Ui.Secondary);
                    break;
            }
        }

        TextRenderer.DrawText(g, "Captures récentes", sectionFont,
            new Rectangle(pad, sectionY, S(300), S(26)), Ui.TextColor, TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);

        if (recent.Count == 0)
        {
            var box = new Rectangle(pad, gridTop, ClientSize.Width - pad * 2, cell.Height);
            using (var gp = Ui.Round(box, S(10)))
            using (var dp = new Pen(Color.FromArgb(200, 200, 205), 1.5f) { DashPattern = new[] { 4f, 3f } })
                g.DrawPath(dp, gp);
            TextRenderer.DrawText(g, settings.SaveToFile ? "Vos captures apparaîtront ici" : "L’enregistrement des fichiers est désactivé dans les Réglages",
                linkFont, box, Ui.Secondary, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }
        else
        {
            foreach (var h in hits.Where(x => x.Item != null))
                Thumb(g, h);
        }

        TextRenderer.DrawText(g, "Les raccourcis marchent même quand cette fenêtre est fermée. Échap annule une capture.",
            smallFont, new Rectangle(pad, footerY, ClientSize.Width - pad * 2, S(22)), Ui.Secondary,
            TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
    }

    void Tile(Graphics g, Hit h, string glyph, string title, string sub, bool active)
    {
        bool hot = h == hover;
        var r = h.R;
        Ui.FillRound(g, Color.White, r, S(12));
        using (var gp = Ui.Round(new RectangleF(r.X + .5f, r.Y + .5f, r.Width - 1, r.Height - 1), S(12)))
        using (var p = new Pen(hot || active ? Ui.Accent : Color.FromArgb(222, 222, 228), hot || active ? 2f : 1f))
            g.DrawPath(p, gp);

        int gs = S(34);
        Glyphs.Draw(g, glyph, new RectangleF(r.X + (r.Width - gs) / 2f, r.Y + S(20), gs, gs), hot || active ? Ui.Accent : Color.FromArgb(58, 58, 60));
        TextRenderer.DrawText(g, title, tileFont, new Rectangle(r.X, r.Y + S(64), r.Width, S(24)), Ui.TextColor,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        TextRenderer.DrawText(g, sub, smallFont, new Rectangle(r.X, r.Y + S(88), r.Width, S(20)), Ui.Secondary,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
    }

    void Thumb(Graphics g, Hit h)
    {
        var r = h.R;
        bool hot = h == hover;
        using (var gp = Ui.Round(r, S(8)))
        {
            if (h.Item.Thumb != null)
            {
                g.SetClip(gp);
                g.DrawImage(h.Item.Thumb, r);
                if (hot)
                {
                    using var dark = new SolidBrush(Color.FromArgb(120, 0, 0, 0));
                    g.FillRectangle(dark, r);
                    TextRenderer.DrawText(g, "Annoter", tileFont, r, Color.White,
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                }
                g.ResetClip();
            }
            else
            {
                using var b = new SolidBrush(Color.FromArgb(225, 225, 230));
                g.FillPath(b, gp);
            }
            using var border = new Pen(hot ? Ui.Accent : Color.FromArgb(215, 215, 222), hot ? 2f : 1f);
            g.DrawPath(border, gp);
        }

        string when = h.Item.Time.Date == DateTime.Today ? $"Aujourd’hui à {h.Item.Time:HH:mm}" : $"{h.Item.Time:dd/MM} à {h.Item.Time:HH:mm}";
        TextRenderer.DrawText(g, when, smallFont, new Rectangle(r.X, r.Bottom + S(4), r.Width, S(20)), Ui.Secondary,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.Top | TextFormatFlags.NoPadding);
    }

    // ---------- Souris ----------

    Hit HitTest(Point p) => hits.FirstOrDefault(h => h.R.Contains(p) && (!h.Id.StartsWith("thumb") || h.Item != null));

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var h = HitTest(e.Location);
        if (h == hover) return;
        hover = h;
        Cursor = h != null ? Cursors.Hand : Cursors.Default;
        Invalidate();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        hover = null;
        Invalidate();
    }

    protected override void OnMouseClick(MouseEventArgs e)
    {
        base.OnMouseClick(e);
        var h = HitTest(e.Location);
        if (h == null) return;

        if (e.Button == MouseButtons.Right)
        {
            if (h.Item != null) { menuHit = h; thumbMenu.Show(this, e.Location); }
            return;
        }
        if (e.Button != MouseButtons.Left) return;

        switch (h.Id)
        {
            case "region":
            case "screen":
                CaptureRequested?.Invoke(h.Id, Delay);
                break;
            case "timer":
                delayIndex = (delayIndex + 1) % Delays.Length;
                Invalidate();
                break;
            case "folder": OpenFolderRequested?.Invoke(); break;
            case "settings": SettingsRequested?.Invoke(); break;
            default:
                if (h.Item != null) OpenFileRequested?.Invoke(h.Item.Path);
                break;
        }
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        switch (keyData)
        {
            case Keys.Enter:
            case Keys.Space: CaptureRequested?.Invoke("region", Delay); return true;
            case Keys.Escape: Close(); return true;
        }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    // ---------- Actions sur les vignettes ----------

    void CopyFile(string path)
    {
        if (path == null) return;
        try
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var img = Image.FromStream(fs);
            ClipboardHelper.SetImage(img);
        }
        catch { }
    }

    static void Reveal(string path)
    {
        if (path == null) return;
        try { Process.Start("explorer.exe", "/select,\"" + path + "\""); } catch { }
    }

    void DeleteFile(string path)
    {
        if (path == null) return;
        try { File.Delete(path); } catch { }
        RefreshRecent();
    }

    // ---------- Fermeture ----------

    protected override void OnActivated(EventArgs e)
    {
        base.OnActivated(e);
        RefreshRecent();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (!reallyClose && e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            Hide();
            HiddenToTray?.Invoke();
            return;
        }
        base.OnFormClosing(e);
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        base.OnFormClosed(e);
        foreach (var r in recent) r.Thumb?.Dispose();
        thumbMenu.Dispose();
        titleFont.Dispose(); tileFont.Dispose(); smallFont.Dispose(); sectionFont.Dispose(); linkFont.Dispose();
    }
}
