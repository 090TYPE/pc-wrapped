using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
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

        if (stats.TopApps.Count > 0)
        {
            IImage? icon = null;
            appIcons?.TryGetValue(stats.TopApps[0].ProcessName, out icon);
            Row(stats.TopApps[0].ProcessName, FormatHours(stats.TopApps[0].Duration), icon);
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

    private static string FormatHours(TimeSpan t) =>
        t.TotalHours >= 1 ? $"{(int)t.TotalHours}ч {t.Minutes:00}м" : $"{t.Minutes}м";
}
