using System.Diagnostics;

namespace Snapit;

/// <summary>
/// Vignette flottante en bas à droite (comme sur macOS) :
/// clic = annoter, glisser = déposer le fichier ailleurs, clic droit = options.
/// Disparaît seule après quelques secondes.
/// </summary>
sealed class PreviewToast : Form
{
    public event Action<Bitmap, string> OpenRequested;

    const int LifeMs = 6000;

    readonly Bitmap img;
    readonly string path;
    readonly System.Windows.Forms.Timer life, anim;
    readonly Rectangle closeRect;
    readonly Point target;
    readonly int slide;

    Point down;
    bool mouseDown, hovering, closing, menuOpen;
    float animT;

    int S(int v) => Ui.S(this, v);

    public PreviewToast(Bitmap img, string path)
    {
        this.img = img;
        this.path = path;

        Text = "";
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        ShowInTaskbar = false;
        AutoScaleMode = AutoScaleMode.None;
        BackColor = Color.White;
        Cursor = Cursors.Hand;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);

        int maxW = S(230), maxH = S(170);
        float k = Math.Min(1f, Math.Min((float)maxW / img.Width, (float)maxH / img.Height));
        int w = Math.Max(S(90), (int)(img.Width * k));
        int h = Math.Max(S(60), (int)(img.Height * k));
        int pad = S(5);
        Size = new Size(w + pad * 2, h + pad * 2);

        var wa = Screen.FromPoint(Cursor.Position).WorkingArea;
        target = new Point(wa.Right - Width - S(20), wa.Bottom - Height - S(20));
        slide = S(36);
        Location = new Point(target.X + slide, target.Y);
        Opacity = 0;

        closeRect = new Rectangle(S(11), S(11), S(22), S(22));

        life = new System.Windows.Forms.Timer { Interval = LifeMs };
        life.Tick += (s, e) => BeginClose();
        anim = new System.Windows.Forms.Timer { Interval = 15 };
        anim.Tick += AnimTick;

        var menu = new ContextMenuStrip { Renderer = new ModernMenuRenderer(), ShowImageMargin = false, Font = Ui.F(9.5f) };
        menu.Items.Add("Annoter…", null, (s, e) => Open());
        var reveal = menu.Items.Add("Afficher dans l’Explorateur", null, (s, e) => Reveal());
        menu.Items.Add("Copier", null, (s, e) => ClipboardHelper.SetImage(img));
        menu.Items.Add(new ToolStripSeparator());
        var del = menu.Items.Add("Supprimer", null, (s, e) => Delete());
        menu.Items.Add("Fermer", null, (s, e) => BeginClose());
        reveal.Enabled = del.Enabled = path != null;
        foreach (ToolStripItem it in menu.Items) it.Padding = new Padding(6, 4, 6, 4);
        menu.Opened += (s, e) => { menuOpen = true; life.Stop(); Native.RoundCorners(menu.Handle, true); };
        menu.Closed += (s, e) => { menuOpen = false; if (!closing && !hovering) life.Start(); };
        ContextMenuStrip = menu;
    }

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= Native.WS_EX_TOOLWINDOW | Native.WS_EX_NOACTIVATE | Native.WS_EX_TOPMOST;
            cp.ClassStyle |= Native.CS_DROPSHADOW;
            return cp;
        }
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == Native.WM_DPICHANGED) return;
        base.WndProc(ref m);
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        Native.RoundCorners(Handle);
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        animT = 0;
        anim.Start();
        life.Start();
    }

    void AnimTick(object sender, EventArgs e)
    {
        animT = Math.Min(1f, animT + 15f / 240f);
        float ease = 1 - (float)Math.Pow(1 - animT, 3);
        if (!closing)
        {
            Left = target.X + (int)(slide * (1 - ease));
            Opacity = ease;
        }
        else
        {
            Left = target.X + (int)(slide * ease);
            Opacity = 1 - ease;
        }
        if (animT >= 1)
        {
            anim.Stop();
            if (closing) Close();
        }
    }

    void BeginClose()
    {
        if (closing || IsDisposed) return;
        closing = true;
        life.Stop();
        animT = 0;
        anim.Start();
    }

    void Open()
    {
        OpenRequested?.Invoke(new Bitmap(img), path);
        Close();
    }

    void Reveal()
    {
        if (path == null) return;
        try { Process.Start("explorer.exe", "/select,\"" + path + "\""); } catch { }
        BeginClose();
    }

    void Delete()
    {
        try { if (path != null && File.Exists(path)) File.Delete(path); } catch { }
        BeginClose();
    }

    // ---------- Dessin ----------

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        Ui.HighQuality(g);
        g.Clear(Color.White);

        int pad = S(5);
        var r = new Rectangle(pad, pad, Width - pad * 2, Height - pad * 2);
        using (var gp = Ui.Round(r, S(6)))
        {
            g.SetClip(gp);
            g.DrawImage(img, r);
            g.ResetClip();
        }
        using (var border = new Pen(Color.FromArgb(45, 0, 0, 0)))
            g.DrawRectangle(border, 0, 0, Width - 1, Height - 1);

        if (hovering)
        {
            bool overClose = closeRect.Contains(PointToClient(Cursor.Position));
            using var b = new SolidBrush(Color.FromArgb(overClose ? 235 : 185, 35, 35, 38));
            g.FillEllipse(b, closeRect);
            Glyphs.Draw(g, "close", RectangleF.Inflate(closeRect, -S(5), -S(5)), Color.White);
        }
    }

    // ---------- Souris ----------

    protected override void OnMouseEnter(EventArgs e)
    {
        base.OnMouseEnter(e);
        hovering = true;
        life.Stop();
        Invalidate();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        hovering = false;
        if (!closing && !menuOpen) { life.Stop(); life.Start(); }
        Invalidate();
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button == MouseButtons.Left) { mouseDown = true; down = e.Location; }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (mouseDown && path != null &&
            (Math.Abs(e.X - down.X) > SystemInformation.DragSize.Width || Math.Abs(e.Y - down.Y) > SystemInformation.DragSize.Height))
        {
            mouseDown = false;
            var data = new DataObject();
            data.SetData(DataFormats.FileDrop, new[] { path });
            DoDragDrop(data, DragDropEffects.Copy | DragDropEffects.Move);
            BeginClose();
            return;
        }
        if (hovering) Invalidate(Rectangle.Inflate(closeRect, 2, 2));
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (e.Button != MouseButtons.Left || !mouseDown) return;
        mouseDown = false;
        if (closeRect.Contains(e.Location)) BeginClose();
        else Open();
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        base.OnFormClosed(e);
        life.Dispose();
        anim.Dispose();
        img.Dispose();
    }
}
