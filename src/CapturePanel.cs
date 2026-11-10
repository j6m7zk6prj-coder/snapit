namespace Snapit;

/// <summary>Barre flottante (Ctrl+Maj+5) façon macOS : mode, minuteur, options, Capturer.</summary>
sealed class CapturePanel : Form
{
    public event Action<string, int> RunCapture;
    public event Action OpenSettings, OpenFolder;

    sealed class Btn { public string Id, Glyph, Label; public Rectangle R; }

    static string mode = "region";
    static int delayIndex;
    static readonly int[] Delays = { 0, 3, 5, 10 };

    readonly List<Btn> btns = new();
    readonly Font font, small, bold;
    readonly ContextMenuStrip optMenu;
    Btn hover;
    bool menuOpen;

    int S(int v) => Ui.S(this, v);

    public CapturePanel()
    {
        Text = "Snapit";
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        ShowInTaskbar = false;
        TopMost = true;
        AutoScaleMode = AutoScaleMode.None;
        KeyPreview = true;
        BackColor = Color.FromArgb(30, 30, 32);
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);

        font = Ui.F(9.5f);
        small = Ui.F(8.5f);
        bold = Ui.F(9.5f, FontStyle.Bold);

        btns.Add(new Btn { Id = "close", Glyph = "close" });
        btns.Add(new Btn { Id = "sep" });
        btns.Add(new Btn { Id = "screen", Glyph = "screen", Label = "Écran entier" });
        btns.Add(new Btn { Id = "region", Glyph = "region", Label = "Zone / fenêtre" });
        btns.Add(new Btn { Id = "sep" });
        btns.Add(new Btn { Id = "timer" });
        btns.Add(new Btn { Id = "options", Label = "Options" });
        btns.Add(new Btn { Id = "capture", Label = "Capturer" });
        DoLayoutButtons();

        var wa = Screen.FromPoint(Cursor.Position).WorkingArea;
        Location = new Point(wa.X + (wa.Width - Width) / 2, wa.Bottom - Height - S(36));

        optMenu = new ContextMenuStrip { Renderer = new ModernMenuRenderer(), ShowImageMargin = false, Font = Ui.F(9.5f) };
        optMenu.Items.Add("Ouvrir le dossier des captures", null, (s, e) => { OpenFolder?.Invoke(); Close(); });
        optMenu.Items.Add("Réglages…", null, (s, e) => { OpenSettings?.Invoke(); Close(); });
        foreach (ToolStripItem it in optMenu.Items) it.Padding = new Padding(6, 4, 6, 4);
        optMenu.Opened += (s, e) => Native.RoundCorners(optMenu.Handle, true);
        optMenu.Closed += (s, e) => menuOpen = false;
    }

    string TimerLabel => Delays[delayIndex] == 0 ? "Minuteur : non" : $"Minuteur : {Delays[delayIndex]} s";

    void DoLayoutButtons()
    {
        int h = S(70), pad = S(9), x = S(10);
        int timerW = TextRenderer.MeasureText("Minuteur : 10 s", font).Width + S(28);
        foreach (var b in btns)
        {
            int w = b.Id switch
            {
                "close" => S(40),
                "sep" => S(15),
                "screen" or "region" => S(96),
                "timer" => timerW,
                _ => TextRenderer.MeasureText(b.Label, bold).Width + S(32),
            };
            if (b.Id == "close") b.R = new Rectangle(x, (h - S(32)) / 2, S(32), S(32));
            else if (b.Id is "options" or "capture" or "timer") b.R = new Rectangle(x, (h - S(34)) / 2, w, S(34));
            else b.R = new Rectangle(x, pad, w, h - pad * 2);
            x += w + S(4);
        }
        ClientSize = new Size(x + S(6), h);
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        Native.RoundCorners(Handle);
    }

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= Native.WS_EX_TOOLWINDOW;
            cp.ClassStyle |= Native.CS_DROPSHADOW;
            return cp;
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        Ui.HighQuality(g);
        g.Clear(BackColor);

        var white = Color.FromArgb(242, 242, 247);
        foreach (var b in btns)
        {
            bool hot = b == hover;
            var r = b.R;
            switch (b.Id)
            {
                case "sep":
                    using (var p = new Pen(Color.FromArgb(72, 72, 74)))
                        g.DrawLine(p, r.X + r.Width / 2, S(20), r.X + r.Width / 2, Height - S(20));
                    break;

                case "close":
                    using (var cb = new SolidBrush(hot ? Color.FromArgb(72, 72, 74) : Color.FromArgb(58, 58, 60)))
                        g.FillEllipse(cb, r);
                    Glyphs.Draw(g, "close", RectangleF.Inflate(r, -S(9), -S(9)), white);
                    break;

                case "screen":
                case "region":
                {
                    bool sel = mode == b.Id;
                    if (sel) Ui.FillRound(g, Color.FromArgb(70, 70, 74), r, S(9));
                    else if (hot) Ui.FillRound(g, Color.FromArgb(50, 50, 53), r, S(9));
                    var gr = new RectangleF(r.X + (r.Width - S(26)) / 2f, r.Y + S(6), S(26), S(26));
                    Glyphs.Draw(g, b.Glyph, gr, sel ? Color.White : Color.FromArgb(200, 200, 205));
                    var lr = new Rectangle(r.X, r.Y + S(33), r.Width, r.Height - S(35));
                    TextRenderer.DrawText(g, b.Label, small, lr, sel ? Color.White : Color.FromArgb(200, 200, 205),
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.Top | TextFormatFlags.NoPadding);
                    break;
                }

                case "timer":
                case "options":
                    if (hot) Ui.FillRound(g, Color.FromArgb(58, 58, 60), r, S(8));
                    TextRenderer.DrawText(g, b.Id == "timer" ? TimerLabel : b.Label, font, r, white,
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
                    break;

                case "capture":
                    Ui.FillRound(g, hot ? Color.FromArgb(40, 150, 255) : Ui.Accent, r, S(8));
                    TextRenderer.DrawText(g, b.Label, bold, r, Color.White,
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
                    break;
            }
        }
    }

    Btn HitTest(Point p) => btns.FirstOrDefault(b => b.Id != "sep" && b.R.Contains(p));

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
        if (e.Button != MouseButtons.Left) return;
        var b = HitTest(e.Location);
        if (b == null) return;
        switch (b.Id)
        {
            case "close": Close(); break;
            case "screen":
            case "region": mode = b.Id; Invalidate(); break;
            case "timer": delayIndex = (delayIndex + 1) % Delays.Length; Invalidate(); break;
            case "options":
                menuOpen = true;
                optMenu.Show(PointToScreen(new Point(b.R.X, b.R.Y - S(6))), ToolStripDropDownDirection.AboveRight);
                break;
            case "capture": Fire(); break;
        }
    }

    void Fire()
    {
        string m = mode;
        int d = Delays[delayIndex];
        Close();
        RunCapture?.Invoke(m, d);
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        switch (keyData)
        {
            case Keys.Escape: Close(); return true;
            case Keys.Enter:
            case Keys.Space: Fire(); return true;
        }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    protected override void OnDeactivate(EventArgs e)
    {
        base.OnDeactivate(e);
        if (!menuOpen && !IsDisposed) BeginInvoke(new Action(() => { if (!menuOpen && !IsDisposed && !ContainsFocus) Close(); }));
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        base.OnFormClosed(e);
        font.Dispose(); small.Dispose(); bold.Dispose();
    }
}

/// <summary>Compte à rebours discret au centre de l'écran.</summary>
sealed class Countdown : Form
{
    readonly Action done;
    readonly System.Windows.Forms.Timer timer;
    readonly Font big, caption;
    int left;

    int S(int v) => Ui.S(this, v);

    public static void Run(int seconds, Action done) => new Countdown(seconds, done).Show();

    Countdown(int seconds, Action done)
    {
        left = seconds;
        this.done = done;
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        ShowInTaskbar = false;
        AutoScaleMode = AutoScaleMode.None;
        BackColor = Color.FromArgb(30, 30, 32);
        Opacity = 0.92;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
        Size = new Size(S(132), S(132));
        var b = Screen.FromPoint(Cursor.Position).Bounds;
        Location = new Point(b.X + (b.Width - Width) / 2, b.Y + (b.Height - Height) / 2);
        big = Ui.F(40f, FontStyle.Bold);
        caption = Ui.F(9f);
        timer = new System.Windows.Forms.Timer { Interval = 1000 };
        timer.Tick += (s, e) =>
        {
            left--;
            if (left > 0) { Invalidate(); return; }
            timer.Stop();
            Close();
            TrayApp.Delay(180, done);
        };
    }

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= Native.WS_EX_TOOLWINDOW | Native.WS_EX_NOACTIVATE | Native.WS_EX_TOPMOST | Native.WS_EX_TRANSPARENT;
            return cp;
        }
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        Native.RoundCorners(Handle);
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        timer.Start();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(BackColor);
        var top = new Rectangle(0, S(14), Width, S(70));
        TextRenderer.DrawText(g, left.ToString(), big, top, Color.White,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        var bottom = new Rectangle(0, S(88), Width, S(26));
        TextRenderer.DrawText(g, "Capture…", caption, bottom, Color.FromArgb(200, 200, 205),
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        base.OnFormClosed(e);
        timer.Dispose();
        big.Dispose();
        caption.Dispose();
    }
}
