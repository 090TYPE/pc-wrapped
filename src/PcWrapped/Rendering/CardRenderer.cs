using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using PcWrapped.Controls;
using PcWrapped.Core.Aggregation;
using PcWrapped.Core.Models;

namespace PcWrapped.Rendering;

public static class CardRenderer
{
    public static readonly PixelSize Square = new(1080, 1080);
    public static readonly PixelSize Story = new(1080, 1920);

    /// <summary>Строит визуальное дерево карточки (без рендера в файл).</summary>
    public static Control BuildCard(PeriodStats stats, CardTheme theme, PixelSize size,
        IReadOnlyDictionary<string, IImage>? appIcons = null)
    {
        var stack = new StackPanel
        {
            Margin = new Thickness(80),
            Spacing = 18,
            VerticalAlignment = VerticalAlignment.Center,
        };

        void Row(string label, string value, IImage? icon = null)
        {
            var left = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 14 };
            if (icon is not null)
                left.Children.Add(new Image { Source = icon, Width = 40, Height = 40,
                    VerticalAlignment = VerticalAlignment.Center });
            left.Children.Add(new TextBlock { Text = label, Foreground = new SolidColorBrush(theme.TextColor),
                FontFamily = theme.FontFamily, FontSize = 34, Opacity = 0.85,
                VerticalAlignment = VerticalAlignment.Center });

            var dock = new DockPanel();
            var valueBlock = new TextBlock { Text = value, Foreground = new SolidColorBrush(theme.AccentColor),
                FontFamily = theme.FontFamily, FontSize = 34, FontWeight = FontWeight.Bold,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center };
            DockPanel.SetDock(valueBlock, Dock.Right);
            dock.Children.Add(valueBlock);
            dock.Children.Add(left);
            stack.Children.Add(dock);
        }

        // header text by period span
        static string Header(PeriodStats s)
        {
            int days = (s.To.DayNumber - s.From.DayNumber);
            if (days <= 0) return "ТВОЙ ДЕНЬ ЗА ПК";
            if (days <= 31) return "ТВОЯ НЕДЕЛЯ ЗА ПК";
            return "ТВОЙ ГОД ЗА ПК";
        }

        stack.Children.Add(new TextBlock
        {
            Text = Header(stats), Foreground = new SolidColorBrush(theme.TextColor),
            FontFamily = theme.FontFamily, FontSize = 28, Opacity = 0.8,
        });
        stack.Children.Add(new TextBlock
        {
            Text = FormatHours(stats.TotalActive), Foreground = new SolidColorBrush(theme.TextColor),
            FontFamily = theme.FontFamily, FontSize = 110, FontWeight = FontWeight.Black,
        });

        int shown = 0;
        foreach (var app in stats.TopApps)
        {
            if (shown >= 3) break;
            IImage? appIcon = null;
            appIcons?.TryGetValue(app.ProcessName, out appIcon);
            Row(app.ProcessName, FormatHours(app.Duration), appIcon);
            shown++;
        }
        // ---- charts band (category donut + hourly bars), theme-shaded ----
        var slices = ChartData.Segments(stats.ByCategory);
        var shades = new[] { 1.0, 0.72, 0.5, 0.36, 0.25 };
        Color Shade(int i) => WithOpacity(theme.TextColor, shades[Math.Min(i, shades.Length - 1)]);

        if (slices.Count > 0)
        {
            var band = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 28 };
            band.Children.Add(new CategoryDonut
            {
                Width = 150, Height = 150, Thickness = 24,
                EmptyBrush = new SolidColorBrush(WithOpacity(theme.TextColor, 0.15)),
                Segments = slices.Select((s, i) => new DonutSegment(s.Fraction, Shade(i))).ToList(),
            });
            var legend = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Spacing = 8 };
            for (int i = 0; i < slices.Count && i < 3; i++)
            {
                var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
                row.Children.Add(new Border
                {
                    Width = 26, Height = 26, CornerRadius = new Avalonia.CornerRadius(6),
                    Background = new SolidColorBrush(Shade(i)),
                    VerticalAlignment = VerticalAlignment.Center,
                });
                row.Children.Add(new TextBlock
                {
                    Text = $"{CategoryPalette.Name(slices[i].Category)} {slices[i].Fraction:P0}",
                    Foreground = new SolidColorBrush(theme.TextColor), FontFamily = theme.FontFamily,
                    FontSize = 30, VerticalAlignment = VerticalAlignment.Center,
                });
                legend.Children.Add(row);
            }
            band.Children.Add(legend);
            stack.Children.Add(band);
        }

        var hours = ChartData.NormalizeHours(stats.HourlySeconds);
        if (hours.Count > 0)
        {
            stack.Children.Add(new HourlyBars
            {
                Height = 90, Values = hours,
                Bar = new SolidColorBrush(theme.AccentColor),
                Track = new SolidColorBrush(WithOpacity(theme.TextColor, 0.12)),
            });
        }

        Row("🖱️ Мышь проехала", $"{stats.MouseKilometers:0.0} км");
        Row("⌨️ Нажатий", $"{stats.Keystrokes:N0}");
        if (stats.PeakHour >= 0) Row("🔥 Пик", $"{stats.PeakHour:00}:00");
        Row("📅 Серия", $"{stats.StreakDays} дн.");

        return new Border
        {
            Background = theme.Background,
            Width = size.Width,
            Height = size.Height,
            Child = stack,
        };
    }

    /// <summary>Рендерит карточку в Avalonia Bitmap (для превью).</summary>
    public static RenderTargetBitmap RenderToBitmap(PeriodStats stats, CardTheme theme, PixelSize size,
        IReadOnlyDictionary<string, IImage>? appIcons = null)
    {
        var card = BuildCard(stats, theme, size, appIcons);
        card.Measure(new Size(size.Width, size.Height));
        card.Arrange(new Rect(0, 0, size.Width, size.Height));
        var bmp = new RenderTargetBitmap(size, new Vector(96, 96));
        bmp.Render(card);
        return bmp;
    }

    /// <summary>Рендерит карточку в PNG-файл.</summary>
    public static void RenderToPng(PeriodStats stats, CardTheme theme, PixelSize size, string path,
        IReadOnlyDictionary<string, IImage>? appIcons = null)
    {
        using var bmp = RenderToBitmap(stats, theme, size, appIcons);
        bmp.Save(path);
    }

    private static Color WithOpacity(Color c, double o) =>
        new Color((byte)(o * 255), c.R, c.G, c.B);

    private static string FormatHours(TimeSpan t) =>
        t.TotalHours >= 1 ? $"{(int)t.TotalHours}ч {t.Minutes:00}м" : $"{t.Minutes}м";
}
