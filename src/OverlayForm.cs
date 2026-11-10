using System.Drawing.Drawing2D;

namespace Snapit;

/// <summary>
/// Écran figé assombri : glisser = zone, simple clic = fenêtre survolée (ou l'écran si aucune).
/// Échap / clic droit = annuler.
/// </summary>
sealed class OverlayForm : Form
{
    public event Action<Bitmap> Captured;

    const string Hint = "Glissez pour capturer une zone   ·   Cliquez pour une fenêtre   ·   Échap pour annuler";

    readonly Bitmap shot, dim;
    readonly List<Rectangle> wins = new();
    readonly List<Rectangle> screens = new();
    readonly Point origin;
    readonly Font labelFont, hintFont;
    readonly Rectangle hintRect;

    Point start;
    bool pressed, dragging, hintVisible = true;
    Rectangle sel, hover, lastDirty;

    public OverlayForm(Bitmap shot, List<Rectangle> windowRects)
    {
        this.shot = shot;
        var vs = SystemInformation.VirtualScreen;
        origin = vs.Location;

        Text = "Snapit";
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        AutoScaleMode = AutoScaleMode.None;
        Bounds = vs;
        ShowInTaskbar = false;
        TopMost = true;
        KeyPreview = true;
        Cursor = Cursors.Cross;
        BackColor = Color.Black;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.Opaque, true);

        var full = new Rectangle(Point.Empty, vs.Size);
        foreach (var r0 in windowRects)
        {
            var r = r0;
            r.Offset(-origin.X, -origin.Y);
            r.Intersect(full);
            if (r.Width > 8 && r.Height > 8) wins.Add(r);
        }
        foreach (var s in Screen.AllScreens)
        {
            var b = s.Bounds;
            b.Offset(-origin.X, -origin.Y);
            screens.Add(b);
        }

        dim = new Bitmap(shot);
        using (var g = Graphics.FromImage(dim))
        using (var b = new SolidBrush(Color.FromArgb(105, 0, 0, 0)))
            g.FillRectangle(b, full);

        labelFont = Ui.F(9.5f, FontStyle.Bold);
        hintFont = Ui.F(10f);

        var cur = Screen.FromPoint(Cursor.Position).Bounds;
        cur.Offset(-origin.X, -origin.Y);
        var sz = TextRenderer.MeasureText(Hint, hintFont);
        int hw = sz.Width + 40, hh = sz.Height + 20;
        hintRect = new Rectangle(cur.X + (cur.Width - hw) / 2, cur.Y + 48, hw, hh);
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == Native.WM_DPICHANGED) return; // ne jamais redimensionner l'overlay
        base.WndProc(ref m);
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        Native.SetForegroundWindow(Handle);
        Activate();
        Focus();
        UpdateHover(PointToClient(Cursor.Position));
        Invalidate();
    }

    // ---------- Dessin ----------

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        var clip = e.ClipRectangle;
        g.DrawImage(dim, clip, clip, GraphicsUnit.Pixel);

        var focus = dragging ? sel : hover;
        if (focus.Width > 0 && focus.Height > 0)
        {
            var inter = Rectangle.Intersect(focus, clip);
            if (!inter.IsEmpty) g.DrawImage(shot, inter, inter, GraphicsUnit.Pixel);

            if (dragging)
            {
                using var p = new Pen(Color.FromArgb(235, 255, 255, 255), 1);
                g.DrawRectangle(p, focus.X, focus.Y, focus.Width - 1, focus.Height - 1);
                DrawLabel(g);
            }
            else
            {
                using var tint = new SolidBrush(Color.FromArgb(38, Ui.Accent));
                g.FillRectangle(tint, focus);
                using var p = new Pen(Ui.Accent, 3);
                g.DrawRectangle(p, focus.X + 1, focus.Y + 1, focus.Width - 3, focus.Height - 3);
            }
        }

        if (hintVisible && hintRect.IntersectsWith(clip)) DrawHint(g);
    }

    void DrawHint(Graphics g)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        Ui.FillRound(g, Color.FromArgb(215, 30, 30, 32), hintRect, hintRect.Height / 2f);
        g.SmoothingMode = SmoothingMode.None;
        TextRenderer.DrawText(g, Hint, hintFont, hintRect, Color.White,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
    }

    string LabelText => $"{sel.Width} × {sel.Height}";

    Rectangle LabelRect()
    {
        var sz = TextRenderer.MeasureText(LabelText, labelFont);
        int w = sz.Width + 18, h = sz.Height + 8;
        int x = sel.Right - w, y = sel.Bottom + 8;
        if (y + h > ClientSize.Height) y = sel.Bottom - h - 8;
        if (x < 0) x = sel.X;
        return new Rectangle(x, y, w, h);
    }

    void DrawLabel(Graphics g)
    {
        var r = LabelRect();
        g.SmoothingMode = SmoothingMode.AntiAlias;
        Ui.FillRound(g, Color.FromArgb(215, 30, 30, 32), r, r.Height / 2f);
        g.SmoothingMode = SmoothingMode.None;
        TextRenderer.DrawText(g, LabelText, labelFont, r, Color.White,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
    }

    /// <summary>Invalide seulement ce qui a changé pour rester fluide même en 4K multi-écrans.</summary>
    void Repaint()
    {
        var focus = dragging ? sel : hover;
        var d = focus.IsEmpty ? Rectangle.Empty : Rectangle.Inflate(focus, 4, 4);
        if (dragging && !sel.IsEmpty) d = d.IsEmpty ? Rectangle.Inflate(LabelRect(), 2, 2) : Rectangle.Union(d, Rectangle.Inflate(LabelRect(), 2, 2));

        Rectangle u;
        if (lastDirty.IsEmpty) u = d;
        else if (d.IsEmpty) u = lastDirty;
        else u = Rectangle.Union(lastDirty, d);

        if (!u.IsEmpty) Invalidate(u);
        lastDirty = d;
    }

    // ---------- Souris / clavier ----------

    void UpdateHover(Point p)
    {
        Rectangle h = Rectangle.Empty;
        foreach (var w in wins) if (w.Contains(p)) { h = w; break; }
        if (h.IsEmpty) foreach (var s in screens) if (s.Contains(p)) { h = s; break; }
        if (h != hover) { hover = h; Repaint(); }
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            pressed = true;
            start = e.Location;
        }
        else if (e.Button == MouseButtons.Right)
        {
            if (dragging)
            {
                dragging = pressed = false;
                sel = Rectangle.Empty;
                UpdateHover(e.Location);
                Repaint();
            }
            else Close();
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        if (!pressed) { UpdateHover(e.Location); return; }

        if (!dragging && (Math.Abs(e.X - start.X) > 3 || Math.Abs(e.Y - start.Y) > 3))
        {
            dragging = true;
            if (hintVisible) { hintVisible = false; Invalidate(hintRect); }
        }
        if (dragging)
        {
            sel = Rectangle.FromLTRB(Math.Min(start.X, e.X), Math.Min(start.Y, e.Y),
                                     Math.Max(start.X, e.X) + 1, Math.Max(start.Y, e.Y) + 1);
            sel.Intersect(ClientRectangle);
            Repaint();
        }
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left || !pressed) return;
        pressed = false;
        var target = dragging ? sel : hover;
        dragging = false;

        if (target.Width < 3 || target.Height < 3)
        {
            sel = Rectangle.Empty;
            UpdateHover(e.Location);
            Repaint();
            return;
        }
        Finish(target);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Escape) Close();
        base.OnKeyDown(e);
    }

    void Finish(Rectangle r)
    {
        var crop = new Bitmap(r.Width, r.Height);
        using (var g = Graphics.FromImage(crop))
            g.DrawImage(shot, new Rectangle(0, 0, r.Width, r.Height), r, GraphicsUnit.Pixel);
        Hide();
        Captured?.Invoke(crop);
        Close();
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        base.OnFormClosed(e);
        shot.Dispose();
        dim.Dispose();
        labelFont.Dispose();
        hintFont.Dispose();
    }
}
