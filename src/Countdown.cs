namespace Snapit;

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
