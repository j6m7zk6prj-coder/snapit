using System.Drawing.Drawing2D;

namespace Snapit;

/// <summary>Petites icônes vectorielles dessinées à la main (aucune dépendance de police).</summary>
static class Glyphs
{
    public static void Draw(Graphics g, string name, RectangleF r, Color c)
    {
        float s = Math.Min(r.Width, r.Height);
        float x = r.X + (r.Width - s) / 2f, y = r.Y + (r.Height - s) / 2f;
        float lw = Math.Max(1.5f, s / 11f);
        var oldMode = g.SmoothingMode;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        using var p = new Pen(c, lw) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
        using var b = new SolidBrush(c);

        PointF P(float fx, float fy) => new PointF(x + fx * s, y + fy * s);
        RectangleF R(float fx, float fy, float fw, float fh) => new RectangleF(x + fx * s, y + fy * s, fw * s, fh * s);
        void Line(float x1, float y1, float x2, float y2) => g.DrawLine(p, P(x1, y1), P(x2, y2));
        void RoundRect(RectangleF rr, float rad) { using var gp = Ui.Round(rr, rad * s); g.DrawPath(p, gp); }

        switch (name)
        {
            case "screen":
                RoundRect(R(.08f, .14f, .84f, .56f), .08f);
                Line(.5f, .70f, .5f, .86f);
                Line(.32f, .86f, .68f, .86f);
                break;

            case "region":
            {
                using var dp = new Pen(c, lw) { DashPattern = new[] { 2.2f, 1.8f } };
                var rr = R(.12f, .2f, .76f, .6f);
                g.DrawRectangle(dp, rr.X, rr.Y, rr.Width, rr.Height);
                break;
            }

            case "window":
                RoundRect(R(.1f, .18f, .8f, .64f), .07f);
                Line(.1f, .36f, .9f, .36f);
                break;

            case "timer":
                g.DrawEllipse(p, R(.16f, .22f, .68f, .68f));
                Line(.5f, .56f, .5f, .38f);
                Line(.5f, .56f, .63f, .64f);
                Line(.4f, .1f, .6f, .1f);
                break;

            case "folder":
                g.DrawPolygon(p, new[] { P(.1f, .24f), P(.4f, .24f), P(.48f, .33f), P(.9f, .33f), P(.9f, .8f), P(.1f, .8f) });
                break;

            case "close":
                Line(.28f, .28f, .72f, .72f);
                Line(.72f, .28f, .28f, .72f);
                break;

            case "pen":
                g.DrawPolygon(p, new[] { P(.18f, .82f), P(.23f, .62f), P(.66f, .19f), P(.81f, .34f), P(.38f, .77f) });
                Line(.23f, .62f, .38f, .77f);
                Line(.57f, .28f, .72f, .43f);
                break;

            case "marker":
                g.DrawPolygon(p, new[] { P(.26f, .62f), P(.62f, .18f), P(.82f, .38f), P(.40f, .74f) });
                Line(.26f, .62f, .2f, .78f);
                Line(.2f, .78f, .40f, .74f);
                using (var hb = new Pen(Color.FromArgb(110, c), lw * 1.8f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                    g.DrawLine(hb, P(.14f, .9f), P(.86f, .9f));
                break;

            case "arrow":
                Line(.2f, .8f, .78f, .22f);
                Line(.78f, .22f, .44f, .22f);
                Line(.78f, .22f, .78f, .56f);
                break;

            case "rect":
                RoundRect(R(.14f, .22f, .72f, .56f), .08f);
                break;

            case "text":
                Line(.22f, .2f, .78f, .2f);
                Line(.5f, .2f, .5f, .82f);
                Line(.38f, .82f, .62f, .82f);
                break;

            case "blur":
                for (int i = 0; i < 3; i++)
                    for (int j = 0; j < 3; j++)
                    {
                        using var cb = new SolidBrush(Color.FromArgb((i + j) % 2 == 0 ? 255 : 90, c));
                        g.FillRectangle(cb, R(.14f + i * .25f, .14f + j * .25f, .22f, .22f));
                    }
                break;

            case "undo":
                Line(.22f, .38f, .6f, .38f);
                g.DrawArc(p, R(.4f, .38f, .42f, .42f), 270, 180);
                Line(.61f, .80f, .38f, .80f);
                Line(.22f, .38f, .36f, .24f);
                Line(.22f, .38f, .36f, .52f);
                break;

            case "copy":
                RoundRect(R(.3f, .12f, .54f, .56f), .07f);
                g.DrawLines(p, new[] { P(.16f, .36f), P(.16f, .88f), P(.6f, .88f) });
                break;

            case "save":
                Line(.5f, .14f, .5f, .62f);
                Line(.32f, .45f, .5f, .63f);
                Line(.68f, .45f, .5f, .63f);
                Line(.18f, .86f, .82f, .86f);
                break;

            case "check":
                g.DrawLines(p, new[] { P(.2f, .52f), P(.42f, .72f), P(.8f, .3f) });
                break;

            case "gear":
                g.DrawEllipse(p, R(.27f, .27f, .46f, .46f));
                g.DrawEllipse(p, R(.42f, .42f, .16f, .16f));
                for (int k = 0; k < 8; k++)
                {
                    double a = k * Math.PI / 4;
                    float cx = .5f, cy = .5f;
                    Line(cx + (float)Math.Cos(a) * .23f, cy + (float)Math.Sin(a) * .23f,
                         cx + (float)Math.Cos(a) * .38f, cy + (float)Math.Sin(a) * .38f);
                }
                break;
        }
        g.SmoothingMode = oldMode;
    }
}
