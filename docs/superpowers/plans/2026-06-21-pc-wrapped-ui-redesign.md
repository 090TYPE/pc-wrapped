# PC Wrapped — UI Redesign + App Icons — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Переделать главное окно PC Wrapped в полированный тёмный интерфейс с большим списком топ-приложений и настоящими иконками .exe, плюс живое превью карточки и мини-иконки на экспортируемой карточке.

**Architecture:** Путь к .exe захватывается трекером и хранится в новой таблице `app_paths`. Иконки извлекаются в app-слое (`AppIconProvider`, кэш в памяти) и биндятся в UI через конвертер. Core остаётся без UI-зависимостей. Главное окно переписано на Grid (рейл + сетка приложений + превью).

**Tech Stack:** .NET 8, Avalonia 11.0.10, C#, Microsoft.Data.Sqlite, System.Drawing.Common (Windows), xUnit.

**Solution file:** `PcWrapped.slnx`. Build: `dotnet build PcWrapped.slnx`. Test: `dotnet test PcWrapped.slnx`. App project has **no ImplicitUsings** — add explicit `using`s in every app .cs file.

---

## File Structure

```
src/PcWrapped.Core/
  Tracking/IForegroundWindowSource.cs   # MODIFY: ForegroundInfo += ExecutablePath
  Tracking/ActivityTracker.cs           # MODIFY: upsert exe path
  Storage/IStatsRepository.cs           # MODIFY: + UpsertAppPathAsync / GetAppPathsAsync
  Storage/SqliteStatsRepository.cs      # MODIFY: app_paths table + methods
src/PcWrapped/
  Native/Win32ForegroundWindowSource.cs # MODIFY: fill ExecutablePath
  Native/AppIconProvider.cs             # CREATE: extract .exe icon -> Avalonia Bitmap (cached)
  Converters/PathToIconConverter.cs     # CREATE: exe path -> icon
  Converters/FirstLetterConverter.cs    # CREATE: name -> first letter (fallback glyph)
  ViewModels/AppRowVm.cs                # CREATE: app row (name/time/fraction/path)
  ViewModels/MainViewModel.cs           # MODIFY: periods + topAppLimit 12 + GetAppPathsAsync
  Rendering/CardRenderer.cs             # MODIFY: RenderToBitmap + optional app icons
  App.axaml                             # MODIFY: dark theme + styles
  Views/MainWindow.axaml(.cs)           # REWRITE: new layout + preview
tests/PcWrapped.Core.Tests/
  SqliteStatsRepositoryTests.cs         # MODIFY: app_paths tests
  ActivityTrackerTests.cs               # MODIFY: path-upsert tests
tests/PcWrapped.App.Tests/
  AppRowVmTests.cs                      # CREATE: FromStats logic
  CardRendererTests.cs                  # MODIFY: render-with-icons smoke
```

---

## Task 1: Core — ExecutablePath + app_paths storage

**Files:**
- Modify: `src/PcWrapped.Core/Tracking/IForegroundWindowSource.cs`
- Modify: `src/PcWrapped.Core/Storage/IStatsRepository.cs`
- Modify: `src/PcWrapped.Core/Storage/SqliteStatsRepository.cs`
- Test: `tests/PcWrapped.Core.Tests/SqliteStatsRepositoryTests.cs`

- [ ] **Step 1: Add ExecutablePath to ForegroundInfo**

In `IForegroundWindowSource.cs`, change the record struct line to:
```csharp
public readonly record struct ForegroundInfo(string ProcessName, string WindowTitle, string? ExecutablePath = null);
```
(The optional 3rd parameter keeps existing two-argument constructions compiling.)

- [ ] **Step 2: Add storage methods to the interface**

In `IStatsRepository.cs`, add inside the interface:
```csharp
    Task UpsertAppPathAsync(string process, string path);
    Task<IReadOnlyDictionary<string, string>> GetAppPathsAsync();
```

- [ ] **Step 3: Write failing tests**

Append inside the `SqliteStatsRepositoryTests` class:
```csharp
    [Fact]
    public async Task AppPaths_UpsertAndGet_RoundTrips()
    {
        var repo = await NewRepoAsync();
        await repo.UpsertAppPathAsync("code", @"C:\apps\code.exe");
        await repo.UpsertAppPathAsync("chrome", @"C:\apps\chrome.exe");

        var map = await repo.GetAppPathsAsync();
        Assert.Equal(@"C:\apps\code.exe", map["code"]);
        Assert.Equal(@"C:\apps\chrome.exe", map["chrome"]);
    }

    [Fact]
    public async Task AppPaths_Upsert_UpdatesExistingPath()
    {
        var repo = await NewRepoAsync();
        await repo.UpsertAppPathAsync("code", @"C:\old\code.exe");
        await repo.UpsertAppPathAsync("code", @"C:\new\code.exe");

        var map = await repo.GetAppPathsAsync();
        Assert.Single(map);
        Assert.Equal(@"C:\new\code.exe", map["code"]);
    }
```

- [ ] **Step 4: Run tests to verify they fail**

Run: `dotnet test --filter SqliteStatsRepositoryTests`
Expected: FAIL (UpsertAppPathAsync/GetAppPathsAsync not implemented).

- [ ] **Step 5: Add the table to the schema**

In `SqliteStatsRepository.InitializeAsync`, append this to the `sql` string (before the closing `";`):
```sql
CREATE TABLE IF NOT EXISTS app_paths (
    process TEXT PRIMARY KEY,
    path    TEXT NOT NULL
);
```

- [ ] **Step 6: Implement the methods**

Add to `SqliteStatsRepository` (before `Dispose`):
```csharp
    public async Task UpsertAppPathAsync(string process, string path)
    {
        await using var cmd = _conn.CreateCommand();
        cmd.CommandText =
            "INSERT INTO app_paths (process, path) VALUES ($p, $path) " +
            "ON CONFLICT(process) DO UPDATE SET path = $path;";
        cmd.Parameters.AddWithValue("$p", process);
        cmd.Parameters.AddWithValue("$path", path);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<IReadOnlyDictionary<string, string>> GetAppPathsAsync()
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        await using var cmd = _conn.CreateCommand();
        cmd.CommandText = "SELECT process, path FROM app_paths";
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            map[r.GetString(0)] = r.GetString(1);
        return map;
    }
```

- [ ] **Step 7: Run tests to verify they pass**

Run: `dotnet test --filter SqliteStatsRepositoryTests`
Expected: PASS.

- [ ] **Step 8: Run full suite + commit**

Run: `dotnet test PcWrapped.slnx` (expected: all pass).
```bash
git add -A
git commit -m "feat: store executable path per app in app_paths table"
```

---

## Task 2: Core — ActivityTracker upserts exe path

**Files:**
- Modify: `src/PcWrapped.Core/Tracking/ActivityTracker.cs`
- Test: `tests/PcWrapped.Core.Tests/ActivityTrackerTests.cs`

- [ ] **Step 1: Write failing tests**

Append inside the `ActivityTrackerTests` class:
```csharp
    [Fact]
    public async Task Tick_WithExecutablePath_UpsertsAppPath()
    {
        var repo = await NewRepoAsync();
        var fg = new FakeForeground { Current = new ForegroundInfo("code", "x", @"C:\a\code.exe"), Idle = 0 };
        var tracker = new ActivityTracker(repo, fg, new FakeInput(), 2, 60);

        await tracker.TickAsync(new DateTimeOffset(2026, 6, 9, 10, 0, 0, TimeSpan.Zero));

        var map = await repo.GetAppPathsAsync();
        Assert.Equal(@"C:\a\code.exe", map["code"]);
    }

    [Fact]
    public async Task Tick_WithoutExecutablePath_DoesNotUpsert()
    {
        var repo = await NewRepoAsync();
        var fg = new FakeForeground { Current = new ForegroundInfo("code", "x"), Idle = 0 };
        var tracker = new ActivityTracker(repo, fg, new FakeInput(), 2, 60);

        await tracker.TickAsync(new DateTimeOffset(2026, 6, 9, 10, 0, 0, TimeSpan.Zero));

        var map = await repo.GetAppPathsAsync();
        Assert.Empty(map);
    }
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test --filter ActivityTrackerTests`
Expected: FAIL (path not upserted; `Tick_WithExecutablePath...` fails on missing key).

- [ ] **Step 3: Implement the upsert**

In `ActivityTracker.TickAsync`, after the existing `await _repo.AddSampleAsync(...)` block, add:
```csharp
        if (!string.IsNullOrEmpty(fg.Value.ExecutablePath))
            await _repo.UpsertAppPathAsync(fg.Value.ProcessName, fg.Value.ExecutablePath!);
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test --filter ActivityTrackerTests`
Expected: PASS.

- [ ] **Step 5: Full suite + commit**

Run: `dotnet test PcWrapped.slnx` (all pass).
```bash
git add -A
git commit -m "feat: tracker records executable path when available"
```

---

## Task 3: Win32ForegroundWindowSource fills ExecutablePath

**Files:**
- Modify: `src/PcWrapped/Native/Win32ForegroundWindowSource.cs`

Platform code — verified by build + manual run.

- [ ] **Step 1: Capture MainModule path**

Replace the `GetForeground()` method body's process-resolution block so it also reads the executable path. The method becomes:
```csharp
    public ForegroundInfo? GetForeground()
    {
        var hwnd = GetForegroundWindow();
        if (hwnd == IntPtr.Zero) return null;
        GetWindowThreadProcessId(hwnd, out uint pid);
        if (pid == 0) return null;

        string process;
        string? path = null;
        try
        {
            var proc = Process.GetProcessById((int)pid);
            process = proc.ProcessName;
            try { path = proc.MainModule?.FileName; } catch { path = null; }
        }
        catch { return null; }

        var sb = new StringBuilder(512);
        GetWindowText(hwnd, sb, sb.Capacity);
        return new ForegroundInfo(process, sb.ToString(), path);
    }
```

- [ ] **Step 2: Build**

Run: `dotnet build src/PcWrapped`
Expected: Build succeeded (CA1416 warnings OK).

- [ ] **Step 3: Manual verification**

Temporarily add to `Program.cs` `Main` before `BuildAvaloniaApp`:
```csharp
var src = new PcWrapped.Native.Win32ForegroundWindowSource();
Console.WriteLine($"FG path: {src.GetForeground()?.ExecutablePath}");
```
Run: `dotnet run --project src/PcWrapped` (in background or with timeout; don't let the GUI hang). Expected: prints a real .exe path (e.g., the terminal/IDE exe). Then REMOVE the temporary lines and rebuild.

- [ ] **Step 4: Commit**

```bash
git add -A
git commit -m "feat: capture foreground executable path via Win32"
```

---

## Task 4: MainViewModel — periods + AppRowVm

**Files:**
- Create: `src/PcWrapped/ViewModels/AppRowVm.cs`
- Modify: `src/PcWrapped/ViewModels/MainViewModel.cs`
- Test: `tests/PcWrapped.App.Tests/AppRowVmTests.cs`

- [ ] **Step 1: Create AppRowVm with pure FromStats**

`src/PcWrapped/ViewModels/AppRowVm.cs`:
```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using PcWrapped.Core.Models;

namespace PcWrapped.ViewModels;

public sealed record AppRowVm(string Name, string TimeText, double Fraction, string? ExecutablePath)
{
    public static IReadOnlyList<AppRowVm> FromStats(
        PeriodStats stats, IReadOnlyDictionary<string, string> paths)
    {
        if (stats.TopApps.Count == 0) return Array.Empty<AppRowVm>();
        double max = stats.TopApps.Max(a => a.Duration.TotalSeconds);
        if (max <= 0) max = 1;
        return stats.TopApps.Select(a =>
        {
            paths.TryGetValue(a.ProcessName, out var p);
            return new AppRowVm(a.ProcessName, FormatHours(a.Duration),
                a.Duration.TotalSeconds / max, p);
        }).ToList();
    }

    private static string FormatHours(TimeSpan t) =>
        t.TotalHours >= 1 ? $"{(int)t.TotalHours}ч {t.Minutes:00}м" : $"{t.Minutes}м";
}
```

- [ ] **Step 2: Write failing test**

`tests/PcWrapped.App.Tests/AppRowVmTests.cs`:
```csharp
using System;
using System.Collections.Generic;
using PcWrapped.Core.Models;
using PcWrapped.ViewModels;
using Xunit;

namespace PcWrapped.App.Tests;

public class AppRowVmTests
{
    private static PeriodStats Stats(params AppUsage[] top) => new(
        new DateOnly(2026, 6, 9), new DateOnly(2026, 6, 15),
        TimeSpan.FromHours(3), top,
        new Dictionary<Category, TimeSpan>(), 22, new int[24], 5, 0, 0, 0);

    [Fact]
    public void FromStats_ComputesFractionTimeAndPath()
    {
        var stats = Stats(
            new AppUsage("code", TimeSpan.FromHours(2)),
            new AppUsage("chrome", TimeSpan.FromHours(1)));
        var rows = AppRowVm.FromStats(stats,
            new Dictionary<string, string> { ["code"] = @"C:\code.exe" });

        Assert.Equal(2, rows.Count);
        Assert.Equal("code", rows[0].Name);
        Assert.Equal(1.0, rows[0].Fraction, 3);
        Assert.Equal(0.5, rows[1].Fraction, 3);
        Assert.Equal("2ч 00м", rows[0].TimeText);
        Assert.Equal(@"C:\code.exe", rows[0].ExecutablePath);
        Assert.Null(rows[1].ExecutablePath);
    }

    [Fact]
    public void FromStats_EmptyTopApps_ReturnsEmpty()
    {
        Assert.Empty(AppRowVm.FromStats(Stats(), new Dictionary<string, string>()));
    }
}
```

- [ ] **Step 3: Run test to verify it fails, then passes**

Run: `dotnet test --filter AppRowVmTests`
Expected: FAIL first if AppRowVm missing; once Step 1 is in place, PASS (2 tests).

- [ ] **Step 4: Add periods + topAppLimit + paths accessor to MainViewModel**

Replace `src/PcWrapped/ViewModels/MainViewModel.cs` with:
```csharp
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using PcWrapped.Core.Aggregation;
using PcWrapped.Core.Categorization;
using PcWrapped.Core.Models;
using PcWrapped.Core.Storage;
using PcWrapped.Rendering;

namespace PcWrapped.ViewModels;

public enum StatsPeriod { Today, Week, Year }

public sealed class MainViewModel
{
    private readonly IStatsRepository _repo;
    private readonly Categorizer _categorizer = new(DefaultRules.Map);

    public CardTheme SelectedTheme { get; set; } = CardThemes.Gradient;
    public Avalonia.PixelSize SelectedSize { get; set; } = CardRenderer.Square;
    public StatsPeriod SelectedPeriod { get; set; } = StatsPeriod.Week;

    public MainViewModel(IStatsRepository repo) => _repo = repo;

    public async Task<PeriodStats> BuildStatsAsync(DateOnly today, double mouseDpi)
    {
        var from = SelectedPeriod switch
        {
            StatsPeriod.Today => today,
            StatsPeriod.Week => today.AddDays(-6),
            StatsPeriod.Year => today.AddDays(-364),
            _ => today.AddDays(-6),
        };
        var fromDt = new DateTimeOffset(from.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var toDt = new DateTimeOffset(today.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);

        var samples = await _repo.GetSamplesAsync(fromDt, toDt);
        var counters = await _repo.GetInputCountersAsync(from, today);
        var activeDays = await _repo.GetActiveDaysAsync();

        return Aggregator.BuildPeriodStats(from, today, samples, _categorizer,
            counters, activeDays, today, topAppLimit: 12, mouseDpi: mouseDpi);
    }

    public Task<IReadOnlyDictionary<string, string>> GetAppPathsAsync() => _repo.GetAppPathsAsync();

    public Task ExportAsync(PeriodStats stats, string path) =>
        Task.Run(() => CardRenderer.RenderToPng(stats, SelectedTheme, SelectedSize, path));
}
```

- [ ] **Step 5: Build + commit**

Run: `dotnet build PcWrapped.slnx` (0 errors). Note: `MainWindow.axaml.cs` still calls the old `BuildWeekStatsAsync` and will FAIL to compile here — that is expected and fixed in Task 7. To keep this task independently green, ALSO do the minimal edit in `MainWindow.axaml.cs`: rename the call `Vm.BuildWeekStatsAsync(` to `Vm.BuildStatsAsync(` (one occurrence in `RefreshAsync`). Then build again → 0 errors.

Run: `dotnet test PcWrapped.slnx` (all pass).
```bash
git add -A
git commit -m "feat: periods (today/week/year), top-12 apps, app-row view model"
```

---

## Task 5: AppIconProvider + converters

**Files:**
- Create: `src/PcWrapped/Native/AppIconProvider.cs`
- Create: `src/PcWrapped/Converters/PathToIconConverter.cs`
- Create: `src/PcWrapped/Converters/FirstLetterConverter.cs`
- Modify: `src/PcWrapped/PcWrapped.csproj` (System.Drawing.Common if needed)

- [ ] **Step 1: Add System.Drawing.Common**

Run: `dotnet add src/PcWrapped package System.Drawing.Common --version 8.0.6`
(If a later build shows the type already resolves without it, the package is harmless.)

- [ ] **Step 2: Create AppIconProvider**

`src/PcWrapped/Native/AppIconProvider.cs`:
```csharp
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using Avalonia.Media.Imaging;

namespace PcWrapped.Native;

/// <summary>Извлекает иконку .exe и кэширует её. Windows-only.</summary>
public static class AppIconProvider
{
    private static readonly Dictionary<string, Bitmap?> _cache =
        new(StringComparer.OrdinalIgnoreCase);

    public static Bitmap? GetIcon(string? exePath)
    {
        if (string.IsNullOrEmpty(exePath)) return null;
        if (_cache.TryGetValue(exePath, out var cached)) return cached;

        Bitmap? result = null;
        try
        {
            if (File.Exists(exePath))
            {
                using var icon = Icon.ExtractAssociatedIcon(exePath);
                if (icon is not null)
                {
                    using var sysBmp = icon.ToBitmap();
                    using var ms = new MemoryStream();
                    sysBmp.Save(ms, ImageFormat.Png);
                    ms.Position = 0;
                    result = new Bitmap(ms);
                }
            }
        }
        catch { result = null; }

        _cache[exePath] = result;
        return result;
    }
}
```

- [ ] **Step 3: Create PathToIconConverter**

`src/PcWrapped/Converters/PathToIconConverter.cs`:
```csharp
using System;
using System.Globalization;
using Avalonia.Data.Converters;
using PcWrapped.Native;

namespace PcWrapped.Converters;

public sealed class PathToIconConverter : IValueConverter
{
    public static readonly PathToIconConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => AppIconProvider.GetIcon(value as string);

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
```

- [ ] **Step 4: Create FirstLetterConverter (fallback glyph)**

`src/PcWrapped/Converters/FirstLetterConverter.cs`:
```csharp
using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace PcWrapped.Converters;

public sealed class FirstLetterConverter : IValueConverter
{
    public static readonly FirstLetterConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var s = value as string;
        return string.IsNullOrEmpty(s) ? "?" : s.Substring(0, 1).ToUpperInvariant();
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
```

- [ ] **Step 5: Build + commit**

Run: `dotnet build PcWrapped.slnx`
Expected: Build succeeded (0 errors; CA1416 warnings OK).
```bash
git add -A
git commit -m "feat: app icon provider and value converters"
```

---

## Task 6: App.axaml dark theme + styles

**Files:**
- Modify: `src/PcWrapped/App.axaml`

- [ ] **Step 1: Rewrite App.axaml with dark theme + styles**

Replace `src/PcWrapped/App.axaml` with (keep the existing `TrayIcon.Icons` block exactly as-is at the end):
```xml
<Application xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             x:Class="PcWrapped.App"
             RequestedThemeVariant="Dark">

    <Application.Resources>
        <Color x:Key="BgColor">#15161C</Color>
        <Color x:Key="PanelColor">#1C1E27</Color>
        <Color x:Key="RailColor">#101119</Color>
        <Color x:Key="AccentColor">#7B2FF7</Color>
        <SolidColorBrush x:Key="BgBrush" Color="#15161C"/>
        <SolidColorBrush x:Key="PanelBrush" Color="#1C1E27"/>
        <SolidColorBrush x:Key="RailBrush" Color="#101119"/>
        <SolidColorBrush x:Key="TextBrush" Color="#E8E8EE"/>
        <SolidColorBrush x:Key="MutedBrush" Color="#8A8F9B"/>
        <LinearGradientBrush x:Key="AccentBrush" StartPoint="0%,0%" EndPoint="100%,0%">
            <GradientStop Color="#7B2FF7" Offset="0"/>
            <GradientStop Color="#F107A3" Offset="1"/>
        </LinearGradientBrush>
    </Application.Resources>

    <Application.Styles>
        <FluentTheme />

        <Style Selector="TextBlock.lab">
            <Setter Property="FontSize" Value="10"/>
            <Setter Property="Foreground" Value="{StaticResource MutedBrush}"/>
            <Setter Property="LetterSpacing" Value="1"/>
        </Style>

        <Style Selector="RadioButton.tab">
            <Setter Property="Foreground" Value="{StaticResource MutedBrush}"/>
            <Setter Property="Padding" Value="8,5"/>
            <Setter Property="FontSize" Value="12"/>
        </Style>
        <Style Selector="RadioButton.tab:checked">
            <Setter Property="Foreground" Value="{StaticResource TextBrush}"/>
            <Setter Property="FontWeight" Value="SemiBold"/>
        </Style>

        <Style Selector="RadioButton.pill">
            <Setter Property="Foreground" Value="{StaticResource MutedBrush}"/>
            <Setter Property="Padding" Value="10,4"/>
            <Setter Property="FontSize" Value="11"/>
        </Style>
        <Style Selector="RadioButton.pill:checked">
            <Setter Property="Foreground" Value="{StaticResource TextBrush}"/>
            <Setter Property="FontWeight" Value="SemiBold"/>
        </Style>

        <Style Selector="Button.share">
            <Setter Property="Background" Value="{StaticResource AccentBrush}"/>
            <Setter Property="Foreground" Value="White"/>
            <Setter Property="FontWeight" Value="Bold"/>
            <Setter Property="HorizontalContentAlignment" Value="Center"/>
            <Setter Property="CornerRadius" Value="8"/>
            <Setter Property="Padding" Value="10"/>
        </Style>

        <Style Selector="Border.appTile">
            <Setter Property="Background" Value="{StaticResource PanelBrush}"/>
            <Setter Property="CornerRadius" Value="10"/>
        </Style>
        <Style Selector="Border.previewPane">
            <Setter Property="Background" Value="{StaticResource RailBrush}"/>
        </Style>
        <Style Selector="Border.rail">
            <Setter Property="Background" Value="{StaticResource RailBrush}"/>
        </Style>

        <Style Selector="ProgressBar.usage">
            <Setter Property="Foreground" Value="{StaticResource AccentBrush}"/>
            <Setter Property="Background" Value="#2A2D39"/>
            <Setter Property="MinHeight" Value="4"/>
        </Style>
    </Application.Styles>

    <TrayIcon.Icons>
        <TrayIcons>
            <TrayIcon ToolTipText="PC Wrapped">
                <TrayIcon.Menu>
                    <NativeMenu>
                        <NativeMenuItem Header="Открыть" Click="OnOpenClicked"/>
                        <NativeMenuItem Header="Выход" Click="OnExitClicked"/>
                    </NativeMenu>
                </TrayIcon.Menu>
            </TrayIcon>
        </TrayIcons>
    </TrayIcon.Icons>
</Application>
```

- [ ] **Step 2: Build**

Run: `dotnet build PcWrapped.slnx`
Expected: 0 errors. (If a `Setter Property="LetterSpacing"` is rejected by the Avalonia 11 build, remove that one setter line and rebuild.)

- [ ] **Step 3: Commit**

```bash
git add -A
git commit -m "feat: dark theme and shared control styles"
```

---

## Task 7: MainWindow redesign + live preview

**Files:**
- Rewrite: `src/PcWrapped/Views/MainWindow.axaml`
- Rewrite: `src/PcWrapped/Views/MainWindow.axaml.cs`
- Modify: `src/PcWrapped/Rendering/CardRenderer.cs` (add `RenderToBitmap`)

- [ ] **Step 1: Add RenderToBitmap to CardRenderer**

In `src/PcWrapped/Rendering/CardRenderer.cs`, add this method and refactor `RenderToPng` to use it:
```csharp
    /// <summary>Рендерит карточку в Avalonia Bitmap (для превью).</summary>
    public static RenderTargetBitmap RenderToBitmap(PeriodStats stats, CardTheme theme, PixelSize size)
    {
        var card = BuildCard(stats, theme, size);
        card.Measure(new Size(size.Width, size.Height));
        card.Arrange(new Rect(0, 0, size.Width, size.Height));
        var bmp = new RenderTargetBitmap(size, new Vector(96, 96));
        bmp.Render(card);
        return bmp;
    }

    /// <summary>Рендерит карточку в PNG-файл.</summary>
    public static void RenderToPng(PeriodStats stats, CardTheme theme, PixelSize size, string path)
    {
        using var bmp = RenderToBitmap(stats, theme, size);
        bmp.Save(path);
    }
```
(Delete the old body of `RenderToPng` that duplicated measure/arrange/render.)

- [ ] **Step 2: Rewrite MainWindow.axaml**

Replace `src/PcWrapped/Views/MainWindow.axaml` with:
```xml
<Window xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:conv="clr-namespace:PcWrapped.Converters"
        x:Class="PcWrapped.Views.MainWindow"
        Width="900" Height="620" MinWidth="760" MinHeight="520"
        Background="{StaticResource BgBrush}" Foreground="{StaticResource TextBrush}"
        Title="PC Wrapped">
  <Grid ColumnDefinitions="180,*,240">

    <!-- LEFT RAIL -->
    <Border Grid.Column="0" Classes="rail" Padding="16">
      <StackPanel Spacing="4">
        <TextBlock Text="📊 PC Wrapped" FontWeight="Bold" FontSize="16" Margin="0,0,0,14"/>
        <StackPanel x:Name="PeriodTabs" Spacing="2">
          <RadioButton GroupName="period" Classes="tab" Content="Сегодня" Tag="Today"/>
          <RadioButton GroupName="period" Classes="tab" Content="Неделя" Tag="Week" IsChecked="True"/>
          <RadioButton GroupName="period" Classes="tab" Content="Год" Tag="Year"/>
        </StackPanel>
        <TextBlock Classes="lab" Text="ТЕМА" Margin="0,14,0,6"/>
        <StackPanel x:Name="ThemeSwatches" Orientation="Horizontal" Spacing="8"/>
        <TextBlock Classes="lab" Text="ФОРМАТ" Margin="0,14,0,6"/>
        <StackPanel Orientation="Horizontal" Spacing="6">
          <RadioButton x:Name="SizeSquare" GroupName="size" Classes="pill" Content="1:1" IsChecked="True"/>
          <RadioButton x:Name="SizeStory" GroupName="size" Classes="pill" Content="9:16"/>
        </StackPanel>
        <Button x:Name="ExportBtn" Classes="share" Content="Поделиться ↗"
                HorizontalAlignment="Stretch" Margin="0,18,0,0"/>
        <TextBlock x:Name="Status" Classes="lab" Text="" Margin="0,10,0,0"/>
      </StackPanel>
    </Border>

    <!-- CENTER -->
    <Grid Grid.Column="1" RowDefinitions="Auto,Auto,*" Margin="20,16">
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

      <TextBlock Grid.Row="1" Classes="lab" Text="ТОП ПРИЛОЖЕНИЙ" Margin="0,14,0,6"/>

      <ScrollViewer Grid.Row="2">
        <ItemsControl x:Name="AppList">
          <ItemsControl.ItemsPanel>
            <ItemsPanelTemplate><UniformGrid Columns="2"/></ItemsPanelTemplate>
          </ItemsControl.ItemsPanel>
          <ItemsControl.ItemTemplate>
            <DataTemplate>
              <Border Classes="appTile" Margin="0,0,8,8" Padding="10">
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

    <!-- PREVIEW -->
    <Border Grid.Column="2" Classes="previewPane" Padding="14">
      <StackPanel>
        <TextBlock Classes="lab" Text="ПРЕВЬЮ"/>
        <Border CornerRadius="14" ClipToBounds="True" Margin="0,8,0,0">
          <Image x:Name="PreviewImage" Stretch="Uniform" MaxHeight="440"/>
        </Border>
      </StackPanel>
    </Border>

  </Grid>
</Window>
```

- [ ] **Step 3: Rewrite MainWindow.axaml.cs**

Replace `src/PcWrapped/Views/MainWindow.axaml.cs` with:
```csharp
using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using PcWrapped.Core.Models;
using PcWrapped.Rendering;
using PcWrapped.ViewModels;

namespace PcWrapped.Views;

public partial class MainWindow : Window
{
    private PeriodStats? _current;

    public MainWindow()
    {
        InitializeComponent();
        BuildThemeSwatches();

        var tabs = this.FindControl<StackPanel>("PeriodTabs")!;
        foreach (var child in tabs.Children)
            if (child is RadioButton rb)
                rb.IsCheckedChanged += async (_, _) => { if (rb.IsChecked == true) await RefreshAsync(); };

        this.FindControl<RadioButton>("SizeSquare")!.IsCheckedChanged += async (_, _) => await RefreshAsync();
        this.FindControl<RadioButton>("SizeStory")!.IsCheckedChanged += async (_, _) => await RefreshAsync();
        this.FindControl<Button>("ExportBtn")!.Click += OnExport;

        Opened += async (_, _) => await RefreshAsync();
    }

    private MainViewModel Vm => (MainViewModel)DataContext!;

    private void BuildThemeSwatches()
    {
        var panel = this.FindControl<StackPanel>("ThemeSwatches")!;
        foreach (var theme in CardThemes.All)
        {
            var captured = theme;
            var swatch = new Border
            {
                Width = 22, Height = 22, CornerRadius = new Avalonia.CornerRadius(6),
                Background = theme.Background,
                BorderBrush = Brushes.White, BorderThickness = new Avalonia.Thickness(0),
                Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand),
            };
            swatch.PointerPressed += async (_, _) =>
            {
                Vm.SelectedTheme = captured;
                await RefreshAsync();
            };
            panel.Children.Add(swatch);
        }
    }

    private StatsPeriod CurrentPeriod()
    {
        var tabs = this.FindControl<StackPanel>("PeriodTabs")!;
        foreach (var child in tabs.Children)
            if (child is RadioButton { IsChecked: true } rb && rb.Tag is string tag
                && Enum.TryParse<StatsPeriod>(tag, out var p))
                return p;
        return StatsPeriod.Week;
    }

    private static string PeriodLabelText(StatsPeriod p) => p switch
    {
        StatsPeriod.Today => "ТВОЙ ДЕНЬ ЗА ПК",
        StatsPeriod.Year => "ТВОЙ ГОД ЗА ПК",
        _ => "ТВОЯ НЕДЕЛЯ ЗА ПК",
    };

    private async Task RefreshAsync()
    {
        Vm.SelectedPeriod = CurrentPeriod();
        Vm.SelectedSize = this.FindControl<RadioButton>("SizeStory")!.IsChecked == true
            ? CardRenderer.Story : CardRenderer.Square;

        _current = await Vm.BuildStatsAsync(DateOnly.FromDateTime(DateTime.Now), mouseDpi: 96);
        var paths = await Vm.GetAppPathsAsync();

        this.FindControl<TextBlock>("PeriodLabel")!.Text = PeriodLabelText(Vm.SelectedPeriod);
        this.FindControl<TextBlock>("TotalText")!.Text =
            $"{(int)_current.TotalActive.TotalHours}ч {_current.TotalActive.Minutes:00}м";
        this.FindControl<TextBlock>("StreakText")!.Text = $"{_current.StreakDays} дн.";
        this.FindControl<ItemsControl>("AppList")!.ItemsSource = AppRowVm.FromStats(_current, paths);

        RenderPreview();
        this.FindControl<TextBlock>("Status")!.Text = "Готово";
    }

    private void RenderPreview()
    {
        if (_current is null) return;
        var bmp = CardRenderer.RenderToBitmap(_current, Vm.SelectedTheme, Vm.SelectedSize);
        this.FindControl<Image>("PreviewImage")!.Source = bmp;
    }

    private async void OnExport(object? sender, RoutedEventArgs e)
    {
        if (_current is null) return;
        var suffix = Vm.SelectedSize == CardRenderer.Story ? "story" : "square";
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            SuggestedFileName = $"pc-wrapped-{suffix}.png",
            DefaultExtension = "png",
        });
        if (file is null) return;
        await Vm.ExportAsync(_current, file.Path.LocalPath);
    }
}
```

- [ ] **Step 4: Build**

Run: `dotnet build PcWrapped.slnx`
Expected: 0 errors (CA1416 warnings OK).

- [ ] **Step 5: Run full test suite**

Run: `dotnet test PcWrapped.slnx`
Expected: all pass (headless renderer test still green).

- [ ] **Step 6: Manual verification**

Run the app (`dotnet run --project src/PcWrapped`, background or timed; don't let GUI hang). Use the PC for a bit first so there is data, OR just confirm the window renders: dark layout, left rail with period tabs / theme swatches / format / Поделиться, center app grid (icons appear for apps that have been tracked), right preview shows a card. Switching period/theme/format updates the view and preview. Export saves a PNG. Then close it.

- [ ] **Step 7: Commit**

```bash
git add -A
git commit -m "feat: redesign main window with app grid, icons, and live preview"
```

---

## Task 8: Mini app-icons on the shareable card

**Files:**
- Modify: `src/PcWrapped/Rendering/CardRenderer.cs`
- Modify: `src/PcWrapped/ViewModels/MainViewModel.cs`
- Modify: `src/PcWrapped/Views/MainWindow.axaml.cs`
- Test: `tests/PcWrapped.App.Tests/CardRendererTests.cs`

- [ ] **Step 1: Add optional icons to the renderer**

In `CardRenderer.cs`, change `BuildCard`, `RenderToBitmap`, and `RenderToPng` to accept an optional `IReadOnlyDictionary<string, IImage>? appIcons = null` and draw a mini-icon next to the #1 app row. Replace `BuildCard` with this complete version (keeps the existing look, adds the optional icon):
```csharp
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

        stack.Children.Add(new TextBlock
        {
            Text = "ТВОЯ НЕДЕЛЯ ЗА ПК", Foreground = new SolidColorBrush(theme.TextColor),
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
```
Add `using System.Collections.Generic;` and ensure `using Avalonia.Layout;` and `using Avalonia.Media;` are present. Update the two wrappers to forward the icons:
```csharp
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

    public static void RenderToPng(PeriodStats stats, CardTheme theme, PixelSize size, string path,
        IReadOnlyDictionary<string, IImage>? appIcons = null)
    {
        using var bmp = RenderToBitmap(stats, theme, size, appIcons);
        bmp.Save(path);
    }
```

- [ ] **Step 2: Build a helper in MainViewModel to collect icons**

In `MainViewModel.cs`, change `ExportAsync` to accept icons and add a render-icons passthrough. Update signature:
```csharp
    public Task ExportAsync(PeriodStats stats, string path,
        IReadOnlyDictionary<string, Avalonia.Media.IImage>? appIcons) =>
        Task.Run(() => CardRenderer.RenderToPng(stats, SelectedTheme, SelectedSize, path, appIcons));
```

- [ ] **Step 3: Resolve icons in the view and pass them through**

In `MainWindow.axaml.cs`, add a helper that builds the icon map from the current app rows using `AppIconProvider`, and use it in `OnExport` and `RenderPreview`:
```csharp
    private System.Collections.Generic.Dictionary<string, Avalonia.Media.IImage> CurrentIcons()
    {
        var map = new System.Collections.Generic.Dictionary<string, Avalonia.Media.IImage>();
        if (_current is null) return map;
        var paths = _currentPaths;
        if (paths is null) return map;
        foreach (var app in _current.TopApps)
            if (paths.TryGetValue(app.ProcessName, out var p))
            {
                var img = PcWrapped.Native.AppIconProvider.GetIcon(p);
                if (img is not null) map[app.ProcessName] = img;
            }
        return map;
    }
```
Add a field `private System.Collections.Generic.IReadOnlyDictionary<string,string>? _currentPaths;`, set it in `RefreshAsync` (`_currentPaths = paths;`), update `RenderPreview` to pass icons:
```csharp
    private void RenderPreview()
    {
        if (_current is null) return;
        var bmp = CardRenderer.RenderToBitmap(_current, Vm.SelectedTheme, Vm.SelectedSize, CurrentIcons());
        this.FindControl<Image>("PreviewImage")!.Source = bmp;
    }
```
and update `OnExport`'s last line:
```csharp
        await Vm.ExportAsync(_current, file.Path.LocalPath, CurrentIcons());
```

- [ ] **Step 4: Update the headless renderer test (still works without icons, plus a with-icons case)**

In `tests/PcWrapped.App.Tests/CardRendererTests.cs`, keep the existing no-icons test. Add a with-icons smoke test inside the class:
```csharp
    [AvaloniaFact]
    public void RenderToPng_WithIcons_DoesNotThrow()
    {
        var dir = Path.Combine(Path.GetTempPath(), "pcwrapped-test");
        Directory.CreateDirectory(dir);
        var icon = new Avalonia.Media.Imaging.RenderTargetBitmap(new Avalonia.PixelSize(16, 16));
        var icons = new Dictionary<string, Avalonia.Media.IImage> { ["code"] = icon };

        var path = Path.Combine(dir, "with-icons.png");
        CardRenderer.RenderToPng(Sample(), CardThemes.Gradient, CardRenderer.Square, path, icons);

        Assert.True(File.Exists(path));
        Assert.True(new FileInfo(path).Length > 1000);
    }
```
(`Sample()` already exists in the test file. Ensure `using System.Collections.Generic;` is present.)

- [ ] **Step 5: Build + test**

Run: `dotnet build PcWrapped.slnx` then `dotnet test PcWrapped.slnx`
Expected: 0 errors; all tests pass (including both renderer cases).

- [ ] **Step 6: Manual verification**

Run the app; export a card after using the PC. The exported PNG (and the live preview) shows a mini-icon next to the #1 app. Close the app.

- [ ] **Step 7: Commit**

```bash
git add -A
git commit -m "feat: show app icon on the shareable card and live preview"
```

---

## Final verification

- [ ] `dotnet test PcWrapped.slnx` — all tests pass (Core + App).
- [ ] `dotnet run --project src/PcWrapped` — dark redesigned window: rail (period/theme/format/share), center app grid with real .exe icons (fallback letter when missing), live card preview; switching period/theme/format updates everything; export writes 1:1 and 9:16 PNGs with the app icon.

---

## Spec Coverage Notes

- ExecutablePath capture + app_paths storage → Tasks 1, 3.
- Tracker records path → Task 2.
- AppIconProvider + cache + fallback → Tasks 5, 7 (fallback in XAML).
- Dark redesign (rail, app grid, preview), periods, top-12 → Tasks 4, 6, 7.
- Live preview → Task 7.
- Card mini-icons (optional, headless test stays green) → Task 8.
- Tests (app_paths, tracker-path, AppRowVm, renderer-with-icons) → Tasks 1, 2, 4, 8.
- Out of scope (disk icon cache, hi-res icons, pie/heatmap, animations) → not included.
```
