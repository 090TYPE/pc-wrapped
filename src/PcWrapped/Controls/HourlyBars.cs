using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace PcWrapped.Controls;

public sealed class HourlyBars : Control
{
    public static readonly StyledProperty<IReadOnlyList<double>?> ValuesProperty =
        AvaloniaProperty.Register<HourlyBars, IReadOnlyList<double>?>(nameof(Values));

    public IReadOnlyList<double>? Values
    {
        get => GetValue(ValuesProperty);
        set => SetValue(ValuesProperty, value);
    }

    public IBrush Bar { get; set; } = new SolidColorBrush(Color.Parse("#7B2FF7"));
    public IBrush Track { get; set; } = new SolidColorBrush(Color.Parse("#2A2D39"));

    static HourlyBars() => AffectsRender<HourlyBars>(ValuesProperty);

    public override void Render(DrawingContext context)
    {
        var b = Bounds;
        if (b.Width <= 0 || b.Height <= 0) return;
        const int n = 24;
        double gap = 2;
        double bw = (b.Width - gap * (n - 1)) / n;
        if (bw <= 0) return;

        var vals = Values;
        for (int i = 0; i < n; i++)
        {
            double v = (vals != null && i < vals.Count) ? Math.Clamp(vals[i], 0, 1) : 0;
            double x = i * (bw + gap);
            context.FillRectangle(Track, new Rect(x, 0, bw, b.Height));
            double h = v * b.Height;
            if (h > 0)
                context.FillRectangle(Bar, new Rect(x, b.Height - h, bw, h));
        }
    }
}
