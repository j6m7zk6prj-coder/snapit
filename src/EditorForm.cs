using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace Snapit;

/// <summary>Fenêtre d'annotation : stylo, surligneur, flèche, rectangle, texte, flou.</summary>
sealed class EditorForm : Form
{
    readonly string path;
    readonly AppSettings settings;
    readonly EditorToolbar bar;
    readonly Canvas canvas;

    int S(int v) => Ui.S(this, v);

    public EditorForm(Bitmap img, string path, AppSettings settings)
    {
        this.path = path;
        this.settings = settings;

        Text = path != null ? Path.GetFileNameWithoutExtension(path) : "Capture d’écran";
        Icon = TrayApp.AppIcon;
        BackColor = Ui.Bg;
        AutoScaleMode = AutoScaleMode.None;
        StartPosition = FormStartPosition.Manual;
        KeyPreview = true;

        canvas = new Canvas(img) { Dock = DockStyle.Fill };
        bar = new EditorToolbar { Dock = DockStyle.Top };
        bar.Height = S(56);
        Controls.Add(canvas);
        Controls.Add(bar);

        bar.Clicked += OnBarClick;
        canvas.Tool = bar.Tool;
        canvas.Color = bar.Color;

        var wa = Screen.FromPoint(Cursor.Position).WorkingArea;
        int w = Math.Min(Math.Max(img.Width + S(100), S(780)), (int)(wa.Width * 0.88));
        int h = Math.Min(Math.Max(img.Height + S(56) + S(120), S(540)), (int)(wa.Height * 0.88));
        Bounds = new Rectangle(wa.X + (wa.Width - w) / 2, wa.Y + (wa.Height - h) / 2, w, h);
        MinimumSize = new Size(S(700), S(420));
    }

    void OnBarClick(BarItem item)
    {
        switch (item.Kind)
        {
            case ItemKind.Tool: SelectTool(item.Id); break;
            case ItemKind.Color: canvas.Color = item.Color; canvas.Focus(); break;
            default: Do(item.Id); break;
        }
    }

    void SelectTool(string id)
    {
        bar.Tool = id;
        bar.Invalidate();
        canvas.Tool = id;
        canvas.UpdateCursor();
        canvas.Focus();
    }

    void Do(string id)
    {
        switch (id)
        {
            case "undo":
                canvas.Undo();
                break;
            case "copy":
                using (var b = canvas.Render()) ClipboardHelper.SetImage(b);
                canvas.ShowToast("Copié dans le presse-papiers");
                break;
            case "save":
                SaveAs();
                break;
            case "done":
                Close();
                break;
        }
    }

    void SaveAs()
    {
        canvas.CommitText();
        using var dlg = new SaveFileDialog
        {
            Filter = "Image PNG (*.png)|*.png|Image JPEG (*.jpg)|*.jpg",
            FileName = path != null ? Path.GetFileName(path) : "Capture d’écran.png",
            InitialDirectory = path != null ? Path.GetDirectoryName(path) : settings.SaveFolder,
            AddExtension = true,
            DefaultExt = "png",
        };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            using var b = canvas.Render();
            string ext = Path.GetExtension(dlg.FileName).ToLowerInvariant();
            b.Save(dlg.FileName, ext == ".jpg" || ext == ".jpeg" ? ImageFormat.Jpeg : ImageFormat.Png);
            canvas.ShowToast("Enregistré");
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Snapit", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (canvas.IsEditingText) return base.ProcessCmdKey(ref msg, keyData);
        switch (keyData)
        {
            case Keys.Control | Keys.Z: Do("undo"); return true;
            case Keys.Control | Keys.C: Do("copy"); return true;
            case Keys.Control | Keys.S: Do("save"); return true;
            case Keys.Escape:
            case Keys.Enter: Close(); return true;
            case Keys.P: SelectTool("pen"); return true;
            case Keys.H: SelectTool("marker"); return true;
            case Keys.A: SelectTool("arrow"); return true;
            case Keys.R: SelectTool("rect"); return true;
            case Keys.T: SelectTool("text"); return true;
            case Keys.B: SelectTool("blur"); return true;
        }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    /// <summary>Comme sur Mac : fermer = garder les modifications (fichier mis à jour + presse-papiers).</summary>
    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        canvas.CommitText();
        if (canvas.HasChanges)
        {
            try
            {
                using var b = canvas.Render();
                if (path != null) b.Save(path, ImageFormat.Png);
                if (settings.CopyToClipboard) ClipboardHelper.SetImage(b);
            }
            catch { }
        }
        base.OnFormClosing(e);
    }
}

// ======================================================================
//  Barre d'outils
// ======================================================================

enum ItemKind { Tool, Color, Action, Primary, Separator }

sealed class BarItem
{
    public ItemKind Kind;
    public string Id, Tip, Label;
    public Color Color;
    public Rectangle R;
}

sealed class EditorToolbar : Control
{
    public event Action<BarItem> Clicked;
    public string Tool = "pen";
    public Color Color;

    readonly List<BarItem> items = new();
    readonly ToolTip tip = new() { InitialDelay = 400, ShowAlways = true };
    readonly Font font;
    BarItem hover;

    static readonly Color[] Palette =
    {
        Color.FromArgb(255, 59, 48), Color.FromArgb(255, 149, 0), Color.FromArgb(255, 204, 0),
        Color.FromArgb(52, 199, 89), Color.FromArgb(0, 122, 255), Color.FromArgb(29, 29, 31),
    };

    int S(int v) => Ui.S(this, v);

    public EditorToolbar()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        BackColor = Color.White;
        font = Ui.F(9.5f, FontStyle.Bold);
        Color = Palette[0];

        void AddTool(string id, string t) => items.Add(new BarItem { Kind = ItemKind.Tool, Id = id, Tip = t });
        AddTool("pen", "Stylo (P)");
        AddTool("marker", "Surligneur (H)");
        AddTool("arrow", "Flèche (A)");
        AddTool("rect", "Rectangle (R)");
        AddTool("text", "Texte (T)");
        AddTool("blur", "Flouter (B)");
        items.Add(new BarItem { Kind = ItemKind.Separator });
        foreach (var c in Palette) items.Add(new BarItem { Kind = ItemKind.Color, Color = c, Id = "color", Tip = "Couleur" });

        items.Add(new BarItem { Kind = ItemKind.Action, Id = "undo", Tip = "Annuler (Ctrl+Z)" });
        items.Add(new BarItem { Kind = ItemKind.Action, Id = "copy", Tip = "Copier (Ctrl+C)" });
        items.Add(new BarItem { Kind = ItemKind.Action, Id = "save", Tip = "Enregistrer sous… (Ctrl+S)" });
        items.Add(new BarItem { Kind = ItemKind.Primary, Id = "done", Label = "Terminé", Tip = "Terminé (Entrée)" });
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        DoLayout();
    }

    void DoLayout()
    {
        int btn = S(36), gap = S(4), cy = Height / 2;
        int x = S(14);
        foreach (var it in items)
        {
            if (it.Kind == ItemKind.Tool) { it.R = new Rectangle(x, cy - btn / 2, btn, btn); x += btn + gap; }
            else if (it.Kind == ItemKind.Separator) { it.R = new Rectangle(x + S(8), cy - S(12), 1, S(24)); x += S(17); }
            else if (it.Kind == ItemKind.Color) { int d = S(24); it.R = new Rectangle(x, cy - d / 2, d, d); x += d + S(8); }
        }

        // à droite : [annuler][copier][enregistrer]  [Terminé]
        int right = Width - S(14);
        var done = items.First(i => i.Kind == ItemKind.Primary);
        int dw = TextRenderer.MeasureText(done.Label, font).Width + S(30);
        done.R = new Rectangle(right - dw, cy - S(16), dw, S(32));
        right = done.R.X - S(10);
        foreach (var it in items.Where(i => i.Kind == ItemKind.Action).Reverse())
        {
            it.R = new Rectangle(right - btn, cy - btn / 2, btn, btn);
            right -= btn + gap;
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        Ui.HighQuality(g);
        g.Clear(BackColor);
        using (var line = new Pen(Ui.Separator)) g.DrawLine(line, 0, Height - 1, Width, Height - 1);

        var iconColor = Color.FromArgb(58, 58, 60);
        foreach (var it in items)
        {
            var r = it.R;
            bool hot = it == hover;
            switch (it.Kind)
            {
                case ItemKind.Tool:
                case ItemKind.Action:
                {
                    bool active = it.Kind == ItemKind.Tool && it.Id == Tool;
                    if (active) Ui.FillRound(g, Color.FromArgb(225, 238, 255), r, S(8));
                    else if (hot) Ui.FillRound(g, Ui.Bg, r, S(8));
                    var gr = RectangleF.Inflate(r, -S(8), -S(8));
                    Glyphs.Draw(g, it.Id, gr, active ? Ui.Accent : iconColor);
                    break;
                }
                case ItemKind.Separator:
                    using (var p = new Pen(Ui.Separator)) g.DrawLine(p, r.X, r.Y, r.X, r.Bottom);
                    break;
                case ItemKind.Color:
                {
                    bool selected = it.Color.ToArgb() == Color.ToArgb();
                    var inner = selected ? RectangleF.Inflate(r, -S(5), -S(5)) : RectangleF.Inflate(r, -S(3), -S(3));
                    if (hot && !selected) inner = RectangleF.Inflate(r, -S(2), -S(2));
                    using (var b = new SolidBrush(it.Color)) g.FillEllipse(b, inner);
                    if (selected)
                        using (var p = new Pen(it.Color, Math.Max(2, S(2))))
                            g.DrawEllipse(p, RectangleF.Inflate(r, -1, -1));
                    break;
                }
                case ItemKind.Primary:
                {
                    var c = hot ? Color.FromArgb(0, 112, 235) : Ui.Accent;
                    Ui.FillRound(g, c, r, r.Height / 2f);
                    TextRenderer.DrawText(g, it.Label, font, r, Color.White,
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
                    break;
                }
            }
        }
    }

    BarItem HitTest(Point p) => items.FirstOrDefault(i => i.Kind != ItemKind.Separator && i.R.Contains(p));

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var h = HitTest(e.Location);
        if (h == hover) return;
        hover = h;
        Cursor = h != null ? Cursors.Hand : Cursors.Default;
        tip.SetToolTip(this, h?.Tip ?? "");
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
        var h = HitTest(e.Location);
        if (h == null) return;
        if (h.Kind == ItemKind.Color) Color = h.Color;
        Invalidate();
        Clicked?.Invoke(h);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) { tip.Dispose(); font.Dispose(); }
        base.Dispose(disposing);
    }
}

// ======================================================================
//  Zone de dessin
// ======================================================================

sealed class Canvas : Control
{
    public string Tool = "pen";
    public Color Color = Color.Red;

    readonly Bitmap img;
    readonly List<Shape> shapes = new();
    readonly float baseWidth;
    readonly System.Windows.Forms.Timer toastTimer;

    Shape current;
    RectangleF dest;
    float scale = 1;

    TextBox textBox;
    PointF textPos;
    Color textColor;
    float textSize;

    string toastMsg;

    public bool IsEditingText => textBox != null;
    public bool HasChanges => shapes.Count > 0;

    int S(int v) => Ui.S(this, v);

    public Canvas(Bitmap img)
    {
        this.img = img;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
        BackColor = Ui.Bg;
        baseWidth = Math.Max(3f, Math.Max(img.Width, img.Height) / 360f);
        toastTimer = new System.Windows.Forms.Timer { Interval = 1600 };
        toastTimer.Tick += (s, e) => { toastTimer.Stop(); toastMsg = null; Invalidate(); };
        UpdateCursor();
    }

    public void UpdateCursor() => Cursor = Tool == "text" ? Cursors.IBeam : Cursors.Cross;

    void Fit()
    {
        float m = S(36);
        float aw = Math.Max(1, Width - 2 * m), ah = Math.Max(1, Height - 2 * m);
        scale = Math.Min(1f, Math.Min(aw / img.Width, ah / img.Height));
        float w = img.Width * scale, h = img.Height * scale;
        dest = new RectangleF((float)Math.Round((Width - w) / 2f), (float)Math.Round((Height - h) / 2f), w, h);
    }

    PointF ToImage(Point p) => new PointF((p.X - dest.X) / scale, (p.Y - dest.Y) / scale);

    protected override void OnPaint(PaintEventArgs e)
    {
        Fit();
        var g = e.Graphics;
        g.Clear(BackColor);
        g.SmoothingMode = SmoothingMode.AntiAlias;

        // ombre douce sous l'image
        for (int i = 1; i <= 8; i++)
        {
            using var sb = new SolidBrush(Color.FromArgb(9 - i, 0, 0, 0));
            var sr = RectangleF.Inflate(dest, i, i);
            sr.Offset(0, i / 2f);
            g.FillRectangle(sb, sr);
        }

        g.InterpolationMode = scale < 1 ? InterpolationMode.HighQualityBicubic : InterpolationMode.NearestNeighbor;
        g.PixelOffsetMode = PixelOffsetMode.Half;
        g.DrawImage(img, dest);

        var state = g.Save();
        g.SetClip(dest);
        g.TranslateTransform(dest.X, dest.Y);
        g.ScaleTransform(scale, scale);
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        foreach (var s in shapes) s.Draw(g, img);
        current?.Draw(g, img);
        if (current is BlurShape bs)
        {
            using var dp = new Pen(Color.FromArgb(160, 0, 0, 0), 1 / scale) { DashPattern = new[] { 4f, 3f } };
            var r = RectangleF.FromLTRB(Math.Min(bs.A.X, bs.B.X), Math.Min(bs.A.Y, bs.B.Y), Math.Max(bs.A.X, bs.B.X), Math.Max(bs.A.Y, bs.B.Y));
            g.DrawRectangle(dp, r.X, r.Y, r.Width, r.Height);
        }
        g.Restore(state);

        if (toastMsg != null)
        {
            using var f = Ui.F(10f, FontStyle.Bold);
            var sz = TextRenderer.MeasureText(toastMsg, f);
            var r = new Rectangle((Width - sz.Width - S(36)) / 2, Height - S(64), sz.Width + S(36), sz.Height + S(16));
            Ui.FillRound(g, Color.FromArgb(225, 30, 30, 32), r, r.Height / 2f);
            TextRenderer.DrawText(g, toastMsg, f, r, Color.White,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        }
    }

    public void ShowToast(string msg)
    {
        toastMsg = msg;
        toastTimer.Stop();
        toastTimer.Start();
        Invalidate();
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left) return;
        if (textBox != null) { CommitText(); return; }
        Focus();

        var p = ToImage(e.Location);
        switch (Tool)
        {
            case "pen":
            {
                var s = new StrokeShape { Color = Color, Width = baseWidth };
                s.Add(p); current = s; break;
            }
            case "marker":
            {
                var s = new StrokeShape { Color = Color, Width = baseWidth * 5.5f, Highlighter = true };
                s.Add(p); current = s; break;
            }
            case "arrow": current = new ArrowShape { Color = Color, Width = baseWidth * 1.3f, A = p, B = p }; break;
            case "rect": current = new RectShape { Color = Color, Width = baseWidth, A = p, B = p }; break;
            case "blur": current = new BlurShape { A = p, B = p }; break;
            case "text": StartText(e.Location, p); break;
        }
        Invalidate();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (current == null) return;
        var p = ToImage(e.Location);
        switch (current)
        {
            case StrokeShape s: s.Add(p); break;
            case ArrowShape a: a.B = p; break;
            case RectShape r: r.B = p; break;
            case BlurShape b: b.B = p; break;
        }
        Invalidate();
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (current == null) return;
        if (current.IsValid) shapes.Add(current);
        current = null;
        Invalidate();
    }

    // ---------- Texte ----------

    void StartText(Point screenPt, PointF imgPt)
    {
        textPos = imgPt;
        textColor = Color;
        textSize = Math.Max(18f, Math.Max(img.Width, img.Height) / 42f);
        float shown = Math.Max(10f, textSize * scale);

        textBox = new TextBox
        {
            BorderStyle = BorderStyle.None,
            Font = new Font(Ui.Family, shown, FontStyle.Bold, GraphicsUnit.Pixel),
            ForeColor = textColor,
            BackColor = Color.White,
            Width = S(80),
        };
        textBox.Location = new Point(screenPt.X, screenPt.Y - textBox.Height / 2);
        textBox.TextChanged += (s, e) =>
        {
            if (textBox == null) return;
            textBox.Width = Math.Max(S(80), TextRenderer.MeasureText(textBox.Text + "WW", textBox.Font).Width);
        };
        textBox.KeyDown += (s, e) =>
        {
            if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; CommitText(); }
            else if (e.KeyCode == Keys.Escape) { e.SuppressKeyPress = true; CancelText(); }
        };
        textBox.LostFocus += (s, e) => CommitText();
        Controls.Add(textBox);
        textBox.Focus();
    }

    public void CommitText()
    {
        if (textBox == null) return;
        var tb = textBox;
        textBox = null;
        string t = tb.Text.Trim();
        // position : centre vertical sous le clic, comme dans la zone de saisie
        var pos = new PointF(textPos.X, textPos.Y - textSize * 0.62f);
        Controls.Remove(tb);
        tb.Dispose();
        if (t.Length > 0)
            shapes.Add(new TextShape { Color = textColor, Text = t, Size = textSize, Position = pos });
        Focus();
        Invalidate();
    }

    void CancelText()
    {
        if (textBox == null) return;
        var tb = textBox;
        textBox = null;
        Controls.Remove(tb);
        tb.Dispose();
        Focus();
    }

    // ---------- Actions ----------

    public void Undo()
    {
        CommitText();
        if (shapes.Count > 0) shapes.RemoveAt(shapes.Count - 1);
        Invalidate();
    }

    public Bitmap Render()
    {
        var b = new Bitmap(img.Width, img.Height, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(b);
        g.DrawImage(img, 0, 0, img.Width, img.Height);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
        foreach (var s in shapes) s.Draw(g, img);
        return b;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) { toastTimer.Dispose(); img.Dispose(); }
        base.Dispose(disposing);
    }
}
