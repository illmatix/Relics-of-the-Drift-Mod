using System;
using Cairo;
using Vintagestory.API.Client;

namespace DriftRelics.Gui;

internal static class RelicTheme
{
    private const double ParchmentR = 0.82, ParchmentG = 0.69, ParchmentB = 0.49;
    private const double InkR       = 0.16, InkG       = 0.10, InkB       = 0.04;
    private const double AmberR     = 0.86, AmberG     = 0.57, AmberB     = 0.18;
    private const double EmberR     = 1.00, EmberG     = 0.79, EmberB     = 0.38;
    private const double WaxR       = 0.58, WaxG       = 0.16, WaxB       = 0.16;

    public static void DrawParchment(Context ctx, ElementBounds bounds)
    {
        var w = bounds.InnerWidth;
        var h = bounds.InnerHeight;
        var r = GuiElement.scaled(6);

        ctx.Save();
        RoundRect(ctx, 0, 0, w, h, r);
        ctx.Clip();

        var diag = Math.Sqrt(w * w + h * h);
        var grad = new RadialGradient(w / 2, h / 2, 0, w / 2, h / 2, diag * 0.6);
        grad.AddColorStop(0,    new Color(ParchmentR + 0.08, ParchmentG + 0.07, ParchmentB + 0.05, 1.0));
        grad.AddColorStop(0.55, new Color(ParchmentR,        ParchmentG,        ParchmentB,        1.0));
        grad.AddColorStop(1.0,  new Color(ParchmentR - 0.28, ParchmentG - 0.26, ParchmentB - 0.22, 1.0));
        ctx.SetSource(grad);
        ctx.Rectangle(0, 0, w, h);
        ctx.Fill();
        grad.Dispose();

        var rng = new Random(1729);

        for (int i = 0; i < 4; i++)
        {
            ctx.SetSourceRGBA(InkR, InkG, InkB, 0.22);
            ctx.LineWidth = GuiElement.scaled(1.1);
            var y1 = h * rng.NextDouble();
            var y2 = y1 + (rng.NextDouble() - 0.5) * h * 0.5;
            ctx.MoveTo(0, y1);
            ctx.CurveTo(w * 0.33, y1 + (rng.NextDouble() - 0.5) * 12, w * 0.66, y2 + (rng.NextDouble() - 0.5) * 12, w, y2);
            ctx.Stroke();
        }

        for (int i = 0; i < 28; i++)
        {
            var sx = w * rng.NextDouble();
            var sy = h * rng.NextDouble();
            var sr = GuiElement.scaled(0.8 + rng.NextDouble() * 2.4);
            var a  = 0.16 + rng.NextDouble() * 0.22;
            ctx.SetSourceRGBA(InkR, InkG, InkB, a);
            ctx.Arc(sx, sy, sr, 0, 2 * Math.PI);
            ctx.Fill();
        }

        for (int i = 0; i < 42; i++)
        {
            var t = i / 42.0;
            var perim = 2 * (w + h);
            var d = t * perim;
            double ex, ey;
            if (d < w)         { ex = d;             ey = 0; }
            else if (d < w + h){ ex = w;             ey = d - w; }
            else if (d < 2*w+h){ ex = w - (d-w-h);   ey = h; }
            else               { ex = 0;             ey = h - (d-2*w-h); }
            var jitter = (rng.NextDouble() - 0.5) * GuiElement.scaled(4);
            var nx = ex + (ex < w/2 ? jitter : -jitter);
            var ny = ey + (ey < h/2 ? jitter : -jitter);
            ctx.SetSourceRGBA(InkR, InkG, InkB, 0.30 + rng.NextDouble() * 0.25);
            ctx.Arc(nx, ny, GuiElement.scaled(1.5 + rng.NextDouble() * 1.2), 0, 2 * Math.PI);
            ctx.Fill();
        }

        var vgrad = new RadialGradient(w / 2, h / 2, diag * 0.25, w / 2, h / 2, diag * 0.6);
        vgrad.AddColorStop(0,   new Color(0, 0, 0, 0));
        vgrad.AddColorStop(1.0, new Color(0, 0, 0, 0.30));
        ctx.SetSource(vgrad);
        ctx.Rectangle(0, 0, w, h);
        ctx.Fill();
        vgrad.Dispose();

        ctx.Restore();

        ctx.SetSourceRGBA(InkR, InkG, InkB, 0.85);
        ctx.LineWidth = GuiElement.scaled(1.8);
        RoundRect(ctx, 0, 0, w, h, r);
        ctx.Stroke();
    }

    public static void DrawBraidedAccent(Context ctx, ElementBounds bounds)
    {
        var w = bounds.InnerWidth;
        var h = bounds.InnerHeight;
        var midY = h / 2;
        var amp = h * 0.32;

        ctx.SetSourceRGBA(AmberR, AmberG, AmberB, 1.0);
        ctx.LineWidth = GuiElement.scaled(1.5);
        ctx.MoveTo(0, midY - amp);
        ctx.LineTo(w, midY - amp);
        ctx.Stroke();
        ctx.MoveTo(0, midY + amp);
        ctx.LineTo(w, midY + amp);
        ctx.Stroke();

        ctx.SetSourceRGBA(EmberR, EmberG, EmberB, 0.95);
        ctx.LineWidth = GuiElement.scaled(1.3);
        int knots = (int)Math.Max(6, w / GuiElement.scaled(10));
        for (int i = 0; i < knots; i++)
        {
            var x0 = w * i / (double)knots;
            var x1 = w * (i + 1) / (double)knots;
            ctx.MoveTo(x0, midY - amp);
            ctx.LineTo((x0 + x1) / 2, midY + amp);
            ctx.LineTo(x1, midY - amp);
            ctx.Stroke();
        }

        ctx.SetSourceRGBA(AmberR, AmberG, AmberB, 1.0);
        for (int i = 0; i <= knots; i++)
        {
            var x = w * i / (double)knots;
            ctx.Arc(x, midY - amp, GuiElement.scaled(1.6), 0, 2 * Math.PI);
            ctx.Fill();
            ctx.Arc(x, midY + amp, GuiElement.scaled(1.6), 0, 2 * Math.PI);
            ctx.Fill();
        }
    }

    public static void DrawRuneRing(Context ctx, double cx, double cy, double radius)
    {
        ctx.SetSourceRGBA(AmberR, AmberG, AmberB, 0.95);
        ctx.LineWidth = GuiElement.scaled(1.5);
        for (int i = 0; i < 8; i++)
        {
            var theta = i * Math.PI / 4 - Math.PI / 2;
            var x = cx + Math.Cos(theta) * radius;
            var y = cy + Math.Sin(theta) * radius;
            DrawRuneGlyph(ctx, x, y, i);
        }
    }

    private static void DrawRuneGlyph(Context ctx, double cx, double cy, int variant)
    {
        var s = GuiElement.scaled(5.0);
        switch (variant % 4)
        {
            case 0:
                ctx.MoveTo(cx - s, cy); ctx.LineTo(cx + s, cy);
                ctx.MoveTo(cx, cy - s); ctx.LineTo(cx, cy + s);
                ctx.Stroke();
                break;
            case 1:
                ctx.MoveTo(cx - s, cy - s); ctx.LineTo(cx + s, cy + s);
                ctx.MoveTo(cx + s, cy - s); ctx.LineTo(cx - s, cy + s);
                ctx.Stroke();
                break;
            case 2:
                ctx.Arc(cx, cy, s * 0.75, 0, 2 * Math.PI);
                ctx.Stroke();
                ctx.Arc(cx, cy, s * 0.30, 0, 2 * Math.PI);
                ctx.Fill();
                break;
            case 3:
                ctx.MoveTo(cx - s, cy + s * 0.5);
                ctx.LineTo(cx,     cy - s);
                ctx.LineTo(cx + s, cy + s * 0.5);
                ctx.ClosePath();
                ctx.Stroke();
                break;
        }
    }

    public static void DrawAmberGlow(Context ctx, ElementBounds bounds, double cx, double cy, double radius, double intensity)
    {
        intensity = Math.Clamp(intensity, 0.0, 1.0);
        if (intensity <= 0) return;

        var grad = new RadialGradient(cx, cy, 0, cx, cy, radius);
        grad.AddColorStop(0,   new Color(EmberR, EmberG, EmberB, 0.65 * intensity));
        grad.AddColorStop(0.4, new Color(AmberR, AmberG, AmberB, 0.45 * intensity));
        grad.AddColorStop(1.0, new Color(AmberR, AmberG, AmberB, 0.0));
        ctx.SetSource(grad);
        ctx.Rectangle(0, 0, bounds.InnerWidth, bounds.InnerHeight);
        ctx.Fill();
        grad.Dispose();
    }

    public static void DrawEmberParticles(Context ctx, ElementBounds bounds, double anchorX, double anchorYBottom, double riseHeight, long timeMs)
    {
        var t = timeMs / 1000.0;
        var seeds = new (double xOffset, double phase, double sway)[]
        {
            (-22,  0.00, 0.7),
            (-12,  0.30, 0.4),
            ( -3,  0.55, 0.9),
            (  7,  0.18, 0.5),
            ( 16,  0.72, 0.3),
            ( 24,  0.45, 0.8),
            ( -8,  0.88, 0.6),
            ( 12,  0.10, 0.7),
        };

        foreach (var (xOff, phase, swayAmp) in seeds)
        {
            var localT = (t * 0.32 + phase) % 1.0;
            var sway   = Math.Sin((t * 1.8 + phase * 6.28)) * GuiElement.scaled(swayAmp * 6);
            var px = anchorX + GuiElement.scaled(xOff) + sway;
            var py = anchorYBottom - riseHeight * localT;
            var alpha = Math.Sin(localT * Math.PI) * 0.95;
            if (alpha <= 0) continue;
            var rad = GuiElement.scaled(1.8 + (1 - localT) * 0.8);
            ctx.SetSourceRGBA(EmberR, EmberG, EmberB, alpha);
            ctx.Arc(px, py, rad, 0, 2 * Math.PI);
            ctx.Fill();
        }
    }

    public static void DrawProgressBar(Context ctx, ElementBounds bounds, double pct)
    {
        pct = Math.Clamp(pct, 0.0, 1.0);

        ctx.SetSourceRGBA(InkR, InkG, InkB, 1.0);
        ctx.Rectangle(0, 0, bounds.InnerWidth, bounds.InnerHeight);
        ctx.Fill();

        var inset = GuiElement.scaled(1.8);
        ctx.SetSourceRGBA(0.50, 0.42, 0.30, 1.0);
        ctx.Rectangle(inset, inset, bounds.InnerWidth - 2 * inset, bounds.InnerHeight - 2 * inset);
        ctx.Fill();

        if (pct > 0)
        {
            var fillW = (bounds.InnerWidth - 2 * inset) * pct;
            ctx.SetSourceRGBA(WaxR, WaxG, WaxB, 1.0);
            ctx.Rectangle(inset, inset, fillW, bounds.InnerHeight - 2 * inset);
            ctx.Fill();

            ctx.SetSourceRGBA(EmberR, EmberG, EmberB, 0.85);
            var gleamY = bounds.InnerHeight * 0.30;
            ctx.Rectangle(inset, gleamY, fillW, GuiElement.scaled(2.0));
            ctx.Fill();
        }
    }

    public static void RoundRect(Context ctx, double x, double y, double w, double h, double r)
    {
        ctx.NewSubPath();
        ctx.Arc(x + w - r, y + r,     r, -Math.PI / 2, 0);
        ctx.Arc(x + w - r, y + h - r, r, 0,            Math.PI / 2);
        ctx.Arc(x + r,     y + h - r, r, Math.PI / 2,  Math.PI);
        ctx.Arc(x + r,     y + r,     r, Math.PI,      3 * Math.PI / 2);
        ctx.ClosePath();
    }
}
