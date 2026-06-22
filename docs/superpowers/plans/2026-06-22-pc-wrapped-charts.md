# PC Wrapped — Charts (category donut + hourly bars) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Нарисовать кольцевую диаграмму категорий и столбики активности по часам — и в главном окне, и на экспортируемой карточке.

**Architecture:** Чистый хелпер `ChartData` в Core готовит доли/нормализацию (покрыт тестами). Два переиспользуемых Avalonia-контрола (`CategoryDonut`, `HourlyBars`) рисуют примитивами и работают как в живом окне, так и при рендере карточки в `RenderTargetBitmap`. Цвета категорий — в app-слое (`CategoryPalette`); на карточке используются оттенки цвета темы.

**Tech Stack:** .NET 8, Avalonia 11.0.10, C#, xUnit.

**Commit author:** ВСЕ коммиты — обычный `git commit -m "..."` (git config user = `090_TYPE`). НИКАКИХ `Co-Authored-By` трейлеров, без `--author`, без упоминания Claude/AI. Solution: `PcWrapped.slnx`. App project БЕЗ ImplicitUsings — явные `using`.

---

## File Structure
```
src/PcWrapped.Core/Aggregation/ChartData.cs       # CREATE: Segments + NormalizeHours (pure)
src/PcWrapped/Rendering/CategoryPalette.cs        # CREATE: Category -> Color/Name
src/PcWrapped/Controls/CategoryDonut.cs           # CREATE: donut control (+ DonutSegment)
src/PcWrapped/Controls/HourlyBars.cs              # CREATE: 24-bar control
src/PcWrapped/Views/MainWindow.axaml(.cs)         # MODIFY: charts section + UpdateCharts
src/PcWrapped/Rendering/CardRenderer.cs           # MODIFY: charts band + top-3 on card
tests/PcWrapped.Core.Tests/ChartDataTests.cs      # CREATE
tests/PcWrapped.App.Tests/CardRendererTests.cs    # MODIFY: render-with-charts smoke
```

---

## Task 1: ChartData (Core, TDD)

**Files:**
- Create: `src/PcWrapped.Core/Aggregation/ChartData.cs`
- Test: `tests/PcWrapped.Core.Tests/ChartDataTests.cs`

- [ ] **Step 1: Write failing tests**

`tests/PcWrapped.Core.Tests/ChartDataTests.cs`:
```csharp
using PcWrapped.Core.Aggregation;
using PcWrapped.Core.Models;

namespace PcWrapped.Core.Tests;

public class ChartDataTests
{
    [Fact]
    public void Segments_ComputesFractionsDescending()
    {
        var byCat = new Dictionary<Category, TimeSpan>
        {
            [Category.Work] = TimeSpan.FromHours(3),
            [Category.Games] = TimeSpan.FromHours(1),
        };
        var segs = ChartData.Segments(byCat);
        Assert.Equal(2, segs.Count);
        Assert.Equal(Category.Work, segs[0].Category);
        Assert.Equal(0.75, segs[0].Fraction, 3);
        Assert.Equal(0.25, segs[1].Fraction, 3);
        Assert.Equal(1.0, segs.Sum(s => s.Fraction), 3);
    }

    [Fact]
    public void Segments_SkipsZeroAndEmpty()
    {
        Assert.Empty(ChartData.Segments(new Dictionary<Category, TimeSpan>()));
        var byCat = new Dictionary<Category, TimeSpan> { [Category.Work] = TimeSpan.Zero };
        Assert.Empty(ChartData.Segments(byCat));
    }

    [Fact]
    public void NormalizeHours_DividesByMax_PreservesLength()
    {
        var hours = new int[24];
        hours[10] = 50; hours[22] = 100;
        var n = ChartData.NormalizeHours(hours);
        Assert.Equal(24, n.Count);
        Assert.Equal(0.5, n[10], 3);
        Assert.Equal(1.0, n[22], 3);
        Assert.Equal(0.0, n[0], 3);
    }

    [Fact]
    public void NormalizeHours_AllZero_ReturnsAllZero()
    {
        var n = ChartData.NormalizeHours(new int[24]);
        Assert.Equal(24, n.Count);
        Assert.All(n, v => Assert.Equal(0.0, v, 6));
    }
}
```

- [ ] **Step 2: Run — verify fail**

Run: `dotnet test --filter ChartDataTests`
Expected: FAIL (ChartData missing).

- [ ] **Step 3: Implement**

`src/PcWrapped.Core/Aggregation/ChartData.cs`:
```csharp
using PcWrapped.Core.Models;

namespace PcWrapped.Core.Aggregation;

public static class ChartData
{
    public static IReadOnlyList<CategorySlice> Segments(IReadOnlyDictionary<Category, TimeSpan> byCategory)
    {
        double total = byCategory.Values.Sum(t => t.TotalSeconds);
        if (total <= 0) return Array.Empty<CategorySlice>();
        return byCategory
            .Where(kv => kv.Value.TotalSeconds > 0)
            .OrderByDescending(kv => kv.Value.TotalSeconds)
            .Select(kv => new CategorySlice(kv.Key, kv.Value.TotalSeconds / total))
            .ToList();
    }

    public static IReadOnlyList<double> NormalizeHours(IReadOnlyList<int> hours)
    {
        if (hours is null || hours.Count == 0) return Array.Empty<double>();
        int max = hours.Max();
        if (max <= 0) return hours.Select(_ => 0.0).ToList();
        return hours.Select(h => (double)h / max).ToList();
    }
}

public readonly record struct CategorySlice(Category Category, double Fraction);
```

- [ ] **Step 4: Run — verify pass**

Run: `dotnet test --filter ChartDataTests`
Expected: PASS (4).

- [ ] **Step 5: Full suite + commit**

Run: `dotnet test PcWrapped.slnx` (all pass).
```bash
git add -A
git commit -m "feat: add ChartData (category slices + hourly normalization)"
```

---

## Task 2: Chart controls + palette

**Files:**
- Create: `src/PcWrapped/Rendering/CategoryPalette.cs`
- Create: `src/PcWrapped/Controls/CategoryDonut.cs`
- Create: `src/PcWrapped/Controls/HourlyBars.cs`

No unit tests (rendering controls); acceptance = build succeeds. They are exercised by the card headless test (Task 4) and manual run (Task 3).

- [ ] **Step 1: CategoryPalette**

`src/PcWrapped/Rendering/CategoryPalette.cs`:
```csharp
using Avalonia.Media;
using PcWrapped.Core.Models;

namespace PcWrapped.Rendering;

public static class CategoryPalette
{
    public static Color Of(Category c) => c switch
    {
        Category.Work => Color.Parse("#7B2FF7"),
        Category.Games => Color.Parse("#F107A3"),
        Category.Social => Color.Parse("#3FB950"),
        Category.Browser => Color.Parse("#58A6FF"),
        _ => Color.Parse("#8A8F9B"),
    };

    public static string Name(Category c) => c switch
    {
        Category.Work => "Работа",
        Category.Games => "Игры",
        Category.Social => "Соцсети",
        Category.Browser => "Браузер",
        _ => "Прочее",
    };
}
```

- [ ] **Step 2: CategoryDonut**

`src/PcWrapped/Controls/CategoryDonut.cs`:
```csharp
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
            var pen = new Pen(new SolidColorBrush(s.Color), Thickness) { LineCap = PenLineCap.Butt };
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
```

- [ ] **Step 3: HourlyBars**

`src/PcWrapped/Controls/HourlyBars.cs`:
```csharp
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
```

- [ ] **Step 4: Build + commit**

Run: `dotnet build PcWrapped.slnx`
Expected: 0 errors (CA1416 warnings OK).
```bash
git add -A
git commit -m "feat: add CategoryDonut and HourlyBars chart controls"
```

---

## Task 3: Dashboard charts section

**Files:**
- Modify: `src/PcWrapped/Views/MainWindow.axaml`
- Modify: `src/PcWrapped/Views/MainWindow.axaml.cs`

- [ ] **Step 1: Add charts section to MainWindow.axaml**

Add the controls namespace to the `<Window>` opening tag (alongside the existing xmlns lines):
```
        xmlns:ctl="clr-namespace:PcWrapped.Controls"
```
Then REPLACE the entire center `<Grid Grid.Column="1" ...>...</Grid>` block (the one with `RowDefinitions="Auto,Auto,*"`) with this version (adds a charts row; rows are now `Auto,Auto,Auto,*`):
```xml
    <Grid Grid.Column="1" RowDefinitions="Auto,Auto,Auto,*" Margin="20,16">
      <Grid Grid.Row="0" ColumnDefinitions="*,Auto">
        <StackPanel>
          <TextBlock x:Name="PeriodLabel" Classes="lab" Text="ТВОЯ НЕДЕЛЯ ЗА ПК"/>
          <TextBlock x:Name="TotalText" FontSize="34" FontWeight="Bold" Text="—"/>
        </StackPanel>
        <StackPanel Grid.Column="1" HorizontalAlignment="Right">
          <TextBlock Classes="lab" Text="СЕРИЯ 🔥" HorizontalAlignment="Right"/>
          <TextBlock x:Name="StreakText" FontSize="20" FontWeight="Bold"
                     HorizontalAlignment="Right" Text="—"/>
        </StackPanel>
      </Grid>

      <Grid Grid.Row="1" ColumnDefinitions="Auto,*" Margin="0,16,0,0">
        <StackPanel Orientation="Horizontal" Spacing="12">
          <ctl:CategoryDonut x:Name="Donut" Width="104" Height="104"/>
          <StackPanel x:Name="DonutLegend" VerticalAlignment="Center" Spacing="3"/>
        </StackPanel>
        <StackPanel Grid.Column="1" Margin="24,0,0,0" VerticalAlignment="Center">
          <TextBlock Classes="lab" Text="АКТИВНОСТЬ ПО ЧАСАМ"/>
          <ctl:HourlyBars x:Name="Bars" Height="64" Margin="0,6,0,2"/>
          <Grid ColumnDefinitions="*,*,*,*,*">
            <TextBlock Classes="lab" Text="0"/>
            <TextBlock Grid.Column="1" Classes="lab" Text="6" HorizontalAlignment="Center"/>
            <TextBlock Grid.Column="2" Classes="lab" Text="12" HorizontalAlignment="Center"/>
            <TextBlock Grid.Column="3" Classes="lab" Text="18" HorizontalAlignment="Center"/>
            <TextBlock Grid.Column="4" Classes="lab" Text="23" HorizontalAlignment="Right"/>
          </Grid>
        </StackPanel>
      </Grid>

      <TextBlock Grid.Row="2" Classes="lab" Text="ТОП ПРИЛОЖЕНИЙ" Margin="0,16,0,6"/>

      <ScrollViewer Grid.Row="3">
        <ItemsControl x:Name="AppList">
          <ItemsControl.ItemsPanel>
            <ItemsPanelTemplate><WrapPanel/></ItemsPanelTemplate>
          </ItemsControl.ItemsPanel>
          <ItemsControl.ItemTemplate>
            <DataTemplate x:DataType="vm:AppRowVm">
              <Border Classes="appTile" Width="332" Height="60" Margin="0,0,10,10" Padding="8">
                <Grid ColumnDefinitions="32,*">
                  <Panel Grid.Column="0" Width="30" Height="30">
                    <Border CornerRadius="7" Background="{StaticResource AccentBrush}">
                      <TextBlock Text="{Binding Name, Converter={x:Static conv:FirstLetterConverter.Instance}}"
                                 Foreground="White" FontWeight="Bold"
                                 HorizontalAlignment="Center" VerticalAlignment="Center"/>
                    </Border>
                    <Image Width="30" Height="30"
                           Source="{Binding ExecutablePath, Converter={x:Static conv:PathToIconConverter.Instance}}"/>
                  </Panel>
                  <StackPanel Grid.Column="1" Margin="9,0,0,0" VerticalAlignment="Center">
                    <TextBlock Text="{Binding Name}" FontWeight="SemiBold" FontSize="12"
                               TextTrimming="CharacterEllipsis"/>
                    <TextBlock Text="{Binding TimeText}" Foreground="{StaticResource MutedBrush}" FontSize="11"/>
                    <ProgressBar Classes="usage" Minimum="0" Maximum="1"
                                 Value="{Binding Fraction}" Margin="0,4,0,0"/>
                  </StackPanel>
                </Grid>
              </Border>
            </DataTemplate>
          </ItemsControl.ItemTemplate>
        </ItemsControl>
      </ScrollViewer>
    </Grid>
```

- [ ] **Step 2: Update charts in code-behind**

In `src/PcWrapped/Views/MainWindow.axaml.cs`, add usings at top:
```csharp
using System.Collections.Generic;
using System.Linq;
using PcWrapped.Controls;
using PcWrapped.Core.Aggregation;
```
Add a call to `UpdateCharts();` inside `RefreshAsync`, right after the `AppList` ItemsSource line and before `RenderPreview();`:
```csharp
        UpdateCharts();
```
Add this method to the class:
```csharp
    private void UpdateCharts()
    {
        if (_current is null) return;

        var slices = ChartData.Segments(_current.ByCategory);
        this.FindControl<CategoryDonut>("Donut")!.Segments =
            slices.Select(s => new DonutSegment(s.Fraction, CategoryPalette.Of(s.Category))).ToList();

        var legend = this.FindControl<StackPanel>("DonutLegend")!;
        legend.Children.Clear();
        foreach (var s in slices)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
            row.Children.Add(new Border
            {
                Width = 10, Height = 10, CornerRadius = new Avalonia.CornerRadius(3),
                Background = new SolidColorBrush(CategoryPalette.Of(s.Category)),
                VerticalAlignment = VerticalAlignment.Center,
            });
            row.Children.Add(new TextBlock
            {
                Text = $"{CategoryPalette.Name(s.Category)} {s.Fraction:P0}",
                FontSize = 11, VerticalAlignment = VerticalAlignment.Center,
            });
            legend.Children.Add(row);
        }

        this.FindControl<HourlyBars>("Bars")!.Values = ChartData.NormalizeHours(_current.HourlySeconds);
    }
```

- [ ] **Step 3: Build**

Run: `dotnet build PcWrapped.slnx`
Expected: 0 errors. (If a running `PcWrapped.exe` locks the build, run `taskkill //F //IM PcWrapped.exe` first.)

- [ ] **Step 4: Tests still green**

Run: `dotnet test PcWrapped.slnx`
Expected: all pass.

- [ ] **Step 5: Manual verification**

Run the app (`dotnet run --project src/PcWrapped`, background/timed; don't hang the session). Confirm: dark window shows a charts section under the header — a category donut with legend on the left and hourly bars with 0/6/12/18/23 axis on the right; app grid below still works; switching period updates charts. Close the app.

- [ ] **Step 6: Commit**

```bash
git add -A
git commit -m "feat: show category donut and hourly bars on the dashboard"
```

---

## Task 4: Charts on the shareable card + top-3

**Files:**
- Modify: `src/PcWrapped/Rendering/CardRenderer.cs`
- Test: `tests/PcWrapped.App.Tests/CardRendererTests.cs`

- [ ] **Step 1: Update BuildCard — top-3 apps + charts band**

In `src/PcWrapped/Rendering/CardRenderer.cs` ensure these usings are present at the top:
```csharp
using System.Collections.Generic;
using System.Linq;
using Avalonia.Layout;
using Avalonia.Media;
using PcWrapped.Controls;
using PcWrapped.Core.Aggregation;
```
Change the app loop limit from 5 to 3 (find `if (shown >= 5) break;` and make it `if (shown >= 3) break;`).

Then, AFTER the app loop and BEFORE the vanity rows (`Row("🖱️ Мышь проехала", ...)`), insert the charts band:
```csharp
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
```
Add this private helper to the `CardRenderer` class:
```csharp
    private static Color WithOpacity(Color c, double o) =>
        new Color((byte)(o * 255), c.R, c.G, c.B);
```

- [ ] **Step 2: Build**

Run: `dotnet build PcWrapped.slnx`
Expected: 0 errors.

- [ ] **Step 3: Add headless render-with-charts test**

In `tests/PcWrapped.App.Tests/CardRendererTests.cs` add inside the class (ensure `using System;` and `using System.Collections.Generic;` and `using PcWrapped.Core.Models;` present):
```csharp
    private static PeriodStats SampleWithCharts()
    {
        var hourly = new int[24];
        hourly[10] = 40; hourly[14] = 90; hourly[22] = 60;
        return new PeriodStats(
            new DateOnly(2026, 6, 9), new DateOnly(2026, 6, 15),
            TimeSpan.FromHours(37),
            new[] { new AppUsage("code", TimeSpan.FromHours(14)), new AppUsage("chrome", TimeSpan.FromHours(8)) },
            new Dictionary<Category, TimeSpan>
            {
                [Category.Work] = TimeSpan.FromHours(20),
                [Category.Browser] = TimeSpan.FromHours(10),
                [Category.Games] = TimeSpan.FromHours(7),
            },
            14, hourly, 5, 91204, 4200, 3.4);
    }

    [AvaloniaFact]
    public void RenderToPng_WithCharts_ProducesValidPng()
    {
        var dir = Path.Combine(Path.GetTempPath(), "pcwrapped-test");
        Directory.CreateDirectory(dir);
        foreach (var theme in CardThemes.All)
        {
            var path = Path.Combine(dir, $"charts-{theme.Id}.png");
            CardRenderer.RenderToPng(SampleWithCharts(), theme, CardRenderer.Square, path);
            Assert.True(File.Exists(path));
            Assert.True(new FileInfo(path).Length > 1000);
        }
    }
```

- [ ] **Step 4: Run tests**

Run: `dotnet test PcWrapped.slnx`
Expected: all pass — existing card tests (empty `ByCategory`/all-zero hours → band skipped/flat, no crash) plus the new charts test (3 themes).

- [ ] **Step 5: Manual verification**

Run the app, use the PC a bit, export a card (1:1 and 9:16). Confirm the PNG shows: title, total, top-3 apps, a category donut + legend, hourly bars, then the vanity rows — nothing clipped. Close the app.

- [ ] **Step 6: Commit**

```bash
git add -A
git commit -m "feat: add category donut and hourly bars to the shareable card"
```

---

## Final verification
- [ ] `dotnet test PcWrapped.slnx` — all pass.
- [ ] `dotnet run --project src/PcWrapped` — dashboard shows donut + legend + hourly bars; export produces a card with charts (1:1 and 9:16) and top-3 apps, nothing clipped.
- [ ] All new commits authored by `090_TYPE`, no `Co-Authored-By` trailers.

## Spec Coverage Notes
- ChartData (Segments/NormalizeHours) + tests → Task 1.
- CategoryDonut / HourlyBars controls + palette → Task 2.
- Dashboard donut+legend+hourly bars → Task 3.
- Card charts band + top-3 + headless test (incl. empty-data safety) → Task 4.
- Out of scope (abs-time legend, tooltips, animations) → not included.
