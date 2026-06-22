using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace PcWrapped.Controls;

public readonly record struct DonutSegment(double Fraction, Color Color);

public sealed class CategoryDonut : Control
{
    public static readonly StyledProperty<IReadOnlyList<DonutSegment>?> SegmentsProperty =
        AvaloniaProperty.Register<CategoryDonut, IReadOnlyList<DonutSegment>?>(nameof(Segments));

    public IReadOnlyList<DonutSegment>? Segments
    {
        get => GetValue(SegmentsProperty);
        set => SetValue(SegmentsProperty, value);
    }

    public double Thickness { get; set; } = 16;
    public IBrush EmptyBrush { get; set; } = new SolidColorBrush(Color.Parse("#2A2D39"));

    static CategoryDonut() => AffectsRender<CategoryDonut>(SegmentsProperty);

    public override void Render(DrawingContext context)
    {
        var b = Bounds;
        double size = Math.Min(b.Width, b.Height);
        if (size <= 0) return;
        double r = size / 2 - Thickness / 2;
        if (r <= 0) return;
        var center = new Point(b.Width / 2, b.Height / 2);

        context.DrawEllipse(null, new Pen(EmptyBrush, Thickness), center, r, r);

        var segs = Segments;
        if (segs is null || segs.Count == 0) return;

        double start = -90;
        foreach (var s in segs)
        {
            double sweep = s.Fraction * 360.0;
            if (sweep <= 0) continue;
            var pen = new Pen(new SolidColorBrush(s.Color), Thickness) { LineCap = PenLineCap.Flat };
            if (sweep >= 359.9)
                context.DrawEllipse(null, pen, center, r, r);
            else
                context.DrawGeometry(null, pen, BuildArc(center, r, start, start + sweep));
            start += sweep;
        }
    }

    private static StreamGeometry BuildArc(Point c, double r, double a0, double a1)
    {
        var geo = new StreamGeometry();
        using var ctx = geo.Open();
        ctx.BeginFigure(PointOn(c, r, a0), false);
        ctx.ArcTo(PointOn(c, r, a1), new Size(r, r), 0, (a1 - a0) > 180, SweepDirection.Clockwise);
        ctx.EndFigure(false);
        return geo;
    }

    private static Point PointOn(Point c, double r, double deg)
    {
        double a = deg * Math.PI / 180.0;
        return new Point(c.X + r * Math.Cos(a), c.Y + r * Math.Sin(a));
    }
}
