# PC Wrapped — Categories (expanded rules + manual assignment) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Сделать категории осмысленными: большой встроенный словарь + ручное назначение категории приложению (правый клик по плитке) с сохранением; переопределения побеждают и сразу меняют донат.

**Architecture:** Переопределения хранятся в новой SQLite-таблице `category_overrides`. Чистый хелпер `CategoryRules.Merge` сливает встроенные правила с переопределениями (ручные побеждают). `MainViewModel` строит `Categorizer` из слияния и пересчитывает статистику; правый клик по плитке вызывает назначение и refresh.

**Tech Stack:** .NET 8, Avalonia 11.0.10, C#, Microsoft.Data.Sqlite, xUnit.

**Commit author:** ВСЕ коммиты — обычный `git commit -m "..."` (git config user = `090_TYPE`). НИКАКИХ `Co-Authored-By` трейлеров, без `--author`, без упоминания Claude/AI. Solution: `PcWrapped.slnx`. App project БЕЗ ImplicitUsings (явные `using`); Core project — ImplicitUsings ON.

---

## File Structure
```
src/PcWrapped.Core/Storage/IStatsRepository.cs        # MODIFY: + category override methods
src/PcWrapped.Core/Storage/SqliteStatsRepository.cs   # MODIFY: category_overrides table + impl
src/PcWrapped.Core/Categorization/Categorizer.cs      # MODIFY: Normalize -> public static
src/PcWrapped.Core/Categorization/CategoryRules.cs    # CREATE: Merge(defaults, overrides)
src/PcWrapped.Core/Categorization/DefaultRules.cs     # MODIFY: big expanded map
src/PcWrapped/ViewModels/AppRowVm.cs                  # MODIFY: + Category, FromStats(categorizer)
src/PcWrapped/ViewModels/MainViewModel.cs            # MODIFY: merged categorizer + AssignCategoryAsync
src/PcWrapped/Views/MainWindow.axaml(.cs)            # MODIFY: tile context menu + handlers
tests/PcWrapped.Core.Tests/SqliteStatsRepositoryTests.cs # MODIFY: overrides tests
tests/PcWrapped.Core.Tests/CategorizerTests.cs       # MODIFY: expanded DefaultRules asserts
tests/PcWrapped.Core.Tests/CategoryRulesTests.cs     # CREATE
tests/PcWrapped.App.Tests/AppRowVmTests.cs           # MODIFY: pass categorizer + assert Category
```

---

## Task 1: Storage — category_overrides (TDD)

**Files:**
- Modify: `src/PcWrapped.Core/Storage/IStatsRepository.cs`
- Modify: `src/PcWrapped.Core/Storage/SqliteStatsRepository.cs`
- Test: `tests/PcWrapped.Core.Tests/SqliteStatsRepositoryTests.cs`

- [ ] **Step 1: Add interface methods**

In `IStatsRepository.cs`, add inside the interface:
```csharp
    Task UpsertCategoryOverrideAsync(string process, Category category);
    Task<IReadOnlyDictionary<string, Category>> GetCategoryOverridesAsync();
```

- [ ] **Step 2: Write failing tests**

Append inside the `SqliteStatsRepositoryTests` class:
```csharp
    [Fact]
    public async Task CategoryOverrides_UpsertAndGet_RoundTrips()
    {
        var repo = await NewRepoAsync();
        await repo.UpsertCategoryOverrideAsync("pycharm64", Category.Work);
        await repo.UpsertCategoryOverrideAsync("spotify", Category.Social);

        var map = await repo.GetCategoryOverridesAsync();
        Assert.Equal(Category.Work, map["pycharm64"]);
        Assert.Equal(Category.Social, map["spotify"]);
    }

    [Fact]
    public async Task CategoryOverrides_Upsert_UpdatesExisting()
    {
        var repo = await NewRepoAsync();
        await repo.UpsertCategoryOverrideAsync("vlc", Category.Social);
        await repo.UpsertCategoryOverrideAsync("vlc", Category.Other);

        var map = await repo.GetCategoryOverridesAsync();
        Assert.Single(map);
        Assert.Equal(Category.Other, map["vlc"]);
    }
```

- [ ] **Step 3: Run — verify fail**

Run: `dotnet test --filter SqliteStatsRepositoryTests`
Expected: FAIL (methods missing).

- [ ] **Step 4: Add table to schema**

In `SqliteStatsRepository.InitializeAsync`, append to the `sql` string (before the closing `";`):
```sql
CREATE TABLE IF NOT EXISTS category_overrides (
    process  TEXT PRIMARY KEY,
    category TEXT NOT NULL
);
```

- [ ] **Step 5: Implement methods**

Add to `SqliteStatsRepository` (before `Dispose`):
```csharp
    public async Task UpsertCategoryOverrideAsync(string process, Category category)
    {
        await using var cmd = _conn.CreateCommand();
        cmd.CommandText =
            "INSERT INTO category_overrides (process, category) VALUES ($p, $c) " +
            "ON CONFLICT(process) DO UPDATE SET category = $c;";
        cmd.Parameters.AddWithValue("$p", process);
        cmd.Parameters.AddWithValue("$c", category.ToString());
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<IReadOnlyDictionary<string, Category>> GetCategoryOverridesAsync()
    {
        var map = new Dictionary<string, Category>(StringComparer.OrdinalIgnoreCase);
        await using var cmd = _conn.CreateCommand();
        cmd.CommandText = "SELECT process, category FROM category_overrides";
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            if (Enum.TryParse<Category>(r.GetString(1), out var cat)) // skip unknown values safely
                map[r.GetString(0)] = cat;
        return map;
    }
```

- [ ] **Step 6: Run — verify pass + full suite**

Run: `dotnet test --filter SqliteStatsRepositoryTests` (PASS), then `dotnet test PcWrapped.slnx` (all pass).

- [ ] **Step 7: Commit**

```bash
git add -A
git commit -m "feat: store manual category overrides in SQLite"
```

---

## Task 2: CategoryRules.Merge + public Normalize + expanded DefaultRules

**Files:**
- Modify: `src/PcWrapped.Core/Categorization/Categorizer.cs`
- Create: `src/PcWrapped.Core/Categorization/CategoryRules.cs`
- Modify: `src/PcWrapped.Core/Categorization/DefaultRules.cs`
- Create: `tests/PcWrapped.Core.Tests/CategoryRulesTests.cs`
- Modify: `tests/PcWrapped.Core.Tests/CategorizerTests.cs`

- [ ] **Step 1: Make Categorizer.Normalize public static**

In `Categorizer.cs` change the signature:
```csharp
    public static string Normalize(string name)
```
(only `private` → `public`; body unchanged).

- [ ] **Step 2: Write failing CategoryRules tests**

`tests/PcWrapped.Core.Tests/CategoryRulesTests.cs`:
```csharp
using PcWrapped.Core.Categorization;
using PcWrapped.Core.Models;

namespace PcWrapped.Core.Tests;

public class CategoryRulesTests
{
    [Fact]
    public void Merge_OverrideBeatsDefault()
    {
        var defaults = new Dictionary<string, Category> { ["code"] = Category.Work };
        var overrides = new Dictionary<string, Category> { ["code"] = Category.Games };
        var merged = CategoryRules.Merge(defaults, overrides);
        Assert.Equal(Category.Games, merged["code"]);
    }

    [Fact]
    public void Merge_NormalizesNames_ExeOverrideBeatsBareDefault()
    {
        var defaults = new Dictionary<string, Category> { ["code"] = Category.Work };
        var overrides = new Dictionary<string, Category> { ["Code.exe"] = Category.Social };
        var merged = CategoryRules.Merge(defaults, overrides);
        Assert.Single(merged);
        Assert.Equal(Category.Social, merged["code"]);
    }

    [Fact]
    public void Merge_EmptyInputs_Empty()
    {
        var merged = CategoryRules.Merge(
            new Dictionary<string, Category>(), new Dictionary<string, Category>());
        Assert.Empty(merged);
    }
}
```

- [ ] **Step 3: Run — verify fail**

Run: `dotnet test --filter CategoryRulesTests`
Expected: FAIL (CategoryRules missing).

- [ ] **Step 4: Implement CategoryRules**

`src/PcWrapped.Core/Categorization/CategoryRules.cs`:
```csharp
using PcWrapped.Core.Models;

namespace PcWrapped.Core.Categorization;

public static class CategoryRules
{
    public static IReadOnlyDictionary<string, Category> Merge(
        IReadOnlyDictionary<string, Category> defaults,
        IReadOnlyDictionary<string, Category> overrides)
    {
        var result = new Dictionary<string, Category>(StringComparer.OrdinalIgnoreCase);
        foreach (var kv in defaults) result[Categorizer.Normalize(kv.Key)] = kv.Value;
        foreach (var kv in overrides) result[Categorizer.Normalize(kv.Key)] = kv.Value;
        return result;
    }
}
```

- [ ] **Step 5: Run — verify pass**

Run: `dotnet test --filter CategoryRulesTests`
Expected: PASS (3).

- [ ] **Step 6: Expand DefaultRules**

Replace the `Map` initializer in `DefaultRules.cs` with the expanded set (media → Other by design):
```csharp
    public static readonly IReadOnlyDictionary<string, Category> Map =
        new Dictionary<string, Category>(StringComparer.OrdinalIgnoreCase)
        {
            // Browsers
            ["chrome"] = Category.Browser, ["msedge"] = Category.Browser,
            ["firefox"] = Category.Browser, ["opera"] = Category.Browser,
            ["opera_gx"] = Category.Browser, ["brave"] = Category.Browser,
            ["browser"] = Category.Browser, ["vivaldi"] = Category.Browser,
            ["arc"] = Category.Browser,
            // Work / dev / office
            ["code"] = Category.Work, ["devenv"] = Category.Work,
            ["rider64"] = Category.Work, ["idea64"] = Category.Work,
            ["pycharm64"] = Category.Work, ["webstorm64"] = Category.Work,
            ["clion64"] = Category.Work, ["goland64"] = Category.Work,
            ["sublime_text"] = Category.Work, ["notepad++"] = Category.Work,
            ["excel"] = Category.Work, ["winword"] = Category.Work,
            ["powerpnt"] = Category.Work, ["onenote"] = Category.Work,
            ["outlook"] = Category.Work, ["notion"] = Category.Work,
            ["obsidian"] = Category.Work, ["figma"] = Category.Work,
            ["photoshop"] = Category.Work, ["illustrator"] = Category.Work,
            ["blender"] = Category.Work, ["windowsterminal"] = Category.Work,
            ["wt"] = Category.Work, ["powershell"] = Category.Work,
            ["cmd"] = Category.Work,
            // Games / launchers
            ["steam"] = Category.Games, ["steamwebhelper"] = Category.Games,
            ["epicgameslauncher"] = Category.Games, ["battle.net"] = Category.Games,
            ["riotclient"] = Category.Games, ["leagueclient"] = Category.Games,
            ["dota2"] = Category.Games, ["csgo"] = Category.Games,
            ["cs2"] = Category.Games, ["valorant"] = Category.Games,
            ["minecraft"] = Category.Games, ["javaw"] = Category.Games,
            ["galaxyclient"] = Category.Games,
            // Social / chat
            ["discord"] = Category.Social, ["telegram"] = Category.Social,
            ["slack"] = Category.Social, ["teams"] = Category.Social,
            ["ms-teams"] = Category.Social, ["whatsapp"] = Category.Social,
            ["zoom"] = Category.Social, ["skype"] = Category.Social,
            ["viber"] = Category.Social, ["signal"] = Category.Social,
            // Media (no Media category -> Other)
            ["spotify"] = Category.Other, ["vlc"] = Category.Other,
            ["music"] = Category.Other, ["wmplayer"] = Category.Other,
            ["mpc-hc"] = Category.Other, ["foobar2000"] = Category.Other,
        };
```

- [ ] **Step 7: Update CategorizerTests expanded assertions**

In `CategorizerTests.cs`, replace the `DefaultRules_ContainsCommonApps` test body with:
```csharp
    [Fact]
    public void DefaultRules_ContainsCommonApps()
    {
        var c = new Categorizer(DefaultRules.Map);
        Assert.Equal(Category.Browser, c.Categorize("chrome"));
        Assert.Equal(Category.Work, c.Categorize("code"));
        Assert.Equal(Category.Work, c.Categorize("pycharm64"));
        Assert.Equal(Category.Social, c.Categorize("discord"));
        Assert.Equal(Category.Games, c.Categorize("steam"));
        Assert.Equal(Category.Other, c.Categorize("spotify"));
    }
```

- [ ] **Step 8: Run + commit**

Run: `dotnet test PcWrapped.slnx` (all pass).
```bash
git add -A
git commit -m "feat: expand category rules and add CategoryRules.Merge"
```

---

## Task 3: MainViewModel merged categorizer + AppRowVm.Category

**Files:**
- Modify: `src/PcWrapped/ViewModels/AppRowVm.cs`
- Modify: `src/PcWrapped/ViewModels/MainViewModel.cs`
- Test: `tests/PcWrapped.App.Tests/AppRowVmTests.cs`

- [ ] **Step 1: Add Category to AppRowVm + categorizer param to FromStats**

Replace `src/PcWrapped/ViewModels/AppRowVm.cs` with:
```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using PcWrapped.Core.Categorization;
using PcWrapped.Core.Models;

namespace PcWrapped.ViewModels;

public sealed record AppRowVm(string Name, string TimeText, double Fraction,
    string? ExecutablePath, Category Category)
{
    public static IReadOnlyList<AppRowVm> FromStats(
        PeriodStats stats, IReadOnlyDictionary<string, string> paths, Categorizer categorizer)
    {
        if (stats.TopApps.Count == 0) return Array.Empty<AppRowVm>();
        double max = stats.TopApps.Max(a => a.Duration.TotalSeconds);
        if (max <= 0) max = 1;
        return stats.TopApps.Select(a =>
        {
            paths.TryGetValue(a.ProcessName, out var p);
            return new AppRowVm(a.ProcessName, FormatHours(a.Duration),
                a.Duration.TotalSeconds / max, p, categorizer.Categorize(a.ProcessName));
        }).ToList();
    }

    private static string FormatHours(TimeSpan t) =>
        t.TotalHours >= 1 ? $"{(int)t.TotalHours}ч {t.Minutes:00}м" : $"{t.Minutes}м";
}
```

- [ ] **Step 2: Update AppRowVmTests**

In `tests/PcWrapped.App.Tests/AppRowVmTests.cs`: add `using PcWrapped.Core.Categorization;`. Update both tests to pass a categorizer and assert Category. Replace the two test methods with:
```csharp
    [Fact]
    public void FromStats_ComputesFractionTimeAndPath()
    {
        var stats = Stats(
            new AppUsage("code", TimeSpan.FromHours(2)),
            new AppUsage("chrome", TimeSpan.FromHours(1)));
        var rows = AppRowVm.FromStats(stats,
            new Dictionary<string, string> { ["code"] = @"C:\code.exe" },
            new Categorizer(DefaultRules.Map));

        Assert.Equal(2, rows.Count);
        Assert.Equal("code", rows[0].Name);
        Assert.Equal(1.0, rows[0].Fraction, 3);
        Assert.Equal(0.5, rows[1].Fraction, 3);
        Assert.Equal("2ч 00м", rows[0].TimeText);
        Assert.Equal(@"C:\code.exe", rows[0].ExecutablePath);
        Assert.Null(rows[1].ExecutablePath);
        Assert.Equal(Category.Work, rows[0].Category);
        Assert.Equal(Category.Browser, rows[1].Category);
    }

    [Fact]
    public void FromStats_EmptyTopApps_ReturnsEmpty()
    {
        Assert.Empty(AppRowVm.FromStats(Stats(),
            new Dictionary<string, string>(), new Categorizer(DefaultRules.Map)));
    }
```

- [ ] **Step 3: Update MainViewModel — merged categorizer + AssignCategoryAsync**

In `src/PcWrapped/ViewModels/MainViewModel.cs`:
1. Replace the field `private readonly Categorizer _categorizer = new(DefaultRules.Map);` with a public, rebuildable property:
```csharp
    public Categorizer Categorizer { get; private set; } = new(DefaultRules.Map);
```
2. In `BuildStatsAsync`, BEFORE building the stats, load overrides and rebuild the categorizer; use `Categorizer` in `BuildPeriodStats`:
```csharp
        var overrides = await _repo.GetCategoryOverridesAsync();
        Categorizer = new Categorizer(CategoryRules.Merge(DefaultRules.Map, overrides));
```
   and change the `Aggregator.BuildPeriodStats(...)` call to pass `Categorizer` instead of the old `_categorizer`.
3. Add the assignment method:
```csharp
    public Task AssignCategoryAsync(string process, Category category) =>
        _repo.UpsertCategoryOverrideAsync(process, category);
```
Ensure `using PcWrapped.Core.Categorization;` and `using PcWrapped.Core.Models;` are present (they are).

- [ ] **Step 4: Build + test**

Run: `dotnet build PcWrapped.slnx` (0 errors; note: MainWindow.axaml.cs calls `AppRowVm.FromStats(_current, paths)` with 2 args and will fail to compile — fix it in this step too: change that call to `AppRowVm.FromStats(_current, paths, Vm.Categorizer)`). Then `dotnet test PcWrapped.slnx` (all pass).

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "feat: merge category overrides into categorizer; expose app category"
```

---

## Task 4: Tile context menu (manual assignment)

**Files:**
- Modify: `src/PcWrapped/Views/MainWindow.axaml`
- Modify: `src/PcWrapped/Views/MainWindow.axaml.cs`

- [ ] **Step 1: Add ContextMenu to the app tile**

In `MainWindow.axaml`, inside the app `DataTemplate`, add a `ContextMenu` to the `appTile` Border. Find the line `<Border Classes="appTile" Width="332" Height="60" Margin="0,0,10,10" Padding="8">` and insert this block immediately after it (as the first child of the Border, before the existing `<Grid ...>`):
```xml
                <Border.ContextMenu>
                  <ContextMenu Opening="OnCategoryMenuOpening">
                    <MenuItem Header="Работа" Tag="Work" ToggleType="CheckBox" Click="OnAssignCategory"/>
                    <MenuItem Header="Игры" Tag="Games" ToggleType="CheckBox" Click="OnAssignCategory"/>
                    <MenuItem Header="Соцсети" Tag="Social" ToggleType="CheckBox" Click="OnAssignCategory"/>
                    <MenuItem Header="Браузер" Tag="Browser" ToggleType="CheckBox" Click="OnAssignCategory"/>
                    <MenuItem Header="Прочее" Tag="Other" ToggleType="CheckBox" Click="OnAssignCategory"/>
                  </ContextMenu>
                </Border.ContextMenu>
```

- [ ] **Step 2: Add handlers in code-behind**

In `src/PcWrapped/Views/MainWindow.axaml.cs`:
- Add usings: `using System.Linq;` and `using PcWrapped.Core.Models;`.
- Add these two methods to the class:
```csharp
    private void OnCategoryMenuOpening(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (sender is ContextMenu cm && cm.DataContext is AppRowVm row)
            foreach (var item in cm.Items.OfType<MenuItem>())
                item.IsChecked = item.Tag as string == row.Category.ToString();
    }

    private async void OnAssignCategory(object? sender, RoutedEventArgs e)
    {
        if (sender is MenuItem mi && mi.Tag is string tag
            && Enum.TryParse<Category>(tag, out var cat)
            && mi.DataContext is AppRowVm row)
        {
            await Vm.AssignCategoryAsync(row.Name, cat);
            await RefreshAsync();
        }
    }
```

- [ ] **Step 3: Build**

Run: `dotnet build PcWrapped.slnx`
Expected: 0 errors. (If a running PcWrapped.exe locks the build, `taskkill //F //IM PcWrapped.exe` first. If `ContextMenu.Opening` event name/signature differs in Avalonia 11, use the correct member — Avalonia 11 `ContextMenu` exposes an `Opening` event with `CancelEventArgs`; if unavailable, attach via `MenuOpened`/`Opened` — make the minimal fix and document it.)

- [ ] **Step 4: Tests still green**

Run: `dotnet test PcWrapped.slnx`
Expected: all pass.

- [ ] **Step 5: Manual verification**

Run the app. Right-click an app tile → category menu appears with the current category checked. Pick a different category → donut/legend update immediately (the app moves into that category). Re-open the app later → the override persists (stored in DB). Close the app.

- [ ] **Step 6: Commit**

```bash
git add -A
git commit -m "feat: assign app category via right-click context menu"
```

---

## Final verification
- [ ] `dotnet test PcWrapped.slnx` — all pass.
- [ ] `dotnet run --project src/PcWrapped` — most common apps auto-categorized (donut colorful); right-click → reassign category persists and updates the donut.
- [ ] All new commits authored by `090_TYPE`, no `Co-Authored-By` trailers.

## Spec Coverage Notes
- category_overrides storage (+ unknown-value guard) → Task 1.
- CategoryRules.Merge (override wins, normalization) + public Normalize → Task 2.
- Expanded DefaultRules (media→Other per §9) → Task 2.
- Merged categorizer in MainViewModel + AssignCategoryAsync → Task 3.
- Right-click context menu with current-category checkmark + immediate refresh → Task 4.
- Out of scope (custom categories, bulk editor) → not included.
```
