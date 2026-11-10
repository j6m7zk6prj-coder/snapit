using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace Snapit;

static class Ui
{
    public static readonly Color Accent = Color.FromArgb(10, 132, 255);
    public static readonly Color Bg = Color.FromArgb(242, 242, 247);
    public static readonly Color TextColor = Color.FromArgb(29, 29, 31);
    public static readonly Color Secondary = Color.FromArgb(110, 110, 115);
    public static readonly Color Separator = Color.FromArgb(229, 229, 234);

    static string family;
    public static string Family
    {
        get
        {
            if (family == null)
            {
                var names = FontFamily.Families.Select(f => f.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
                family = names.Contains("Segoe UI Variable Text") ? "Segoe UI Variable Text" : "Segoe UI";
            }
            return family;
        }
    }

    public static Font F(float size, FontStyle style = FontStyle.Regular) => new Font(Family, size, style);

    public static int S(Control c, int v) => (int)Math.Round(v * c.DeviceDpi / 96.0);

    public static GraphicsPath Round(RectangleF r, float radius)
    {
        var p = new GraphicsPath();
        radius = Math.Min(radius, Math.Min(r.Width, r.Height) / 2f);
        if (radius <= 0.5f) { p.AddRectangle(r); return p; }
        float d = radius * 2;
        p.AddArc(r.X, r.Y, d, d, 180, 90);
        p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }

    public static void HighQuality(Graphics g)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
    }

    public static void FillRound(Graphics g, Color c, RectangleF r, float radius)
    {
        using var path = Round(r, radius);
        using var b = new SolidBrush(c);
        g.FillPath(b, path);
    }
}

/// <summary>Menu contextuel épuré façon macOS (sélection bleue, texte blanc).</summary>
sealed class ModernMenuRenderer : ToolStripProfessionalRenderer
{
    public ModernMenuRenderer() { RoundedEdges = false; }

    protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e) => e.Graphics.Clear(Color.White);

    protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
    {
        using var p = new Pen(Color.FromArgb(215, 215, 220));
        e.Graphics.DrawRectangle(p, 0, 0, e.ToolStrip.Width - 1, e.ToolStrip.Height - 1);
    }

    protected override void OnRenderImageMargin(ToolStripRenderEventArgs e) { }

    protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
    {
        if (!e.Item.Selected || !e.Item.Enabled) return;
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        Ui.FillRound(e.Graphics, Ui.Accent, new RectangleF(4, 1, e.Item.Width - 8, e.Item.Height - 2), 5);
    }

    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
    {
        bool isShortcut = e.Item is ToolStripMenuItem mi && !string.IsNullOrEmpty(mi.ShortcutKeyDisplayString) && e.Text == mi.ShortcutKeyDisplayString;
        if (!e.Item.Enabled) e.TextColor = Color.FromArgb(170, 170, 175);
        else if (e.Item.Selected) e.TextColor = Color.White;
        else e.TextColor = isShortcut ? Ui.Secondary : Ui.TextColor;
        base.OnRenderItemText(e);
    }

    protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
    {
        int y = e.Item.Height / 2;
        using var p = new Pen(Ui.Separator);
        e.Graphics.DrawLine(p, 10, y, e.Item.Width - 10, y);
    }
}
