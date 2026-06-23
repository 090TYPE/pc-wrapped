# PC Wrapped — Settings screen Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Экран настроек: вкл/выкл трекинга, автозапуск, исключения приложений (право-клик + список), очистка данных, открыть папку данных.

**Architecture:** Исключения и очистка живут в SQLite/репозитории. `ActivityTracker` пропускает исключённые (инъекция предиката). `AppController` (app-слой) держит ресурсы App и даёт окну настроек живые операции. `SettingsWindow` открывается из рейла; статистика фильтрует исключённых.

**Tech Stack:** .NET 8, Avalonia 11.0.10, C#, Microsoft.Data.Sqlite, xUnit.

**Commit author:** ВСЕ коммиты — обычный `git commit -m "..."` (git config user = `090_TYPE`). НИКАКИХ `Co-Authored-By` трейлеров, без `--author`, без упоминания Claude/AI. Solution: `PcWrapped.slnx`. App project БЕЗ ImplicitUsings (явные `using`); Core — ImplicitUsings ON. If a running PcWrapped.exe locks a build, `taskkill //F //IM PcWrapped.exe` first.

---

## File Structure
```
src/PcWrapped.Core/Storage/IStatsRepository.cs        # MODIFY: exclusions + ClearAllData
src/PcWrapped.Core/Storage/SqliteStatsRepository.cs   # MODIFY: excluded_apps table + methods + ClearAllData
src/PcWrapped.Core/Tracking/ActivityTracker.cs        # MODIFY: isExcluded predicate
src/PcWrapped/Native/Win32InputCounterSource.cs       # MODIFY: Stop()
src/PcWrapped/AppController.cs                         # CREATE: live settings operations
src/PcWrapped/ViewModels/MainViewModel.cs            # MODIFY: filter excluded in stats
src/PcWrapped/App.axaml.cs                            # MODIFY: build controller, predicate, pass to window
src/PcWrapped/Views/SettingsWindow.axaml(.cs)        # CREATE
src/PcWrapped/Views/MainWindow.axaml(.cs)            # MODIFY: ⚙ button + "Exclude" menu item
src/PcWrapped/Localization/Loc.cs                    # MODIFY: settings.* / menu.exclude / rail.settings keys
tests/PcWrapped.Core.Tests/SqliteStatsRepositoryTests.cs # MODIFY
tests/PcWrapped.Core.Tests/ActivityTrackerTests.cs   # MODIFY
```

---

## Task 1: Storage — exclusions + clear all data (TDD)

**Files:**
- Modify: `src/PcWrapped.Core/Storage/IStatsRepository.cs`
- Modify: `src/PcWrapped.Core/Storage/SqliteStatsRepository.cs`
- Test: `tests/PcWrapped.Core.Tests/SqliteStatsRepositoryTests.cs`

- [ ] **Step 1: Interface methods**

In `IStatsRepository.cs` add:
```csharp
    Task AddExclusionAsync(string process);
    Task RemoveExclusionAsync(string process);
    Task<IReadOnlySet<string>> GetExclusionsAsync();
    Task ClearAllDataAsync();
```

- [ ] **Step 2: Failing tests**

Append inside `SqliteStatsRepositoryTests`:
```csharp
    [Fact]
    public async Task Exclusions_AddRemoveGet()
    {
        var repo = await NewRepoAsync();
        await repo.AddExclusionAsync("discord");
        await repo.AddExclusionAsync("discord"); // idempotent
        await repo.AddExclusionAsync("steam");
        Assert.True((await repo.GetExclusionsAsync()).Contains("DISCORD")); // case-insensitive
        await repo.RemoveExclusionAsync("discord");
        var set = await repo.GetExclusionsAsync();
        Assert.False(set.Contains("discord"));
        Assert.True(set.Contains("steam"));
    }

    [Fact]
    public async Task ClearAllData_WipesStatsButKeepsExclusions()
    {
        var repo = await NewRepoAsync();
        var t = new DateTimeOffset(2026, 6, 9, 10, 0, 0, TimeSpan.Zero);
        await repo.AddSampleAsync(new UsageSample(t, "code", "x", 60));
        await repo.AddInputCountersAsync(new DateOnly(2026, 6, 9), new InputCounters(10, 2, 100));
        await repo.UpsertAppPathAsync("code", @"C:\code.exe");
        await repo.UpsertCategoryOverrideAsync("code", Category.Games);
        await repo.AddExclusionAsync("discord");

        await repo.ClearAllDataAsync();

        Assert.Empty(await repo.GetSamplesAsync(t.AddDays(-1), t.AddDays(1)));
        Assert.Equal(0, (await repo.GetInputCountersAsync(new DateOnly(2026, 6, 9), new DateOnly(2026, 6, 9))).Keystrokes);
        Assert.Empty(await repo.GetAppPathsAsync());
        Assert.Empty(await repo.GetCategoryOverridesAsync());
        Assert.True((await repo.GetExclusionsAsync()).Contains("discord")); // exclusions preserved
    }
```

- [ ] **Step 3: Run — verify fail**

Run: `dotnet test --filter SqliteStatsRepositoryTests`
Expected: FAIL (methods missing).

- [ ] **Step 4: Add table + implement**

In `InitializeAsync` schema string append:
```sql
CREATE TABLE IF NOT EXISTS excluded_apps (
    process TEXT PRIMARY KEY
);
```
Add to `SqliteStatsRepository` (before `Dispose`):
```csharp
    public async Task AddExclusionAsync(string process)
    {
        await using var cmd = _conn.CreateCommand();
        cmd.CommandText = "INSERT OR IGNORE INTO excluded_apps (process) VALUES ($p);";
        cmd.Parameters.AddWithValue("$p", process);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task RemoveExclusionAsync(string process)
    {
        await using var cmd = _conn.CreateCommand();
        cmd.CommandText = "DELETE FROM excluded_apps WHERE process = $p COLLATE NOCASE;";
        cmd.Parameters.AddWithValue("$p", process);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<IReadOnlySet<string>> GetExclusionsAsync()
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using var cmd = _conn.CreateCommand();
        cmd.CommandText = "SELECT process FROM excluded_apps";
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync()) set.Add(r.GetString(0));
        return set;
    }

    public async Task ClearAllDataAsync()
    {
        await using var tx = (Microsoft.Data.Sqlite.SqliteTransaction)await _conn.BeginTransactionAsync();
        await using (var cmd = _conn.CreateCommand())
        {
            cmd.Transaction = tx;
            cmd.CommandText =
                "DELETE FROM samples; DELETE FROM input_counters; " +
                "DELETE FROM app_paths; DELETE FROM category_overrides;";
            await cmd.ExecuteNonQueryAsync();
        }
        await tx.CommitAsync();
    }
```

- [ ] **Step 5: Run — verify pass + full suite + commit**

Run: `dotnet test --filter SqliteStatsRepositoryTests` (PASS), then `dotnet test PcWrapped.slnx` (all pass).
```bash
git add -A
git commit -m "feat: add app exclusions storage and clear-all-data"
```

---

## Task 2: Tracker exclusion + input Stop()

**Files:**
- Modify: `src/PcWrapped.Core/Tracking/ActivityTracker.cs`
- Modify: `src/PcWrapped/Native/Win32InputCounterSource.cs`
- Test: `tests/PcWrapped.Core.Tests/ActivityTrackerTests.cs`

- [ ] **Step 1: Failing tests**

Append inside `ActivityTrackerTests`:
```csharp
    [Fact]
    public async Task Tick_ExcludedProcess_NotRecorded_ButCountersStill()
    {
        var repo = await NewRepoAsync();
        var fg = new FakeForeground { Current = new ForegroundInfo("code", "x", @"C:\code.exe"), Idle = 0 };
        var input = new FakeInput { Pending = new InputCounters(5, 1, 20) };
        var tracker = new ActivityTracker(repo, fg, input, 2, 60, isExcluded: p => p == "code");

        await tracker.TickAsync(new DateTimeOffset(2026, 6, 9, 10, 0, 0, TimeSpan.Zero));

        Assert.Empty(await repo.GetSamplesAsync(
            new DateTimeOffset(2026, 6, 9, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 6, 10, 0, 0, 0, TimeSpan.Zero)));
        Assert.Empty(await repo.GetAppPathsAsync());
        var counters = await repo.GetInputCountersAsync(new DateOnly(2026, 6, 9), new DateOnly(2026, 6, 9));
        Assert.Equal(5, counters.Keystrokes); // input counters still recorded
    }

    [Fact]
    public async Task Tick_NotExcluded_RecordsSample()
    {
        var repo = await NewRepoAsync();
        var fg = new FakeForeground { Current = new ForegroundInfo("code", "x"), Idle = 0 };
        var tracker = new ActivityTracker(repo, fg, new FakeInput(), 2, 60, isExcluded: p => false);

        await tracker.TickAsync(new DateTimeOffset(2026, 6, 9, 10, 0, 0, TimeSpan.Zero));

        Assert.Single(await repo.GetSamplesAsync(
            new DateTimeOffset(2026, 6, 9, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 6, 10, 0, 0, 0, TimeSpan.Zero)));
    }
```

- [ ] **Step 2: Run — verify fail**

Run: `dotnet test --filter ActivityTrackerTests`
Expected: FAIL (ctor has no isExcluded param).

- [ ] **Step 3: Add predicate to ActivityTracker**

Modify `ActivityTracker`: add a field and optional ctor param, and the skip check.
- Add field: `private readonly Func<string, bool>? _isExcluded;`
- Change the constructor signature to add a last optional parameter `Func<string, bool>? isExcluded = null` and assign `_isExcluded = isExcluded;` in the body.
- Add `using System;` at the top (Func lives in System; Core has ImplicitUsings so it's available, but add explicitly only if the build complains).
- In `TickAsync`, immediately after `if (fg is null) return;` add:
```csharp
        if (_isExcluded is not null && _isExcluded(fg.Value.ProcessName)) return;
```

- [ ] **Step 4: Run — verify pass**

Run: `dotnet test --filter ActivityTrackerTests`
Expected: PASS.

- [ ] **Step 5: Add Win32InputCounterSource.Stop()**

In `src/PcWrapped/Native/Win32InputCounterSource.cs` add a public `Stop()` (unhooks but keeps the object reusable; `Start()` can be called again because the delegates are retained in fields):
```csharp
    public void Stop()
    {
        if (_kbHook != IntPtr.Zero) UnhookWindowsHookEx(_kbHook);
        if (_mouseHook != IntPtr.Zero) UnhookWindowsHookEx(_mouseHook);
        _kbHook = _mouseHook = IntPtr.Zero;
    }
```
And change `Dispose()` to call `Stop()` (replace its unhook body with `Stop();`).

- [ ] **Step 6: Build + full suite + commit**

Run: `dotnet build PcWrapped.slnx` (0 errors), `dotnet test PcWrapped.slnx` (all pass).
```bash
git add -A
git commit -m "feat: tracker skips excluded apps; input source can stop/restart"
```

---

## Task 3: AppController + stats filter + App wiring

**Files:**
- Create: `src/PcWrapped/AppController.cs`
- Modify: `src/PcWrapped/ViewModels/MainViewModel.cs`
- Modify: `src/PcWrapped/App.axaml.cs`

- [ ] **Step 1: Filter excluded in MainViewModel.BuildStatsAsync**

In `src/PcWrapped/ViewModels/MainViewModel.cs`:
- Add `using System.Linq;` at the top.
- In `BuildStatsAsync`, after `var samples = await _repo.GetSamplesAsync(fromDt, toDt);` add:
```csharp
        var excluded = await _repo.GetExclusionsAsync();
        var visible = excluded.Count == 0
            ? samples
            : samples.Where(s => !excluded.Contains(s.ProcessName)).ToList();
```
- Change the `Aggregator.BuildPeriodStats(...)` call to pass `visible` instead of `samples`.

- [ ] **Step 2: Create AppController**

`src/PcWrapped/AppController.cs`:
```csharp
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using PcWrapped.Core.Settings;
using PcWrapped.Core.Storage;
using PcWrapped.Native;

namespace PcWrapped;

/// <summary>Live settings operations shared between App and the settings window.</summary>
public sealed class AppController
{
    private readonly IStatsRepository _repo;
    private readonly Win32InputCounterSource _input;
    private readonly JsonSettingsStore _settingsStore;
    private readonly string _dataDir;
    private readonly HashSet<string> _excluded;
    private bool _tracking;
    private bool _autostart;

    public AppController(IStatsRepository repo, Win32InputCounterSource input,
        JsonSettingsStore settingsStore, string dataDir, AppSettings settings,
        IReadOnlySet<string> excluded)
    {
        _repo = repo;
        _input = input;
        _settingsStore = settingsStore;
        _dataDir = dataDir;
        _tracking = settings.CountInput;
        _autostart = settings.Autostart;
        _excluded = new HashSet<string>(excluded, StringComparer.OrdinalIgnoreCase);
    }

    public IStatsRepository Repo => _repo;

    public bool IsExcluded(string process) => _excluded.Contains(process);
    public IReadOnlyCollection<string> Exclusions => _excluded;

    public bool TrackingEnabled => _tracking;
    public bool AutostartEnabled => _autostart;

    public void SetTracking(bool on)
    {
        _tracking = on;
        if (on) _input.Start(); else _input.Stop();
        SaveSettings();
    }

    public void SetAutostart(bool on)
    {
        _autostart = on;
        try { AutostartManager.SetEnabled(on, Environment.ProcessPath!); } catch { /* registry */ }
        SaveSettings();
    }

    public async Task AddExclusionAsync(string process)
    {
        await _repo.AddExclusionAsync(process);
        _excluded.Add(process);
    }

    public async Task RemoveExclusionAsync(string process)
    {
        await _repo.RemoveExclusionAsync(process);
        _excluded.Remove(process);
    }

    public Task ClearDataAsync() => _repo.ClearAllDataAsync();

    public void OpenDataFolder()
    {
        try { Process.Start(new ProcessStartInfo("explorer.exe", _dataDir) { UseShellExecute = true }); }
        catch { /* ignore */ }
    }

    private void SaveSettings()
    {
        var s = _settingsStore.Load();
        _settingsStore.Save(s with { CountInput = _tracking, Autostart = _autostart });
    }
}
```

- [ ] **Step 3: Wire AppController in App.axaml.cs**

In `src/PcWrapped/App.axaml.cs`:
- Add a field: `private AppController? _controller;`
- After `_input = new Win32InputCounterSource();` and BEFORE creating `_tracker`, load exclusions and build the controller:
```csharp
        var excluded = await _repo.GetExclusionsAsync();
        _controller = new AppController(_repo, _input, settingsStore, dir, settings, excluded);
```
- Change the `_tracker = new ActivityTracker(...)` line to pass the predicate:
```csharp
        _tracker = new ActivityTracker(_repo, new Win32ForegroundWindowSource(), _input,
            IntervalSeconds, idleThresholdSeconds: 180, isExcluded: _controller.IsExcluded);
```
- In `ShowMainWindow`, set the controller on the window after creating it:
```csharp
        var window = new MainWindow { DataContext = vm, Controller = _controller };
```

- [ ] **Step 4: Build + test**

Run: `dotnet build PcWrapped.slnx` (0 errors; note `MainWindow.Controller` property is added in Task 4 — if building this task alone fails because `Controller` doesn't exist yet, temporarily set it via a TODO is NOT allowed; instead add the `public AppController? Controller { get; set; }` auto-property to MainWindow.axaml.cs now as part of this step). Then `dotnet test PcWrapped.slnx` (all pass).

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "feat: AppController for live settings; filter excluded apps from stats"
```

---

## Task 4: SettingsWindow + rail button + exclude menu + Loc

**Files:**
- Create: `src/PcWrapped/Views/SettingsWindow.axaml` + `.cs`
- Modify: `src/PcWrapped/Views/MainWindow.axaml` + `.cs`
- Modify: `src/PcWrapped/Localization/Loc.cs`

- [ ] **Step 1: Add Loc keys**

In `src/PcWrapped/Localization/Loc.cs`, add to the **Ru** dictionary:
```csharp
        ["rail.settings"] = "Настройки",
        ["menu.exclude"] = "Исключить",
        ["settings.title"] = "Настройки",
        ["settings.tracking"] = "Считать ввод (клавиши/мышь)",
        ["settings.autostart"] = "Запускать при старте Windows",
        ["settings.exclusions"] = "Исключённые приложения",
        ["settings.noExclusions"] = "Нет исключений",
        ["settings.clear"] = "Очистить данные",
        ["settings.clearConfirm"] = "Точно удалить всю статистику?",
        ["settings.confirm"] = "Удалить",
        ["settings.cancel"] = "Отмена",
        ["settings.openFolder"] = "Открыть папку данных",
```
and to the **En** dictionary:
```csharp
        ["rail.settings"] = "Settings",
        ["menu.exclude"] = "Exclude",
        ["settings.title"] = "Settings",
        ["settings.tracking"] = "Count input (keys/mouse)",
        ["settings.autostart"] = "Launch on Windows startup",
        ["settings.exclusions"] = "Excluded apps",
        ["settings.noExclusions"] = "No exclusions",
        ["settings.clear"] = "Clear data",
        ["settings.clearConfirm"] = "Really delete all statistics?",
        ["settings.confirm"] = "Delete",
        ["settings.cancel"] = "Cancel",
        ["settings.openFolder"] = "Open data folder",
```

- [ ] **Step 2: Create SettingsWindow.axaml**

`src/PcWrapped/Views/SettingsWindow.axaml`:
```xml
<Window xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        x:Class="PcWrapped.Views.SettingsWindow"
        Width="440" Height="520"
        Background="{StaticResource BgBrush}" Foreground="{StaticResource TextBrush}"
        WindowStartupLocation="CenterOwner" Title="Settings">
  <Grid RowDefinitions="Auto,Auto,Auto,*,Auto" Margin="20">
    <TextBlock x:Name="HeaderText" FontSize="20" FontWeight="Bold" Text="Settings"/>
    <StackPanel Grid.Row="1" Spacing="10" Margin="0,14,0,0">
      <CheckBox x:Name="TrackingToggle" Content="tracking"/>
      <CheckBox x:Name="AutostartToggle" Content="autostart"/>
    </StackPanel>
    <TextBlock Grid.Row="2" x:Name="ExclLabel" Classes="lab" Text="EXCLUSIONS" Margin="0,16,0,6"/>
    <Border Grid.Row="3" Classes="appTile" Padding="6">
      <ScrollViewer>
        <StackPanel x:Name="ExclList" Spacing="2"/>
      </ScrollViewer>
    </Border>
    <StackPanel Grid.Row="4" Orientation="Horizontal" Spacing="8" Margin="0,14,0,0">
      <Button x:Name="OpenFolderBtn" Content="Open folder"/>
      <Button x:Name="ClearBtn" Content="Clear"/>
      <StackPanel x:Name="ConfirmPanel" Orientation="Horizontal" Spacing="8" IsVisible="False">
        <TextBlock x:Name="ConfirmText" VerticalAlignment="Center"/>
        <Button x:Name="ConfirmYes" Classes="share" Content="Delete"/>
        <Button x:Name="ConfirmNo" Content="Cancel"/>
      </StackPanel>
    </StackPanel>
  </Grid>
</Window>
```

- [ ] **Step 3: Create SettingsWindow.axaml.cs**

`src/PcWrapped/Views/SettingsWindow.axaml.cs`:
```csharp
using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Markup.Xaml;
using PcWrapped.Localization;

namespace PcWrapped.Views;

public partial class SettingsWindow : Window
{
    private readonly AppController _controller;

    // Parameterless ctor for the XAML previewer only; real use passes a controller.
    public SettingsWindow() : this(null!) { }

    public SettingsWindow(AppController controller)
    {
        _controller = controller;
        AvaloniaXamlLoader.Load(this);

        Title = Loc.T("settings.title");
        this.FindControl<TextBlock>("HeaderText")!.Text = Loc.T("settings.title");
        this.FindControl<TextBlock>("ExclLabel")!.Text = Loc.T("settings.exclusions");

        var tracking = this.FindControl<CheckBox>("TrackingToggle")!;
        tracking.Content = Loc.T("settings.tracking");
        var autostart = this.FindControl<CheckBox>("AutostartToggle")!;
        autostart.Content = Loc.T("settings.autostart");
        this.FindControl<Button>("OpenFolderBtn")!.Content = Loc.T("settings.openFolder");
        this.FindControl<Button>("ClearBtn")!.Content = Loc.T("settings.clear");
        this.FindControl<TextBlock>("ConfirmText")!.Text = Loc.T("settings.clearConfirm");
        this.FindControl<Button>("ConfirmYes")!.Content = Loc.T("settings.confirm");
        this.FindControl<Button>("ConfirmNo")!.Content = Loc.T("settings.cancel");

        if (_controller is not null)
        {
            tracking.IsChecked = _controller.TrackingEnabled;
            autostart.IsChecked = _controller.AutostartEnabled;
            tracking.IsCheckedChanged += (_, _) => _controller.SetTracking(tracking.IsChecked == true);
            autostart.IsCheckedChanged += (_, _) => _controller.SetAutostart(autostart.IsChecked == true);
            this.FindControl<Button>("OpenFolderBtn")!.Click += (_, _) => _controller.OpenDataFolder();
            this.FindControl<Button>("ClearBtn")!.Click += OnClearClicked;
            this.FindControl<Button>("ConfirmNo")!.Click += (_, _) => SetConfirm(false);
            this.FindControl<Button>("ConfirmYes")!.Click += OnConfirmClear;
            RebuildExclusions();
        }
    }

    private void SetConfirm(bool visible)
    {
        this.FindControl<StackPanel>("ConfirmPanel")!.IsVisible = visible;
        this.FindControl<Button>("ClearBtn")!.IsVisible = !visible;
    }

    private void OnClearClicked(object? sender, RoutedEventArgs e) => SetConfirm(true);

    private async void OnConfirmClear(object? sender, RoutedEventArgs e)
    {
        await _controller.ClearDataAsync();
        SetConfirm(false);
    }

    private void RebuildExclusions()
    {
        var list = this.FindControl<StackPanel>("ExclList")!;
        list.Children.Clear();
        var items = _controller.Exclusions.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList();
        if (items.Count == 0)
        {
            list.Children.Add(new TextBlock { Text = Loc.T("settings.noExclusions"), Opacity = 0.6, Margin = new Avalonia.Thickness(6) });
            return;
        }
        foreach (var p in items)
        {
            var captured = p;
            var row = new DockPanel { Margin = new Avalonia.Thickness(6, 3) };
            var remove = new Button { Content = "✕", Padding = new Avalonia.Thickness(8, 2), [DockPanel.DockProperty] = Dock.Right };
            remove.Click += async (_, _) => { await _controller.RemoveExclusionAsync(captured); RebuildExclusions(); };
            row.Children.Add(remove);
            row.Children.Add(new TextBlock { Text = captured, VerticalAlignment = VerticalAlignment.Center });
            list.Children.Add(row);
        }
    }
}
```

- [ ] **Step 4: MainWindow — ⚙ button + Exclude menu item**

In `src/PcWrapped/Views/MainWindow.axaml`:
- Add a settings button to the rail (e.g., right after the brand TextBlock `📊 PC Wrapped`):
```xml
        <Button x:Name="SettingsBtn" Content="⚙" HorizontalAlignment="Right" Margin="0,-30,0,8"
                Background="Transparent" Padding="4"/>
```
- In the app-tile `ContextMenu`, add an Exclude item after the 5 category items:
```xml
                    <Separator/>
                    <MenuItem x:Name="ExcludeItem" Header="Исключить" Tag="__exclude" Click="OnExcludeApp"/>
```

In `src/PcWrapped/Views/MainWindow.axaml.cs`:
- Ensure the auto-property exists (added in Task 3): `public AppController? Controller { get; set; }`.
- In the constructor wiring, add: `this.FindControl<Button>("SettingsBtn")!.Click += OnOpenSettings;`
- In `ApplyLanguage()`, add: `this.FindControl<Button>("SettingsBtn")!.SetValue(ToolTip.TipProperty, Loc.T("rail.settings"));` and localize the exclude item is handled in `OnCategoryMenuOpening` (next bullet).
- In `OnCategoryMenuOpening`, after the existing loop that sets category item headers/icons, also localize the exclude item if present:
```csharp
            var exclude = cm.Items.OfType<MenuItem>().FirstOrDefault(m => (m.Tag as string) == "__exclude");
            if (exclude is not null) exclude.Header = Loc.T("menu.exclude");
```
- Add the handlers:
```csharp
    private async void OnExcludeApp(object? sender, RoutedEventArgs e)
    {
        if (Controller is not null && sender is MenuItem mi && mi.DataContext is ViewModels.AppRowVm row)
        {
            await Controller.AddExclusionAsync(row.Name);
            await RefreshAsync();
        }
    }

    private async void OnOpenSettings(object? sender, RoutedEventArgs e)
    {
        if (Controller is null) return;
        var win = new SettingsWindow(Controller);
        await win.ShowDialog(this);
        await RefreshAsync();
    }
```
Add `using Avalonia.Controls;` and `using Avalonia.Controls.ApplicationLifetimes;` only if missing (ToolTip is in `Avalonia.Controls`).

- [ ] **Step 5: Build**

Run: `dotnet build PcWrapped.slnx`
Expected: 0 errors. If `OnCategoryMenuOpening` doesn't currently iterate with `using System.Linq;`, ensure it's imported (it is, from the categories work). If `ToolTip.TipProperty` path differs, use `ToolTip.SetTip(button, Loc.T("rail.settings"));` instead.

- [ ] **Step 6: Tests**

Run: `dotnet test PcWrapped.slnx`
Expected: all pass.

- [ ] **Step 7: Manual verification**

Run the app. Click ⚙ → Settings opens: toggle tracking off/on, toggle autostart, see exclusions list; right-click an app tile → "Исключить"/"Exclude" → it disappears from stats and appears in Settings exclusions; remove it there → reappears after refresh. "Open data folder" opens Explorer. "Clear data" → confirm → stats reset. Close the app.

- [ ] **Step 8: Commit**

```bash
git add -A
git commit -m "feat: settings window, app exclusions, clear data, open folder"
```

---

## Final verification
- [ ] `dotnet test PcWrapped.slnx` — all pass.
- [ ] `dotnet run --project src/PcWrapped` — ⚙ opens settings; tracking/autostart toggles work; right-click exclude hides an app; clear-data resets; open-folder works; localized RU/EN.
- [ ] All new commits authored by `090_TYPE`, no `Co-Authored-By` trailers.

## Spec Coverage Notes
- excluded_apps + ClearAllData storage → Task 1.
- Tracker exclusion + input Stop → Task 2.
- AppController (live tracking/autostart/clear/open folder/exclusions) + stats filter + App wiring → Task 3.
- SettingsWindow + rail ⚙ + right-click Exclude + Loc → Task 4.
- Tests (storage, tracker) → Tasks 1–2; stats filter covered by the excluded Where + tracker tests (no separate VM test — integration via repo).
- Out of scope (export/import, schedule, granular clear) → not included.
```
