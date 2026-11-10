using System.Drawing.Drawing2D;

namespace Snapit;

/// <summary>Annotations, stockées en coordonnées de l'image.</summary>
abstract class Shape
{
    public Color Color;
    public float Width;
    public virtual bool IsValid => true;
    public abstract void Draw(Graphics g, Bitmap source);

    protected static RectangleF Norm(PointF a, PointF b) =>
        RectangleF.FromLTRB(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Max(a.X, b.X), Math.Max(a.Y, b.Y));
}

sealed class StrokeShape : Shape
{
    public readonly List<PointF> Points = new();
    public bool Highlighter;

    public void Add(PointF p)
    {
        if (Points.Count > 0)
        {
            var last = Points[^1];
            if (Math.Abs(last.X - p.X) < 0.8f && Math.Abs(last.Y - p.Y) < 0.8f) return;
        }
        Points.Add(p);
    }

    public override void Draw(Graphics g, Bitmap source)
    {
        var c = Highlighter ? Color.FromArgb(110, Color) : Color;
        if (Points.Count == 1)
        {
            using var b = new SolidBrush(c);
            g.FillEllipse(b, Points[0].X - Width / 2, Points[0].Y - Width / 2, Width, Width);
            return;
        }
        var cap = Highlighter ? LineCap.Square : LineCap.Round;
        using var p = new Pen(c, Width) { StartCap = cap, EndCap = cap, LineJoin = LineJoin.Round };
        g.DrawLines(p, Points.ToArray());
    }
}

sealed class ArrowShape : Shape
{
    public PointF A, B;

    float Length => (float)Math.Sqrt((B.X - A.X) * (B.X - A.X) + (B.Y - A.Y) * (B.Y - A.Y));
    public override bool IsValid => Length > Width * 2;

    public override void Draw(Graphics g, Bitmap source)
    {
        float len = Length;
        if (len < 1) return;
        float ux = (B.X - A.X) / len, uy = (B.Y - A.Y) / len;
        float head = Math.Min(len * 0.6f, Width * 4.2f + 4);
        float half = head * 0.62f;
        float bx = B.X - ux * head, by = B.Y - uy * head;
        var p1 = new PointF(bx - uy * half, by + ux * half);
        var p2 = new PointF(bx + uy * half, by - ux * half);

        using (var pen = new Pen(Color, Width) { StartCap = LineCap.Round, EndCap = LineCap.Flat })
            g.DrawLine(pen, A, new PointF(B.X - ux * head * 0.8f, B.Y - uy * head * 0.8f));

        using var gp = new GraphicsPath();
        gp.AddPolygon(new[] { B, p1, p2 });
        using var br = new SolidBrush(Color);
        g.FillPath(br, gp);
        using var edge = new Pen(Color, Math.Max(1f, Width * 0.45f)) { LineJoin = LineJoin.Round };
        g.DrawPath(edge, gp);
    }
}

sealed class RectShape : Shape
{
    public PointF A, B;
    public override bool IsValid { get { var r = Norm(A, B); return r.Width > 3 && r.Height > 3; } }

    public override void Draw(Graphics g, Bitmap source)
    {
        var r = Norm(A, B);
        if (r.Width < 1 || r.Height < 1) return;
        using var gp = Ui.Round(r, Width * 1.5f);
        using var p = new Pen(Color, Width) { LineJoin = LineJoin.Round };
        g.DrawPath(p, gp);
    }
}

sealed class TextShape : Shape
{
    public PointF Position;
    public string Text;
    public float Size;

    public override void Draw(Graphics g, Bitmap source)
    {
        using var ff = new FontFamily(Ui.Family);
        using var gp = new GraphicsPath();
        gp.AddString(Text, ff, (int)FontStyle.Bold, Size, Position, StringFormat.GenericTypographic);
        bool light = Color.GetBrightness() > 0.7f;
        using var outline = new Pen(light ? Color.FromArgb(220, 0, 0, 0) : Color.White, Math.Max(2f, Size / 6f)) { LineJoin = LineJoin.Round };
        g.DrawPath(outline, gp);
        using var b = new SolidBrush(Color);
        g.FillPath(b, gp);
    }
}

/// <summary>Pixellisation (pour masquer des infos sensibles).</summary>
sealed class BlurShape : Shape
{
    public PointF A, B;
    public override bool IsValid { get { var r = Norm(A, B); return r.Width > 3 && r.Height > 3; } }

    public override void Draw(Graphics g, Bitmap source)
    {
        var r = Rectangle.Round(Norm(A, B));
        r.Intersect(new Rectangle(0, 0, source.Width, source.Height));
        if (r.Width < 2 || r.Height < 2) return;

        int block = Math.Max(6, (int)(Math.Max(source.Width, source.Height) / 110f));
        int sw = Math.Max(1, r.Width / block), sh = Math.Max(1, r.Height / block);
        using var small = new Bitmap(sw, sh);
        using (var gs = Graphics.FromImage(small))
        {
            gs.InterpolationMode = InterpolationMode.HighQualityBilinear;
            gs.PixelOffsetMode = PixelOffsetMode.Half;
            gs.DrawImage(source, new Rectangle(0, 0, sw, sh), r, GraphicsUnit.Pixel);
        }

        var im = g.InterpolationMode;
        var pm = g.PixelOffsetMode;
        g.InterpolationMode = InterpolationMode.NearestNeighbor;
        g.PixelOffsetMode = PixelOffsetMode.Half;
        g.DrawImage(small, r, new Rectangle(0, 0, sw, sh), GraphicsUnit.Pixel);
        g.InterpolationMode = im;
        g.PixelOffsetMode = pm;
    }
}
