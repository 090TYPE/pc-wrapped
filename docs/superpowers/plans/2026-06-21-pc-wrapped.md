# PC Wrapped Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Фоновое Windows-приложение, которое локально собирает статистику использования ПК и генерирует красивые шеринг-карточки (Spotify-Wrapped для компьютера).

**Architecture:** Вся чистая логика (модели, категоризация, агрегация, хранение) живёт в библиотеке `PcWrapped.Core` и покрыта юнит-тестами. Avalonia-приложение `PcWrapped` отвечает за UI, трей, нативные хуки ввода и рендер карточек в PNG. Поток данных: Tracker → SQLite (сырьё) → Aggregator (метрики) → CardRenderer (PNG).

**Tech Stack:** .NET 8, Avalonia 11, C#, Microsoft.Data.Sqlite, xUnit, P/Invoke (user32.dll).

---

## File Structure

```
PcWrapped.sln
src/PcWrapped.Core/
  PcWrapped.Core.csproj
  Models/Category.cs                 # enum категорий
  Models/UsageSample.cs              # один интервал работы в приложении
  Models/AppUsage.cs                 # агрегат: приложение + длительность
  Models/InputCounters.cs           # счётчики ввода
  Models/PeriodStats.cs             # итоговые метрики периода
  Categorization/Categorizer.cs     # процесс -> категория
  Categorization/DefaultRules.cs    # встроенный словарь
  Aggregation/Aggregator.cs         # чистые функции метрик
  Aggregation/VanityMath.cs         # пиксели -> км и пр.
  Storage/IStatsRepository.cs       # интерфейс хранения
  Storage/SqliteStatsRepository.cs  # реализация на SQLite
  Tracking/IForegroundWindowSource.cs  # абстракция активного окна
  Tracking/IInputCounterSource.cs      # абстракция счётчиков ввода
  Tracking/ActivityTracker.cs          # сборщик (использует абстракции)
tests/PcWrapped.Core.Tests/
  PcWrapped.Core.Tests.csproj
  CategorizerTests.cs
  VanityMathTests.cs
  AggregatorTests.cs
  SqliteStatsRepositoryTests.cs
  ActivityTrackerTests.cs
src/PcWrapped/
  PcWrapped.csproj
  Program.cs
  App.axaml / App.axaml.cs
  Native/Win32ForegroundWindowSource.cs  # P/Invoke активное окно
  Native/Win32InputCounterSource.cs      # P/Invoke хуки ввода
  Native/AutostartManager.cs             # реестр Run
  Rendering/CardTheme.cs                  # описание темы
  Rendering/CardThemes.cs                 # 3 встроенные темы
  Rendering/CardRenderer.cs              # метрики+тема -> PNG (1:1, 9:16)
  ViewModels/MainViewModel.cs
  Views/MainWindow.axaml / .cs
  Views/OnboardingWindow.axaml / .cs
```

---

## Task 1: Solution scaffolding

**Files:**
- Create: `PcWrapped.sln`
- Create: `src/PcWrapped.Core/PcWrapped.Core.csproj`
- Create: `tests/PcWrapped.Core.Tests/PcWrapped.Core.Tests.csproj`
- Test: `tests/PcWrapped.Core.Tests/SmokeTest.cs`

- [ ] **Step 1: Create solution and Core class library**

```bash
cd /c/Users/090/pc-wrapped
dotnet new sln -n PcWrapped
dotnet new classlib -n PcWrapped.Core -o src/PcWrapped.Core -f net8.0
dotnet new xunit -n PcWrapped.Core.Tests -o tests/PcWrapped.Core.Tests -f net8.0
rm src/PcWrapped.Core/Class1.cs tests/PcWrapped.Core.Tests/UnitTest1.cs
dotnet sln add src/PcWrapped.Core tests/PcWrapped.Core.Tests
dotnet add tests/PcWrapped.Core.Tests reference src/PcWrapped.Core
```

- [ ] **Step 2: Write a smoke test**

Create `tests/PcWrapped.Core.Tests/SmokeTest.cs`:

```csharp
namespace PcWrapped.Core.Tests;

public class SmokeTest
{
    [Fact]
    public void Toolchain_Works()
    {
        Assert.Equal(4, 2 + 2);
    }
}
```

- [ ] **Step 3: Run tests — verify toolchain builds and passes**

Run: `dotnet test`
Expected: PASS (1 test passed).

- [ ] **Step 4: Commit**

```bash
git add -A
git commit -m "chore: scaffold solution, Core lib, and test project"
```

---

## Task 2: Domain models

**Files:**
- Create: `src/PcWrapped.Core/Models/Category.cs`
- Create: `src/PcWrapped.Core/Models/UsageSample.cs`
- Create: `src/PcWrapped.Core/Models/AppUsage.cs`
- Create: `src/PcWrapped.Core/Models/InputCounters.cs`
- Create: `src/PcWrapped.Core/Models/PeriodStats.cs`

These are plain data types with no behavior, so no dedicated tests — they are exercised by later tasks.

- [ ] **Step 1: Create the model files**

`Models/Category.cs`:
```csharp
namespace PcWrapped.Core.Models;

public enum Category { Work, Games, Social, Browser, Other }
```

`Models/UsageSample.cs`:
```csharp
namespace PcWrapped.Core.Models;

/// <summary>Один зафиксированный интервал активности в приложении.</summary>
public sealed record UsageSample(
    DateTimeOffset Start,
    string ProcessName,
    string WindowTitle,
    int DurationSeconds);
```

`Models/AppUsage.cs`:
```csharp
namespace PcWrapped.Core.Models;

public sealed record AppUsage(string ProcessName, TimeSpan Duration);
```

`Models/InputCounters.cs`:
```csharp
namespace PcWrapped.Core.Models;

/// <summary>Только счётчики — никогда не содержимое ввода.</summary>
public sealed record InputCounters(long Keystrokes, long Clicks, double MousePixels)
{
    public static readonly InputCounters Zero = new(0, 0, 0);

    public InputCounters Add(InputCounters other) =>
        new(Keystrokes + other.Keystrokes,
            Clicks + other.Clicks,
            MousePixels + other.MousePixels);
}
```

`Models/PeriodStats.cs`:
```csharp
namespace PcWrapped.Core.Models;

public sealed record PeriodStats(
    DateOnly From,
    DateOnly To,
    TimeSpan TotalActive,
    IReadOnlyList<AppUsage> TopApps,
    IReadOnlyDictionary<Category, TimeSpan> ByCategory,
    int PeakHour,                 // 0..23, -1 если данных нет
    IReadOnlyList<int> HourlySeconds,  // длина 24
    int StreakDays,
    long Keystrokes,
    long Clicks,
    double MouseKilometers);
```

- [ ] **Step 2: Build to verify it compiles**

Run: `dotnet build src/PcWrapped.Core`
Expected: Build succeeded.

- [ ] **Step 3: Commit**

```bash
git add -A
git commit -m "feat: add core domain models"
```

---

## Task 3: VanityMath (pixels → km)

**Files:**
- Create: `src/PcWrapped.Core/Aggregation/VanityMath.cs`
- Test: `tests/PcWrapped.Core.Tests/VanityMathTests.cs`

- [ ] **Step 1: Write the failing test**

`tests/PcWrapped.Core.Tests/VanityMathTests.cs`:
```csharp
using PcWrapped.Core.Aggregation;

namespace PcWrapped.Core.Tests;

public class VanityMathTests
{
    [Fact]
    public void PixelsToKilometers_At96Dpi_ConvertsCorrectly()
    {
        // 96 px @ 96 dpi = 1 inch = 0.0254 m = 0.0000254 km
        double km = VanityMath.PixelsToKilometers(96, dpi: 96);
        Assert.Equal(0.0000254, km, 9);
    }

    [Fact]
    public void PixelsToKilometers_Zero_IsZero()
    {
        Assert.Equal(0, VanityMath.PixelsToKilometers(0, 96));
    }

    [Fact]
    public void PixelsToKilometers_InvalidDpi_FallsBackTo96()
    {
        double a = VanityMath.PixelsToKilometers(96, dpi: 0);
        Assert.Equal(0.0000254, a, 9);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test --filter VanityMathTests`
Expected: FAIL (VanityMath does not exist).

- [ ] **Step 3: Write minimal implementation**

`src/PcWrapped.Core/Aggregation/VanityMath.cs`:
```csharp
namespace PcWrapped.Core.Aggregation;

public static class VanityMath
{
    private const double MetersPerInch = 0.0254;

    public static double PixelsToKilometers(double pixels, double dpi)
    {
        if (dpi <= 0) dpi = 96;
        double inches = pixels / dpi;
        double meters = inches * MetersPerInch;
        return meters / 1000.0;
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test --filter VanityMathTests`
Expected: PASS (3 tests).

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "feat: add VanityMath pixels-to-kilometers conversion"
```

---

## Task 4: Categorizer

**Files:**
- Create: `src/PcWrapped.Core/Categorization/Categorizer.cs`
- Create: `src/PcWrapped.Core/Categorization/DefaultRules.cs`
- Test: `tests/PcWrapped.Core.Tests/CategorizerTests.cs`

- [ ] **Step 1: Write the failing test**

`tests/PcWrapped.Core.Tests/CategorizerTests.cs`:
```csharp
using PcWrapped.Core.Categorization;
using PcWrapped.Core.Models;

namespace PcWrapped.Core.Tests;

public class CategorizerTests
{
    [Fact]
    public void Categorize_KnownProcess_ReturnsMappedCategory()
    {
        var c = new Categorizer(new Dictionary<string, Category>
        {
            ["chrome"] = Category.Browser
        });
        Assert.Equal(Category.Browser, c.Categorize("chrome"));
    }

    [Fact]
    public void Categorize_IsCaseInsensitive_AndIgnoresExe()
    {
        var c = new Categorizer(new Dictionary<string, Category>
        {
            ["code"] = Category.Work
        });
        Assert.Equal(Category.Work, c.Categorize("Code.exe"));
        Assert.Equal(Category.Work, c.Categorize("CODE"));
    }

    [Fact]
    public void Categorize_Unknown_ReturnsOther()
    {
        var c = new Categorizer(new Dictionary<string, Category>());
        Assert.Equal(Category.Other, c.Categorize("whatever"));
    }

    [Fact]
    public void DefaultRules_ContainsCommonApps()
    {
        var c = new Categorizer(DefaultRules.Map);
        Assert.Equal(Category.Browser, c.Categorize("chrome"));
        Assert.Equal(Category.Work, c.Categorize("code"));
        Assert.Equal(Category.Social, c.Categorize("discord"));
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test --filter CategorizerTests`
Expected: FAIL (Categorizer/DefaultRules do not exist).

- [ ] **Step 3: Write minimal implementation**

`src/PcWrapped.Core/Categorization/Categorizer.cs`:
```csharp
using PcWrapped.Core.Models;

namespace PcWrapped.Core.Categorization;

public sealed class Categorizer
{
    private readonly Dictionary<string, Category> _rules;

    public Categorizer(IReadOnlyDictionary<string, Category> rules)
    {
        _rules = new Dictionary<string, Category>(StringComparer.OrdinalIgnoreCase);
        foreach (var kv in rules) _rules[Normalize(kv.Key)] = kv.Value;
    }

    public Category Categorize(string processName)
    {
        if (string.IsNullOrWhiteSpace(processName)) return Category.Other;
        return _rules.TryGetValue(Normalize(processName), out var cat) ? cat : Category.Other;
    }

    private static string Normalize(string name)
    {
        name = name.Trim();
        if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            name = name[..^4];
        return name;
    }
}
```

`src/PcWrapped.Core/Categorization/DefaultRules.cs`:
```csharp
using PcWrapped.Core.Models;

namespace PcWrapped.Core.Categorization;

public static class DefaultRules
{
    public static readonly IReadOnlyDictionary<string, Category> Map =
        new Dictionary<string, Category>(StringComparer.OrdinalIgnoreCase)
        {
            ["chrome"] = Category.Browser,
            ["msedge"] = Category.Browser,
            ["firefox"] = Category.Browser,
            ["code"] = Category.Work,
            ["devenv"] = Category.Work,
            ["rider64"] = Category.Work,
            ["excel"] = Category.Work,
            ["winword"] = Category.Work,
            ["discord"] = Category.Social,
            ["telegram"] = Category.Social,
            ["slack"] = Category.Social,
            ["steam"] = Category.Games,
            ["steamwebhelper"] = Category.Games,
        };
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test --filter CategorizerTests`
Expected: PASS (4 tests).

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "feat: add Categorizer with default rules"
```

---

## Task 5: Aggregator — totals, top apps, categories

**Files:**
- Create: `src/PcWrapped.Core/Aggregation/Aggregator.cs`
- Test: `tests/PcWrapped.Core.Tests/AggregatorTests.cs`

- [ ] **Step 1: Write the failing test**

`tests/PcWrapped.Core.Tests/AggregatorTests.cs`:
```csharp
using PcWrapped.Core.Aggregation;
using PcWrapped.Core.Categorization;
using PcWrapped.Core.Models;

namespace PcWrapped.Core.Tests;

public class AggregatorTests
{
    private static UsageSample S(string proc, string day, int hour, int seconds) =>
        new(new DateTimeOffset(DateTime.Parse($"{day}T{hour:00}:00:00")), proc, proc, seconds);

    [Fact]
    public void TotalActive_SumsAllDurations()
    {
        var samples = new[] { S("code", "2026-06-09", 10, 60), S("chrome", "2026-06-09", 11, 120) };
        Assert.Equal(TimeSpan.FromSeconds(180), Aggregator.TotalActive(samples));
    }

    [Fact]
    public void TopApps_OrdersByDurationDescending_AndLimits()
    {
        var samples = new[]
        {
            S("code", "2026-06-09", 10, 300),
            S("chrome", "2026-06-09", 11, 100),
            S("code", "2026-06-09", 12, 200),
            S("steam", "2026-06-09", 13, 50),
        };
        var top = Aggregator.TopApps(samples, 2);
        Assert.Equal(2, top.Count);
        Assert.Equal("code", top[0].ProcessName);
        Assert.Equal(TimeSpan.FromSeconds(500), top[0].Duration);
        Assert.Equal("chrome", top[1].ProcessName);
    }

    [Fact]
    public void ByCategory_GroupsDurationsUsingCategorizer()
    {
        var cat = new Categorizer(DefaultRules.Map);
        var samples = new[]
        {
            S("code", "2026-06-09", 10, 100),
            S("chrome", "2026-06-09", 11, 50),
            S("unknownApp", "2026-06-09", 12, 25),
        };
        var byCat = Aggregator.ByCategory(samples, cat);
        Assert.Equal(TimeSpan.FromSeconds(100), byCat[Category.Work]);
        Assert.Equal(TimeSpan.FromSeconds(50), byCat[Category.Browser]);
        Assert.Equal(TimeSpan.FromSeconds(25), byCat[Category.Other]);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test --filter AggregatorTests`
Expected: FAIL (Aggregator does not exist).

- [ ] **Step 3: Write minimal implementation**

`src/PcWrapped.Core/Aggregation/Aggregator.cs`:
```csharp
using PcWrapped.Core.Categorization;
using PcWrapped.Core.Models;

namespace PcWrapped.Core.Aggregation;

public static class Aggregator
{
    public static TimeSpan TotalActive(IEnumerable<UsageSample> samples) =>
        TimeSpan.FromSeconds(samples.Sum(s => (long)s.DurationSeconds));

    public static IReadOnlyList<AppUsage> TopApps(IEnumerable<UsageSample> samples, int limit) =>
        samples
            .GroupBy(s => s.ProcessName)
            .Select(g => new AppUsage(g.Key, TimeSpan.FromSeconds(g.Sum(x => (long)x.DurationSeconds))))
            .OrderByDescending(a => a.Duration)
            .Take(limit)
            .ToList();

    public static IReadOnlyDictionary<Category, TimeSpan> ByCategory(
        IEnumerable<UsageSample> samples, Categorizer categorizer)
    {
        var result = new Dictionary<Category, TimeSpan>();
        foreach (var s in samples)
        {
            var cat = categorizer.Categorize(s.ProcessName);
            result.TryGetValue(cat, out var cur);
            result[cat] = cur + TimeSpan.FromSeconds(s.DurationSeconds);
        }
        return result;
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test --filter AggregatorTests`
Expected: PASS (3 tests).

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "feat: add Aggregator totals, top apps, categories"
```

---

## Task 6: Aggregator — peak hour, hourly heatmap, streak

**Files:**
- Modify: `src/PcWrapped.Core/Aggregation/Aggregator.cs`
- Modify: `tests/PcWrapped.Core.Tests/AggregatorTests.cs`

- [ ] **Step 1: Add failing tests**

Append to `AggregatorTests.cs` (inside the class):
```csharp
    [Fact]
    public void HourlySeconds_BucketsByHourOfDay_Length24()
    {
        var samples = new[] { S("code", "2026-06-09", 10, 60), S("code", "2026-06-09", 10, 30), S("x", "2026-06-09", 22, 90) };
        var hourly = Aggregator.HourlySeconds(samples);
        Assert.Equal(24, hourly.Count);
        Assert.Equal(90, hourly[10]);
        Assert.Equal(90, hourly[22]);
        Assert.Equal(0, hourly[0]);
    }

    [Fact]
    public void PeakHour_ReturnsHourWithMostSeconds()
    {
        var samples = new[] { S("code", "2026-06-09", 10, 60), S("x", "2026-06-09", 22, 200) };
        Assert.Equal(22, Aggregator.PeakHour(samples));
    }

    [Fact]
    public void PeakHour_NoData_ReturnsMinusOne()
    {
        Assert.Equal(-1, Aggregator.PeakHour(Array.Empty<UsageSample>()));
    }

    [Fact]
    public void Streak_CountsConsecutiveDaysEndingToday()
    {
        var today = new DateOnly(2026, 6, 15);
        var activeDays = new[]
        {
            new DateOnly(2026, 6, 15),
            new DateOnly(2026, 6, 14),
            new DateOnly(2026, 6, 13),
            new DateOnly(2026, 6, 11), // gap on the 12th
        };
        Assert.Equal(3, Aggregator.Streak(activeDays, today));
    }

    [Fact]
    public void Streak_NoActivityToday_IsZero()
    {
        var today = new DateOnly(2026, 6, 15);
        var activeDays = new[] { new DateOnly(2026, 6, 14) };
        Assert.Equal(0, Aggregator.Streak(activeDays, today));
    }
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test --filter AggregatorTests`
Expected: FAIL (HourlySeconds/PeakHour/Streak do not exist).

- [ ] **Step 3: Add the implementations**

Add these methods to `Aggregator`:
```csharp
    public static IReadOnlyList<int> HourlySeconds(IEnumerable<UsageSample> samples)
    {
        var buckets = new int[24];
        foreach (var s in samples)
            buckets[s.Start.Hour] += s.DurationSeconds;
        return buckets;
    }

    public static int PeakHour(IEnumerable<UsageSample> samples)
    {
        var hourly = HourlySeconds(samples);
        if (hourly.All(v => v == 0)) return -1;
        int best = 0;
        for (int h = 1; h < 24; h++)
            if (hourly[h] > hourly[best]) best = h;
        return best;
    }

    public static int Streak(IEnumerable<DateOnly> activeDays, DateOnly today)
    {
        var set = activeDays.ToHashSet();
        int streak = 0;
        var day = today;
        while (set.Contains(day))
        {
            streak++;
            day = day.AddDays(-1);
        }
        return streak;
    }
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test --filter AggregatorTests`
Expected: PASS (all AggregatorTests).

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "feat: add peak hour, hourly heatmap, and streak aggregation"
```

---

## Task 7: Storage — IStatsRepository + SQLite implementation

**Files:**
- Create: `src/PcWrapped.Core/Storage/IStatsRepository.cs`
- Create: `src/PcWrapped.Core/Storage/SqliteStatsRepository.cs`
- Test: `tests/PcWrapped.Core.Tests/SqliteStatsRepositoryTests.cs`
- Modify: `src/PcWrapped.Core/PcWrapped.Core.csproj` (add Microsoft.Data.Sqlite)

- [ ] **Step 1: Add the SQLite package**

```bash
dotnet add src/PcWrapped.Core package Microsoft.Data.Sqlite --version 8.0.6
```

- [ ] **Step 2: Define the interface**

`src/PcWrapped.Core/Storage/IStatsRepository.cs`:
```csharp
using PcWrapped.Core.Models;

namespace PcWrapped.Core.Storage;

public interface IStatsRepository
{
    Task InitializeAsync();
    Task AddSampleAsync(UsageSample sample);
    Task<IReadOnlyList<UsageSample>> GetSamplesAsync(DateTimeOffset fromInclusive, DateTimeOffset toExclusive);
    Task AddInputCountersAsync(DateOnly day, InputCounters delta);
    Task<InputCounters> GetInputCountersAsync(DateOnly fromInclusive, DateOnly toInclusive);
    Task<IReadOnlyList<DateOnly>> GetActiveDaysAsync();
}
```

- [ ] **Step 3: Write the failing tests**

`tests/PcWrapped.Core.Tests/SqliteStatsRepositoryTests.cs`:
```csharp
using PcWrapped.Core.Models;
using PcWrapped.Core.Storage;

namespace PcWrapped.Core.Tests;

public class SqliteStatsRepositoryTests
{
    // Each test uses a unique in-memory shared DB so the connection stays alive.
    private static async Task<SqliteStatsRepository> NewRepoAsync()
    {
        var cs = $"Data Source=file:{Guid.NewGuid():N}?mode=memory&cache=shared";
        var repo = new SqliteStatsRepository(cs);
        await repo.InitializeAsync();
        return repo;
    }

    [Fact]
    public async Task AddAndGetSamples_RoundTripsWithinRange()
    {
        var repo = await NewRepoAsync();
        var t = new DateTimeOffset(2026, 6, 9, 10, 0, 0, TimeSpan.Zero);
        await repo.AddSampleAsync(new UsageSample(t, "code", "main.cs", 60));
        await repo.AddSampleAsync(new UsageSample(t.AddDays(5), "chrome", "tab", 30));

        var inRange = await repo.GetSamplesAsync(t.AddHours(-1), t.AddHours(1));
        Assert.Single(inRange);
        Assert.Equal("code", inRange[0].ProcessName);
        Assert.Equal(60, inRange[0].DurationSeconds);
    }

    [Fact]
    public async Task InputCounters_AccumulateAcrossCallsAndDays()
    {
        var repo = await NewRepoAsync();
        var d = new DateOnly(2026, 6, 9);
        await repo.AddInputCountersAsync(d, new InputCounters(10, 2, 100));
        await repo.AddInputCountersAsync(d, new InputCounters(5, 1, 50));
        await repo.AddInputCountersAsync(d.AddDays(1), new InputCounters(7, 3, 25));

        var total = await repo.GetInputCountersAsync(d, d.AddDays(1));
        Assert.Equal(22, total.Keystrokes);
        Assert.Equal(6, total.Clicks);
        Assert.Equal(175, total.MousePixels);
    }

    [Fact]
    public async Task GetActiveDays_ReturnsDistinctSampleDays()
    {
        var repo = await NewRepoAsync();
        var t = new DateTimeOffset(2026, 6, 9, 10, 0, 0, TimeSpan.Zero);
        await repo.AddSampleAsync(new UsageSample(t, "code", "x", 60));
        await repo.AddSampleAsync(new UsageSample(t.AddHours(2), "code", "x", 60));
        await repo.AddSampleAsync(new UsageSample(t.AddDays(1), "code", "x", 60));

        var days = await repo.GetActiveDaysAsync();
        Assert.Equal(2, days.Count);
        Assert.Contains(new DateOnly(2026, 6, 9), days);
        Assert.Contains(new DateOnly(2026, 6, 10), days);
    }
}
```

- [ ] **Step 4: Run tests to verify they fail**

Run: `dotnet test --filter SqliteStatsRepositoryTests`
Expected: FAIL (SqliteStatsRepository does not exist).

- [ ] **Step 5: Implement the repository**

`src/PcWrapped.Core/Storage/SqliteStatsRepository.cs`:
```csharp
using Microsoft.Data.Sqlite;
using PcWrapped.Core.Models;

namespace PcWrapped.Core.Storage;

public sealed class SqliteStatsRepository : IStatsRepository, IDisposable
{
    private readonly SqliteConnection _conn;

    public SqliteStatsRepository(string connectionString)
    {
        _conn = new SqliteConnection(connectionString);
        _conn.Open(); // keep open so in-memory shared DB survives
    }

    public async Task InitializeAsync()
    {
        const string sql = @"
CREATE TABLE IF NOT EXISTS samples (
    start_unix INTEGER NOT NULL,
    process    TEXT    NOT NULL,
    title      TEXT    NOT NULL,
    seconds    INTEGER NOT NULL
);
CREATE INDEX IF NOT EXISTS ix_samples_start ON samples(start_unix);
CREATE TABLE IF NOT EXISTS input_counters (
    day        TEXT    PRIMARY KEY,
    keystrokes INTEGER NOT NULL,
    clicks     INTEGER NOT NULL,
    pixels     REAL    NOT NULL
);";
        await using var cmd = _conn.CreateCommand();
        cmd.CommandText = sql;
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task AddSampleAsync(UsageSample sample)
    {
        await using var cmd = _conn.CreateCommand();
        cmd.CommandText =
            "INSERT INTO samples (start_unix, process, title, seconds) VALUES ($s,$p,$t,$d)";
        cmd.Parameters.AddWithValue("$s", sample.Start.ToUnixTimeSeconds());
        cmd.Parameters.AddWithValue("$p", sample.ProcessName);
        cmd.Parameters.AddWithValue("$t", sample.WindowTitle);
        cmd.Parameters.AddWithValue("$d", sample.DurationSeconds);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<IReadOnlyList<UsageSample>> GetSamplesAsync(
        DateTimeOffset fromInclusive, DateTimeOffset toExclusive)
    {
        var list = new List<UsageSample>();
        await using var cmd = _conn.CreateCommand();
        cmd.CommandText =
            "SELECT start_unix, process, title, seconds FROM samples " +
            "WHERE start_unix >= $from AND start_unix < $to";
        cmd.Parameters.AddWithValue("$from", fromInclusive.ToUnixTimeSeconds());
        cmd.Parameters.AddWithValue("$to", toExclusive.ToUnixTimeSeconds());
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
        {
            list.Add(new UsageSample(
                DateTimeOffset.FromUnixTimeSeconds(r.GetInt64(0)),
                r.GetString(1), r.GetString(2), r.GetInt32(3)));
        }
        return list;
    }

    public async Task AddInputCountersAsync(DateOnly day, InputCounters delta)
    {
        await using var cmd = _conn.CreateCommand();
        cmd.CommandText = @"
INSERT INTO input_counters (day, keystrokes, clicks, pixels)
VALUES ($day, $k, $c, $p)
ON CONFLICT(day) DO UPDATE SET
    keystrokes = keystrokes + $k,
    clicks     = clicks + $c,
    pixels     = pixels + $p;";
        cmd.Parameters.AddWithValue("$day", day.ToString("yyyy-MM-dd"));
        cmd.Parameters.AddWithValue("$k", delta.Keystrokes);
        cmd.Parameters.AddWithValue("$c", delta.Clicks);
        cmd.Parameters.AddWithValue("$p", delta.MousePixels);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<InputCounters> GetInputCountersAsync(
        DateOnly fromInclusive, DateOnly toInclusive)
    {
        await using var cmd = _conn.CreateCommand();
        cmd.CommandText =
            "SELECT COALESCE(SUM(keystrokes),0), COALESCE(SUM(clicks),0), COALESCE(SUM(pixels),0) " +
            "FROM input_counters WHERE day >= $from AND day <= $to";
        cmd.Parameters.AddWithValue("$from", fromInclusive.ToString("yyyy-MM-dd"));
        cmd.Parameters.AddWithValue("$to", toInclusive.ToString("yyyy-MM-dd"));
        await using var r = await cmd.ExecuteReaderAsync();
        await r.ReadAsync();
        return new InputCounters(r.GetInt64(0), r.GetInt64(1), r.GetDouble(2));
    }

    public async Task<IReadOnlyList<DateOnly>> GetActiveDaysAsync()
    {
        var days = new List<DateOnly>();
        await using var cmd = _conn.CreateCommand();
        cmd.CommandText =
            "SELECT DISTINCT date(start_unix, 'unixepoch') FROM samples ORDER BY 1";
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            days.Add(DateOnly.Parse(r.GetString(0)));
        return days;
    }

    public void Dispose() => _conn.Dispose();
}
```

- [ ] **Step 6: Run tests to verify they pass**

Run: `dotnet test --filter SqliteStatsRepositoryTests`
Expected: PASS (3 tests).

- [ ] **Step 7: Commit**

```bash
git add -A
git commit -m "feat: add SQLite stats repository"
```

---

## Task 8: ActivityTracker (foreground polling + idle, behind interfaces)

**Files:**
- Create: `src/PcWrapped.Core/Tracking/IForegroundWindowSource.cs`
- Create: `src/PcWrapped.Core/Tracking/IInputCounterSource.cs`
- Create: `src/PcWrapped.Core/Tracking/ActivityTracker.cs`
- Test: `tests/PcWrapped.Core.Tests/ActivityTrackerTests.cs`

The tracker contains the testable timing/idle logic. Native sources are mocked.

- [ ] **Step 1: Define the source interfaces**

`src/PcWrapped.Core/Tracking/IForegroundWindowSource.cs`:
```csharp
namespace PcWrapped.Core.Tracking;

public readonly record struct ForegroundInfo(string ProcessName, string WindowTitle);

public interface IForegroundWindowSource
{
    /// <summary>Текущее активное окно, или null если рабочий стол/неизвестно.</summary>
    ForegroundInfo? GetForeground();

    /// <summary>Секунд с последнего ввода пользователя (для определения простоя).</summary>
    int GetIdleSeconds();
}
```

`src/PcWrapped.Core/Tracking/IInputCounterSource.cs`:
```csharp
using PcWrapped.Core.Models;

namespace PcWrapped.Core.Tracking;

public interface IInputCounterSource
{
    /// <summary>Возвращает накопленные счётчики и обнуляет их.</summary>
    InputCounters DrainCounters();
}
```

- [ ] **Step 2: Write the failing tests**

`tests/PcWrapped.Core.Tests/ActivityTrackerTests.cs`:
```csharp
using PcWrapped.Core.Models;
using PcWrapped.Core.Storage;
using PcWrapped.Core.Tracking;

namespace PcWrapped.Core.Tests;

public class ActivityTrackerTests
{
    private sealed class FakeForeground : IForegroundWindowSource
    {
        public ForegroundInfo? Current;
        public int Idle;
        public ForegroundInfo? GetForeground() => Current;
        public int GetIdleSeconds() => Idle;
    }

    private sealed class FakeInput : IInputCounterSource
    {
        public InputCounters Pending = InputCounters.Zero;
        public InputCounters DrainCounters()
        {
            var c = Pending; Pending = InputCounters.Zero; return c;
        }
    }

    private static async Task<SqliteStatsRepository> NewRepoAsync()
    {
        var repo = new SqliteStatsRepository(
            $"Data Source=file:{Guid.NewGuid():N}?mode=memory&cache=shared");
        await repo.InitializeAsync();
        return repo;
    }

    [Fact]
    public async Task Tick_ActiveWindow_RecordsSampleWithIntervalSeconds()
    {
        var repo = await NewRepoAsync();
        var fg = new FakeForeground { Current = new ForegroundInfo("code", "main.cs"), Idle = 0 };
        var input = new FakeInput();
        var now = new DateTimeOffset(2026, 6, 9, 10, 0, 0, TimeSpan.Zero);
        var tracker = new ActivityTracker(repo, fg, input, intervalSeconds: 2, idleThresholdSeconds: 60);

        await tracker.TickAsync(now);

        var samples = await repo.GetSamplesAsync(now.AddMinutes(-1), now.AddMinutes(1));
        Assert.Single(samples);
        Assert.Equal("code", samples[0].ProcessName);
        Assert.Equal(2, samples[0].DurationSeconds);
    }

    [Fact]
    public async Task Tick_WhenIdleBeyondThreshold_RecordsNoSample()
    {
        var repo = await NewRepoAsync();
        var fg = new FakeForeground { Current = new ForegroundInfo("code", "x"), Idle = 120 };
        var tracker = new ActivityTracker(repo, fg, new FakeInput(), 2, idleThresholdSeconds: 60);

        await tracker.TickAsync(new DateTimeOffset(2026, 6, 9, 10, 0, 0, TimeSpan.Zero));

        var samples = await repo.GetSamplesAsync(
            DateTimeOffset.MinValue.AddDays(1), DateTimeOffset.MaxValue.AddDays(-1));
        Assert.Empty(samples);
    }

    [Fact]
    public async Task Tick_DrainsInputCountersIntoRepository()
    {
        var repo = await NewRepoAsync();
        var fg = new FakeForeground { Current = new ForegroundInfo("code", "x"), Idle = 0 };
        var input = new FakeInput { Pending = new InputCounters(5, 2, 40) };
        var now = new DateTimeOffset(2026, 6, 9, 10, 0, 0, TimeSpan.Zero);
        var tracker = new ActivityTracker(repo, fg, input, 2, 60);

        await tracker.TickAsync(now);

        var counters = await repo.GetInputCountersAsync(
            new DateOnly(2026, 6, 9), new DateOnly(2026, 6, 9));
        Assert.Equal(5, counters.Keystrokes);
        Assert.Equal(2, counters.Clicks);
        Assert.Equal(40, counters.MousePixels);
    }
}
```

- [ ] **Step 3: Run tests to verify they fail**

Run: `dotnet test --filter ActivityTrackerTests`
Expected: FAIL (ActivityTracker does not exist).

- [ ] **Step 4: Implement the tracker**

`src/PcWrapped.Core/Tracking/ActivityTracker.cs`:
```csharp
using PcWrapped.Core.Storage;

namespace PcWrapped.Core.Tracking;

public sealed class ActivityTracker
{
    private readonly IStatsRepository _repo;
    private readonly IForegroundWindowSource _foreground;
    private readonly IInputCounterSource _input;
    private readonly int _intervalSeconds;
    private readonly int _idleThresholdSeconds;

    public ActivityTracker(
        IStatsRepository repo,
        IForegroundWindowSource foreground,
        IInputCounterSource input,
        int intervalSeconds,
        int idleThresholdSeconds)
    {
        _repo = repo;
        _foreground = foreground;
        _input = input;
        _intervalSeconds = intervalSeconds;
        _idleThresholdSeconds = idleThresholdSeconds;
    }

    /// <summary>Один тик опроса. Вызывается по таймеру в приложении.</summary>
    public async Task TickAsync(DateTimeOffset now)
    {
        // Счётчики ввода забираем всегда (даже если окно неизвестно).
        var counters = _input.DrainCounters();
        if (counters.Keystrokes != 0 || counters.Clicks != 0 || counters.MousePixels != 0)
            await _repo.AddInputCountersAsync(DateOnly.FromDateTime(now.DateTime), counters);

        if (_foreground.GetIdleSeconds() >= _idleThresholdSeconds)
            return; // пользователь отошёл — время не накручиваем

        var fg = _foreground.GetForeground();
        if (fg is null) return;

        await _repo.AddSampleAsync(new Models.UsageSample(
            now, fg.Value.ProcessName, fg.Value.WindowTitle, _intervalSeconds));
    }
}
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test --filter ActivityTrackerTests`
Expected: PASS (3 tests).

- [ ] **Step 6: Run the full Core suite**

Run: `dotnet test`
Expected: PASS (all tests so far).

- [ ] **Step 7: Commit**

```bash
git add -A
git commit -m "feat: add ActivityTracker with idle handling and input draining"
```

---

## Task 9: BuildPeriodStats orchestration

**Files:**
- Modify: `src/PcWrapped.Core/Aggregation/Aggregator.cs`
- Modify: `tests/PcWrapped.Core.Tests/AggregatorTests.cs`

This is the single entry point the UI calls to turn stored data into a `PeriodStats`.

- [ ] **Step 1: Add the failing test**

Append to `AggregatorTests.cs`:
```csharp
    [Fact]
    public void BuildPeriodStats_ComposesAllMetrics()
    {
        var cat = new Categorizer(DefaultRules.Map);
        var samples = new[]
        {
            S("code", "2026-06-09", 10, 300),
            S("chrome", "2026-06-09", 22, 100),
        };
        var stats = Aggregator.BuildPeriodStats(
            from: new DateOnly(2026, 6, 9),
            to: new DateOnly(2026, 6, 9),
            samples: samples,
            categorizer: cat,
            counters: new InputCounters(1000, 200, 96 * 1000),
            activeDays: new[] { new DateOnly(2026, 6, 9) },
            today: new DateOnly(2026, 6, 9),
            topAppLimit: 5,
            mouseDpi: 96);

        Assert.Equal(TimeSpan.FromSeconds(400), stats.TotalActive);
        Assert.Equal("code", stats.TopApps[0].ProcessName);
        Assert.Equal(10, stats.PeakHour);
        Assert.Equal(1, stats.StreakDays);
        Assert.Equal(1000, stats.Keystrokes);
        Assert.Equal(200, stats.Clicks);
        // 96*1000 px @ 96 dpi = 1000 inch = 25.4 m = 0.0254 km
        Assert.Equal(0.0254, stats.MouseKilometers, 6);
        Assert.Equal(TimeSpan.FromSeconds(300), stats.ByCategory[Category.Work]);
    }
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test --filter AggregatorTests`
Expected: FAIL (BuildPeriodStats does not exist).

- [ ] **Step 3: Implement the orchestrator**

Add to `Aggregator`:
```csharp
    public static PeriodStats BuildPeriodStats(
        DateOnly from,
        DateOnly to,
        IEnumerable<UsageSample> samples,
        Categorizer categorizer,
        InputCounters counters,
        IEnumerable<DateOnly> activeDays,
        DateOnly today,
        int topAppLimit,
        double mouseDpi)
    {
        var list = samples as IReadOnlyList<UsageSample> ?? samples.ToList();
        return new PeriodStats(
            From: from,
            To: to,
            TotalActive: TotalActive(list),
            TopApps: TopApps(list, topAppLimit),
            ByCategory: ByCategory(list, categorizer),
            PeakHour: PeakHour(list),
            HourlySeconds: HourlySeconds(list),
            StreakDays: Streak(activeDays, today),
            Keystrokes: counters.Keystrokes,
            Clicks: counters.Clicks,
            MouseKilometers: VanityMath.PixelsToKilometers(counters.MousePixels, mouseDpi));
    }
```

Add `using PcWrapped.Core.Models;` if not already present (it is, from Task 5).

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test --filter AggregatorTests`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "feat: add BuildPeriodStats orchestrator"
```

---

## Task 10: Avalonia app scaffolding + DI wiring

**Files:**
- Create: `src/PcWrapped/PcWrapped.csproj`
- Create: `src/PcWrapped/Program.cs`
- Create: `src/PcWrapped/App.axaml` + `App.axaml.cs`
- Create: `src/PcWrapped/Views/MainWindow.axaml` + `.cs`
- Modify: `PcWrapped.sln`

- [ ] **Step 1: Create the Avalonia project**

```bash
cd /c/Users/090/pc-wrapped
dotnet new install Avalonia.Templates
dotnet new avalonia.app -n PcWrapped -o src/PcWrapped -f net8.0
dotnet sln add src/PcWrapped
dotnet add src/PcWrapped reference src/PcWrapped.Core
```

- [ ] **Step 2: Build the empty app to verify the template runs**

Run: `dotnet build src/PcWrapped`
Expected: Build succeeded.

- [ ] **Step 3: Launch once to confirm a window appears**

Run: `dotnet run --project src/PcWrapped`
Expected: An empty Avalonia window opens. Close it.

- [ ] **Step 4: Commit**

```bash
git add -A
git commit -m "chore: scaffold Avalonia app project"
```

---

## Task 11: Native foreground window source (P/Invoke)

**Files:**
- Create: `src/PcWrapped/Native/Win32ForegroundWindowSource.cs`

This is platform code that cannot be unit-tested; verify manually.

- [ ] **Step 1: Implement the native source**

`src/PcWrapped/Native/Win32ForegroundWindowSource.cs`:
```csharp
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using PcWrapped.Core.Tracking;

namespace PcWrapped.Native;

public sealed class Win32ForegroundWindowSource : IForegroundWindowSource
{
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);
    [DllImport("user32.dll")] private static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);

    [StructLayout(LayoutKind.Sequential)]
    private struct LASTINPUTINFO { public uint cbSize; public uint dwTime; }

    public ForegroundInfo? GetForeground()
    {
        var hwnd = GetForegroundWindow();
        if (hwnd == IntPtr.Zero) return null;
        GetWindowThreadProcessId(hwnd, out uint pid);
        if (pid == 0) return null;

        string process;
        try { process = Process.GetProcessById((int)pid).ProcessName; }
        catch { return null; }

        var sb = new StringBuilder(512);
        GetWindowText(hwnd, sb, sb.Capacity);
        return new ForegroundInfo(process, sb.ToString());
    }

    public int GetIdleSeconds()
    {
        var info = new LASTINPUTINFO { cbSize = (uint)Marshal.SizeOf<LASTINPUTINFO>() };
        if (!GetLastInputInfo(ref info)) return 0;
        uint idleMs = (uint)Environment.TickCount - info.dwTime;
        return (int)(idleMs / 1000);
    }
}
```

- [ ] **Step 2: Build to verify it compiles**

Run: `dotnet build src/PcWrapped`
Expected: Build succeeded.

- [ ] **Step 3: Manual verification**

Add a temporary line in `Program.cs` `Main` before `BuildAvaloniaApp`:
```csharp
var src = new PcWrapped.Native.Win32ForegroundWindowSource();
Console.WriteLine($"FG: {src.GetForeground()?.ProcessName} idle={src.GetIdleSeconds()}s");
```
Run: `dotnet run --project src/PcWrapped`
Expected: prints the current foreground process name (e.g., the terminal/IDE) and an idle value. Then remove the temporary lines.

- [ ] **Step 4: Commit**

```bash
git add -A
git commit -m "feat: add Win32 foreground window source"
```

---

## Task 12: Native input counter source (low-level hooks)

**Files:**
- Create: `src/PcWrapped/Native/Win32InputCounterSource.cs`

Counts only — never captures key contents. Cannot be unit-tested; verify manually.

- [ ] **Step 1: Implement the hook-based counter**

`src/PcWrapped/Native/Win32InputCounterSource.cs`:
```csharp
using System.Runtime.InteropServices;
using PcWrapped.Core.Models;
using PcWrapped.Core.Tracking;

namespace PcWrapped.Native;

/// <summary>
/// Низкоуровневые хуки клавиатуры/мыши. Считает ТОЛЬКО количество событий и
/// пройденное мышью расстояние. Содержимое нажатий нигде не сохраняется.
/// </summary>
public sealed class Win32InputCounterSource : IInputCounterSource, IDisposable
{
    private const int WH_KEYBOARD_LL = 13;
    private const int WH_MOUSE_LL = 14;
    private const int WM_KEYDOWN = 0x0100;
    private const int WM_SYSKEYDOWN = 0x0104;
    private const int WM_LBUTTONDOWN = 0x0201;
    private const int WM_RBUTTONDOWN = 0x0204;
    private const int WM_MOUSEMOVE = 0x0200;

    private delegate IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, HookProc lpfn, IntPtr hMod, uint dwThreadId);
    [DllImport("user32.dll")] private static extern bool UnhookWindowsHookEx(IntPtr hhk);
    [DllImport("user32.dll")] private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);
    [DllImport("kernel32.dll", CharSet = CharSet.Auto)] private static extern IntPtr GetModuleHandle(string? name);

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int x; public int y; }
    [StructLayout(LayoutKind.Sequential)]
    private struct MSLLHOOKSTRUCT { public POINT pt; public uint mouseData; public uint flags; public uint time; public IntPtr dwExtraInfo; }

    // Keep delegates alive for the lifetime of the object.
    private readonly HookProc _kbProc;
    private readonly HookProc _mouseProc;
    private IntPtr _kbHook;
    private IntPtr _mouseHook;

    private long _keystrokes;
    private long _clicks;
    private double _pixels;
    private bool _hasLast;
    private int _lastX, _lastY;
    private readonly object _lock = new();

    public bool IsActive => _kbHook != IntPtr.Zero || _mouseHook != IntPtr.Zero;

    public Win32InputCounterSource()
    {
        _kbProc = KeyboardHook;
        _mouseProc = MouseHook;
    }

    public void Start()
    {
        IntPtr mod = GetModuleHandle(null);
        _kbHook = SetWindowsHookEx(WH_KEYBOARD_LL, _kbProc, mod, 0);
        _mouseHook = SetWindowsHookEx(WH_MOUSE_LL, _mouseProc, mod, 0);
        // Если хук не поставился (антивирус и т.п.) — счётчики просто останутся нулевыми.
    }

    private IntPtr KeyboardHook(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            int msg = (int)wParam;
            if (msg == WM_KEYDOWN || msg == WM_SYSKEYDOWN)
                Interlocked.Increment(ref _keystrokes);
        }
        return CallNextHookEx(_kbHook, nCode, wParam, lParam);
    }

    private IntPtr MouseHook(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            int msg = (int)wParam;
            if (msg == WM_LBUTTONDOWN || msg == WM_RBUTTONDOWN)
                Interlocked.Increment(ref _clicks);
            else if (msg == WM_MOUSEMOVE)
            {
                var data = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
                lock (_lock)
                {
                    if (_hasLast)
                    {
                        double dx = data.pt.x - _lastX;
                        double dy = data.pt.y - _lastY;
                        _pixels += Math.Sqrt(dx * dx + dy * dy);
                    }
                    _lastX = data.pt.x; _lastY = data.pt.y; _hasLast = true;
                }
            }
        }
        return CallNextHookEx(_mouseHook, nCode, wParam, lParam);
    }

    public InputCounters DrainCounters()
    {
        long k = Interlocked.Exchange(ref _keystrokes, 0);
        long c = Interlocked.Exchange(ref _clicks, 0);
        double p;
        lock (_lock) { p = _pixels; _pixels = 0; }
        return new InputCounters(k, c, p);
    }

    public void Dispose()
    {
        if (_kbHook != IntPtr.Zero) UnhookWindowsHookEx(_kbHook);
        if (_mouseHook != IntPtr.Zero) UnhookWindowsHookEx(_mouseHook);
        _kbHook = _mouseHook = IntPtr.Zero;
    }
}
```

- [ ] **Step 2: Build to verify it compiles**

Run: `dotnet build src/PcWrapped`
Expected: Build succeeded.

> Note: Low-level hooks require a running message loop, which the Avalonia UI thread provides at runtime. Full verification happens in Task 16 when the tracker is wired into the running app.

- [ ] **Step 3: Commit**

```bash
git add -A
git commit -m "feat: add Win32 input counter source (counts only)"
```

---

## Task 13: Card themes + renderer

**Files:**
- Create: `src/PcWrapped/Rendering/CardTheme.cs`
- Create: `src/PcWrapped/Rendering/CardThemes.cs`
- Create: `src/PcWrapped/Rendering/CardRenderer.cs`

- [ ] **Step 1: Define theme model and the three built-in themes**

`src/PcWrapped/Rendering/CardTheme.cs`:
```csharp
using Avalonia.Media;

namespace PcWrapped.Rendering;

public sealed record CardTheme(
    string Id,
    string DisplayName,
    IBrush Background,
    Color TextColor,
    Color AccentColor,
    string FontFamily);
```

`src/PcWrapped/Rendering/CardThemes.cs`:
```csharp
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
```

- [ ] **Step 2: Implement the renderer (metrics + theme + size → PNG)**

`src/PcWrapped/Rendering/CardRenderer.cs`:
```csharp
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
    public static Control BuildCard(PeriodStats stats, CardTheme theme, PixelSize size)
    {
        var stack = new StackPanel
        {
            Margin = new Thickness(80),
            Spacing = 18,
            VerticalAlignment = VerticalAlignment.Center,
        };

        void Row(string label, string value)
        {
            stack.Children.Add(new DockPanel
            {
                Children =
                {
                    new TextBlock { Text = label, Foreground = new SolidColorBrush(theme.TextColor),
                                    FontFamily = theme.FontFamily, FontSize = 34, Opacity = 0.85 },
                    new TextBlock { Text = value, Foreground = new SolidColorBrush(theme.AccentColor),
                                    FontFamily = theme.FontFamily, FontSize = 34, FontWeight = FontWeight.Bold,
                                    HorizontalAlignment = HorizontalAlignment.Right,
                                    [DockPanel.DockProperty] = Dock.Right },
                }
            });
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
            Row("🏆 " + stats.TopApps[0].ProcessName, FormatHours(stats.TopApps[0].Duration));
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

    /// <summary>Рендерит карточку в PNG-файл.</summary>
    public static void RenderToPng(PeriodStats stats, CardTheme theme, PixelSize size, string path)
    {
        var card = BuildCard(stats, theme, size);
        card.Measure(new Size(size.Width, size.Height));
        card.Arrange(new Rect(0, 0, size.Width, size.Height));

        using var bmp = new RenderTargetBitmap(size, new Vector(96, 96));
        bmp.Render(card);
        bmp.Save(path);
    }

    private static string FormatHours(TimeSpan t) =>
        t.TotalHours >= 1 ? $"{(int)t.TotalHours}ч {t.Minutes:00}м" : $"{t.Minutes}м";
}
```

- [ ] **Step 3: Build to verify it compiles**

Run: `dotnet build src/PcWrapped`
Expected: Build succeeded.

- [ ] **Step 4: Commit**

```bash
git add -A
git commit -m "feat: add card themes and PNG renderer"
```

---

## Task 14: Card renderer smoke test (headless)

**Files:**
- Create: `tests/PcWrapped.App.Tests/PcWrapped.App.Tests.csproj`
- Create: `tests/PcWrapped.App.Tests/CardRendererTests.cs`
- Modify: `PcWrapped.sln`

Renders all themes in both sizes via Avalonia headless and asserts a non-empty PNG of the right dimensions is produced.

- [ ] **Step 1: Create the headless test project**

```bash
cd /c/Users/090/pc-wrapped
dotnet new xunit -n PcWrapped.App.Tests -o tests/PcWrapped.App.Tests -f net8.0
dotnet sln add tests/PcWrapped.App.Tests
dotnet add tests/PcWrapped.App.Tests reference src/PcWrapped
dotnet add tests/PcWrapped.App.Tests reference src/PcWrapped.Core
dotnet add tests/PcWrapped.App.Tests package Avalonia.Headless --version 11.0.10
```

- [ ] **Step 2: Write the failing test**

`tests/PcWrapped.App.Tests/CardRendererTests.cs`:
```csharp
using Avalonia;
using Avalonia.Headless;
using PcWrapped.Core.Models;
using PcWrapped.Rendering;
using Xunit;

[assembly: AvaloniaTestApplication(typeof(PcWrapped.App.Tests.TestAppBuilder))]

namespace PcWrapped.App.Tests;

public static class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<Application>().UseHeadless(new AvaloniaHeadlessPlatformOptions());
}

public class CardRendererTests
{
    private static PeriodStats Sample() => new(
        new DateOnly(2026, 6, 9), new DateOnly(2026, 6, 15),
        TimeSpan.FromHours(37), new[] { new AppUsage("code", TimeSpan.FromHours(14)) },
        new Dictionary<Category, TimeSpan> { [Category.Work] = TimeSpan.FromHours(14) },
        22, new int[24], 5, 91204, 4200, 3.4);

    [AvaloniaFact]
    public void RenderToPng_AllThemes_BothSizes_ProducesValidPng()
    {
        var dir = Path.Combine(Path.GetTempPath(), "pcwrapped-test");
        Directory.CreateDirectory(dir);

        foreach (var theme in CardThemes.All)
        foreach (var size in new[] { CardRenderer.Square, CardRenderer.Story })
        {
            var path = Path.Combine(dir, $"{theme.Id}-{size.Width}x{size.Height}.png");
            CardRenderer.RenderToPng(Sample(), theme, size, path);

            Assert.True(File.Exists(path));
            Assert.True(new FileInfo(path).Length > 1000, "PNG should be non-trivial");
            using var fs = File.OpenRead(path);
            var sig = new byte[8];
            fs.Read(sig, 0, 8);
            Assert.Equal(0x89, sig[0]); // PNG magic
            Assert.Equal((byte)'P', sig[1]);
        }
    }
}
```

- [ ] **Step 3: Run test to verify it fails, then passes**

Run: `dotnet test --filter CardRendererTests`
Expected: First run may FAIL if headless package/markers are missing; fix references until it PASSES (6 renders).

- [ ] **Step 4: Commit**

```bash
git add -A
git commit -m "test: headless smoke test for card renderer across themes/sizes"
```

---

## Task 15: Autostart manager

**Files:**
- Create: `src/PcWrapped/Native/AutostartManager.cs`

- [ ] **Step 1: Implement registry-based autostart**

`src/PcWrapped/Native/AutostartManager.cs`:
```csharp
using Microsoft.Win32;

namespace PcWrapped.Native;

public static class AutostartManager
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "PcWrapped";

    public static bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey);
        return key?.GetValue(ValueName) is not null;
    }

    public static void SetEnabled(bool enabled, string exePath)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true)
                        ?? Registry.CurrentUser.CreateSubKey(RunKey);
        if (enabled) key.SetValue(ValueName, $"\"{exePath}\"");
        else key.DeleteValue(ValueName, throwOnMissingValue: false);
    }
}
```

- [ ] **Step 2: Build to verify it compiles**

Run: `dotnet build src/PcWrapped`
Expected: Build succeeded.

- [ ] **Step 3: Commit**

```bash
git add -A
git commit -m "feat: add autostart manager"
```

---

## Task 16: Wire it together — tray, background timer, MainWindow, onboarding

**Files:**
- Modify: `src/PcWrapped/App.axaml` + `App.axaml.cs`
- Create: `src/PcWrapped/ViewModels/MainViewModel.cs`
- Modify: `src/PcWrapped/Views/MainWindow.axaml` + `.cs`
- Create: `src/PcWrapped/Views/OnboardingWindow.axaml` + `.cs`

This task assembles the running product. Verify by running the app.

- [ ] **Step 1: Build the MainViewModel**

`src/PcWrapped/ViewModels/MainViewModel.cs`:
```csharp
using System;
using System.Threading.Tasks;
using PcWrapped.Core.Aggregation;
using PcWrapped.Core.Categorization;
using PcWrapped.Core.Models;
using PcWrapped.Core.Storage;
using PcWrapped.Rendering;

namespace PcWrapped.ViewModels;

public sealed class MainViewModel
{
    private readonly IStatsRepository _repo;
    private readonly Categorizer _categorizer = new(DefaultRules.Map);

    public CardTheme SelectedTheme { get; set; } = CardThemes.Gradient;

    public MainViewModel(IStatsRepository repo) => _repo = repo;

    public async Task<PeriodStats> BuildWeekStatsAsync(DateOnly today, double mouseDpi)
    {
        var from = today.AddDays(-6);
        var fromDt = new DateTimeOffset(from.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var toDt = new DateTimeOffset(today.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);

        var samples = await _repo.GetSamplesAsync(fromDt, toDt);
        var counters = await _repo.GetInputCountersAsync(from, today);
        var activeDays = await _repo.GetActiveDaysAsync();

        return Aggregator.BuildPeriodStats(from, today, samples, _categorizer,
            counters, activeDays, today, topAppLimit: 5, mouseDpi: mouseDpi);
    }

    public Task ExportAsync(PeriodStats stats, string path) =>
        Task.Run(() => CardRenderer.RenderToPng(stats, SelectedTheme, CardRenderer.Square, path));
}
```

- [ ] **Step 2: Set up app lifetime, DB path, tray, and the polling timer**

Replace `src/PcWrapped/App.axaml.cs` with:
```csharp
using System;
using System.IO;
using System.Timers;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using PcWrapped.Core.Storage;
using PcWrapped.Core.Tracking;
using PcWrapped.Native;
using PcWrapped.ViewModels;
using PcWrapped.Views;

namespace PcWrapped;

public partial class App : Application
{
    private SqliteStatsRepository? _repo;
    private Win32InputCounterSource? _input;
    private ActivityTracker? _tracker;
    private Timer? _timer;
    private const int IntervalSeconds = 2;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override async void OnFrameworkInitializationCompleted()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PcWrapped");
        Directory.CreateDirectory(dir);
        _repo = new SqliteStatsRepository($"Data Source={Path.Combine(dir, "stats.db")}");
        await _repo.InitializeAsync();

        _input = new Win32InputCounterSource();
        _input.Start();
        _tracker = new ActivityTracker(_repo, new Win32ForegroundWindowSource(), _input,
            IntervalSeconds, idleThresholdSeconds: 60);

        _timer = new Timer(IntervalSeconds * 1000);
        _timer.Elapsed += async (_, _) =>
        {
            try { await _tracker.TickAsync(DateTimeOffset.Now); } catch { /* keep running */ }
        };
        _timer.Start();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            var vm = new MainViewModel(_repo);
            desktop.MainWindow = new MainWindow { DataContext = vm };
            desktop.MainWindow.Show();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
```

- [ ] **Step 3: Add the tray icon in App.axaml**

Add inside `<Application>...</Application>` in `src/PcWrapped/App.axaml`:
```xml
<TrayIcon.Icons>
  <TrayIcons>
    <TrayIcon Icon="/Assets/avalonia-logo.ico" ToolTipText="PC Wrapped">
      <TrayIcon.Menu>
        <NativeMenu>
          <NativeMenuItem Header="Открыть" Click="OnOpenClicked"/>
          <NativeMenuItem Header="Выход" Click="OnExitClicked"/>
        </NativeMenu>
      </TrayIcon.Menu>
    </TrayIcon>
  </TrayIcons>
</TrayIcon.Icons>
```
And add the handlers to `App.axaml.cs`:
```csharp
    private void OnOpenClicked(object? sender, EventArgs e)
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime d)
        { d.MainWindow?.Show(); d.MainWindow?.Activate(); }
    }

    private void OnExitClicked(object? sender, EventArgs e)
    {
        _timer?.Stop(); _input?.Dispose(); _repo?.Dispose();
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime d) d.Shutdown();
    }
```

- [ ] **Step 4: Build the MainWindow UI (dashboard + theme picker + export)**

`src/PcWrapped/Views/MainWindow.axaml`:
```xml
<Window xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        x:Class="PcWrapped.Views.MainWindow"
        Width="520" Height="640" Title="PC Wrapped">
  <DockPanel Margin="24">
    <TextBlock DockPanel.Dock="Top" Text="PC Wrapped" FontSize="28" FontWeight="Bold"/>
    <TextBlock DockPanel.Dock="Top" x:Name="Status" Margin="0,8,0,16"
               Text="Собираю статистику…" Opacity="0.7"/>
    <StackPanel DockPanel.Dock="Bottom" Orientation="Horizontal" Spacing="12" Margin="0,16,0,0">
      <ComboBox x:Name="ThemeBox" Width="200"/>
      <Button x:Name="RefreshBtn" Content="Обновить"/>
      <Button x:Name="ExportBtn" Content="Сохранить карточку (PNG)"/>
    </StackPanel>
    <ScrollViewer>
      <StackPanel x:Name="StatsPanel" Spacing="6"/>
    </ScrollViewer>
  </DockPanel>
</Window>
```

`src/PcWrapped/Views/MainWindow.axaml.cs`:
```csharp
using System;
using System.IO;
using Avalonia.Controls;
using Avalonia.Interactivity;
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
        var box = this.FindControl<ComboBox>("ThemeBox")!;
        box.ItemsSource = CardThemes.All;
        box.SelectedIndex = 0;
        box.DisplayMemberBinding = new Avalonia.Data.Binding("DisplayName");

        this.FindControl<Button>("RefreshBtn")!.Click += async (_, _) => await RefreshAsync();
        this.FindControl<Button>("ExportBtn")!.Click += OnExport;
        Opened += async (_, _) => await RefreshAsync();
    }

    private MainViewModel Vm => (MainViewModel)DataContext!;

    private async System.Threading.Tasks.Task RefreshAsync()
    {
        var box = this.FindControl<ComboBox>("ThemeBox")!;
        if (box.SelectedItem is CardTheme t) Vm.SelectedTheme = t;

        _current = await Vm.BuildWeekStatsAsync(DateOnly.FromDateTime(DateTime.Now), mouseDpi: 96);
        var panel = this.FindControl<StackPanel>("StatsPanel")!;
        panel.Children.Clear();
        void Add(string s) => panel.Children.Add(new TextBlock { Text = s, FontSize = 18 });
        Add($"Всего активного времени: {(int)_current.TotalActive.TotalHours}ч {_current.TotalActive.Minutes}м");
        if (_current.TopApps.Count > 0) Add($"Топ-приложение: {_current.TopApps[0].ProcessName}");
        Add($"Мышь проехала: {_current.MouseKilometers:0.0} км");
        Add($"Нажатий клавиш: {_current.Keystrokes:N0}");
        Add($"Кликов: {_current.Clicks:N0}");
        if (_current.PeakHour >= 0) Add($"Пик активности: {_current.PeakHour:00}:00");
        Add($"Серия дней подряд: {_current.StreakDays}");
        this.FindControl<TextBlock>("Status")!.Text = "Готово";
    }

    private async void OnExport(object? sender, RoutedEventArgs e)
    {
        if (_current is null) return;
        var box = this.FindControl<ComboBox>("ThemeBox")!;
        if (box.SelectedItem is CardTheme t) Vm.SelectedTheme = t;

        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            SuggestedFileName = "pc-wrapped.png",
            DefaultExtension = "png",
        });
        if (file is null) return;
        await Vm.ExportAsync(_current, file.Path.LocalPath);
    }
}
```

- [ ] **Step 5: Create a minimal onboarding/privacy window**

`src/PcWrapped/Views/OnboardingWindow.axaml`:
```xml
<Window xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        x:Class="PcWrapped.Views.OnboardingWindow"
        Width="460" Height="360" Title="Добро пожаловать в PC Wrapped">
  <StackPanel Margin="28" Spacing="14">
    <TextBlock Text="Приватность прежде всего" FontSize="22" FontWeight="Bold"/>
    <TextBlock TextWrapping="Wrap"
      Text="PC Wrapped считает время в приложениях и количество нажатий/кликов/движений мыши, чтобы строить красивые карточки. Все данные хранятся только на этом компьютере и никогда не отправляются в сеть. Содержимое набранного текста не сохраняется — только счётчики."/>
    <CheckBox x:Name="VanityToggle" IsChecked="True"
      Content="Считать нажатия клавиш и движение мыши"/>
    <CheckBox x:Name="AutostartToggle" IsChecked="True"
      Content="Запускать при старте Windows"/>
    <Button x:Name="StartBtn" Content="Начать" HorizontalAlignment="Right"/>
  </StackPanel>
</Window>
```

`src/PcWrapped/Views/OnboardingWindow.axaml.cs`:
```csharp
using Avalonia.Controls;

namespace PcWrapped.Views;

public partial class OnboardingWindow : Window
{
    public OnboardingWindow()
    {
        InitializeComponent();
        this.FindControl<Button>("StartBtn")!.Click += (_, _) => Close();
    }
}
```

- [ ] **Step 6: Build and run the full app**

Run: `dotnet build` then `dotnet run --project src/PcWrapped`
Expected: App window opens, tray icon present. Type and move the mouse for ~30 seconds, click "Обновить" — non-zero keystrokes/clicks/mouse km appear. Click "Сохранить карточку" — a PNG is saved and looks like the chosen theme.

- [ ] **Step 7: Commit**

```bash
git add -A
git commit -m "feat: wire tracker, tray, dashboard, export, and onboarding"
```

---

## Task 17: Raw-data rollup (DB growth control)

**Files:**
- Modify: `src/PcWrapped/App.axaml.cs` (call rollup on startup)
- Modify: `src/PcWrapped.Core/Storage/IStatsRepository.cs`
- Modify: `src/PcWrapped.Core/Storage/SqliteStatsRepository.cs`
- Modify: `tests/PcWrapped.Core.Tests/SqliteStatsRepositoryTests.cs`

Samples older than N days are collapsed into one row per (process, hour) to cap DB size while preserving hourly aggregation.

- [ ] **Step 1: Add the failing test**

Append to `SqliteStatsRepositoryTests.cs`:
```csharp
    [Fact]
    public async Task RollupOlderThan_CollapsesSamplesPerProcessPerHour()
    {
        var repo = await NewRepoAsync();
        var old = new DateTimeOffset(2026, 1, 1, 10, 5, 0, TimeSpan.Zero);
        await repo.AddSampleAsync(new UsageSample(old, "code", "a", 60));
        await repo.AddSampleAsync(new UsageSample(old.AddMinutes(10), "code", "b", 120));
        await repo.AddSampleAsync(new UsageSample(old.AddMinutes(20), "chrome", "c", 30));

        await repo.RollupOlderThanAsync(new DateOnly(2026, 1, 2));

        var all = await repo.GetSamplesAsync(
            new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 1, 2, 0, 0, 0, TimeSpan.Zero));
        Assert.Equal(2, all.Count); // code (180s) + chrome (30s), collapsed to the hour
        var code = all.First(s => s.ProcessName == "code");
        Assert.Equal(180, code.DurationSeconds);
        Assert.Equal(10, code.Start.Hour); // bucketed to start of hour
        Assert.Equal(0, code.Start.Minute);
    }
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test --filter SqliteStatsRepositoryTests`
Expected: FAIL (RollupOlderThanAsync does not exist).

- [ ] **Step 3: Add to the interface and implement**

Add to `IStatsRepository`:
```csharp
    Task RollupOlderThanAsync(DateOnly cutoffDay);
```

Add to `SqliteStatsRepository`:
```csharp
    public async Task RollupOlderThanAsync(DateOnly cutoffDay)
    {
        long cutoff = new DateTimeOffset(
            cutoffDay.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero).ToUnixTimeSeconds();

        await using var tx = (Microsoft.Data.Sqlite.SqliteTransaction)
            await _conn.BeginTransactionAsync();

        // Bucket each old sample to the start of its hour, sum seconds per (process, hour).
        await using (var agg = _conn.CreateCommand())
        {
            agg.Transaction = tx;
            agg.CommandText = @"
CREATE TEMP TABLE _rollup AS
SELECT (start_unix / 3600) * 3600 AS hour_unix, process,
       MIN(title) AS title, SUM(seconds) AS seconds
FROM samples WHERE start_unix < $cutoff
GROUP BY hour_unix, process;";
            agg.Parameters.AddWithValue("$cutoff", cutoff);
            await agg.ExecuteNonQueryAsync();
        }
        await using (var del = _conn.CreateCommand())
        {
            del.Transaction = tx;
            del.CommandText = "DELETE FROM samples WHERE start_unix < $cutoff;";
            del.Parameters.AddWithValue("$cutoff", cutoff);
            await del.ExecuteNonQueryAsync();
        }
        await using (var ins = _conn.CreateCommand())
        {
            ins.Transaction = tx;
            ins.CommandText = @"
INSERT INTO samples (start_unix, process, title, seconds)
SELECT hour_unix, process, title, seconds FROM _rollup;
DROP TABLE _rollup;";
            await ins.ExecuteNonQueryAsync();
        }
        await tx.CommitAsync();
    }
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test --filter SqliteStatsRepositoryTests`
Expected: PASS.

- [ ] **Step 5: Call rollup on startup**

In `App.axaml.cs`, after `await _repo.InitializeAsync();` add:
```csharp
        await _repo.RollupOlderThanAsync(DateOnly.FromDateTime(DateTime.Now).AddDays(-30));
```

- [ ] **Step 6: Run the full test suite**

Run: `dotnet test`
Expected: PASS (all tests).

- [ ] **Step 7: Commit**

```bash
git add -A
git commit -m "feat: roll up raw samples older than 30 days"
```

---

## Task 18: README with privacy/open-source pitch

**Files:**
- Create: `README.md`

- [ ] **Step 1: Write the README**

`README.md`:
```markdown
# PC Wrapped

Spotify-Wrapped для твоего компьютера. Локально считает, как ты проводишь время
за ПК, и генерирует красивые карточки, которыми можно поделиться.

## Приватность
- 100% локально. Данные хранятся в `%APPDATA%\PcWrapped\stats.db` и никогда не
  отправляются в сеть.
- Считаются только счётчики ввода (число нажатий/кликов/движение мыши).
  Содержимое набранного текста не сохраняется. Код открыт — проверь сам.

## Возможности
- Время по приложениям и категориям (работа/игры/соцсети/браузер)
- Самый продуктивный час, серии активных дней (streak)
- «Vanity»-метрики: км пробега мыши, число нажатий и кликов
- 3 темы карточек, экспорт в PNG (1:1 и 9:16)

## Сборка
```
dotnet build
dotnet run --project src/PcWrapped
```

## Тесты
```
dotnet test
```
```

- [ ] **Step 2: Commit**

```bash
git add -A
git commit -m "docs: add README with privacy and feature overview"
```

---

## Final verification

- [ ] Run `dotnet test` — all tests pass.
- [ ] Run `dotnet run --project src/PcWrapped` — app tracks input, dashboard shows non-zero metrics after activity, all three themes export valid PNGs in both sizes.
- [ ] Confirm `%APPDATA%\PcWrapped\stats.db` is created and grows as you use the PC.

---

## Spec Coverage Notes

- Метрики (время/категории/ритм/vanity) → Tasks 5, 6, 9.
- 3 выбираемые темы + экспорт 1:1 и 9:16 → Tasks 13, 14, 16.
- Приватность/локальность → Task 16 (local DB), Task 18 (messaging), onboarding (Task 16 step 5).
- Хуки + мягкая деградация → Task 12 (hooks never throw; zero counters if unavailable).
- Tracker + idle → Task 8.
- Хранение + рост БД → Tasks 7, 17.
- Трей + автозапуск + онбординг → Tasks 15, 16.
- Тестирование (Aggregator/Categorizer/VanityMath/Storage/Renderer) → Tasks 3–9, 14.
- Out of scope (cloud, Mac/Linux, social API, per-URL, AI) → не включено.
```
