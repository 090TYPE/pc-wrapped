using System.Collections.Generic;
using Avalonia;
using Avalonia.Media;

namespace PcWrapped.Rendering;

public static class CardThemes
{
    public static readonly CardTheme Gradient = new(
        "gradient", "Яркий градиент",
        new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
            GradientStops =
            {
                new GradientStop(Color.Parse("#7b2ff7"), 0),
                new GradientStop(Color.Parse("#f107a3"), 1),
            }
        },
        Colors.White, Colors.White, "Segoe UI");

    public static readonly CardTheme Terminal = new(
        "terminal", "Dev / неон",
        new SolidColorBrush(Color.Parse("#0d1117")),
        Color.Parse("#e6edf3"), Color.Parse("#3fb950"), "Consolas");

    public static readonly CardTheme Minimal = new(
        "minimal", "Минимализм",
        new SolidColorBrush(Color.Parse("#faf9f7")),
        Color.Parse("#1a1a1a"), Color.Parse("#e0563f"), "Segoe UI");

    public static readonly IReadOnlyList<CardTheme> All = new[] { Gradient, Terminal, Minimal };
}
