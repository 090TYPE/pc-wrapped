# PC Wrapped — Polish batch Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Полировка: реальный DPI мыши, копирование карточки в буфер, иконка приложения/трея, пустой стейт, память темы/периода.

**Architecture:** Тема/период хранятся в `AppSettings` и управляются `AppController`. Буфер обмена — Win32 `CF_BITMAP` через `System.Drawing`. Иконка — сгенерированный `app.ico`, прописанный в csproj/axaml. Пустой стейт и восстановление — в `MainWindow`.

**Tech Stack:** .NET 8, Avalonia 11.0.10, C#, System.Drawing.Common, xUnit.

**Commit author:** ВСЕ коммиты — обычный `git commit -m "..."` (git config user = `090_TYPE`). НИКАКИХ `Co-Authored-By` трейлеров, без `--author`, без упоминания Claude/AI. Solution: `PcWrapped.slnx`. App project БЕЗ ImplicitUsings (явные `using`). If a running PcWrapped.exe locks a build, `taskkill //F //IM PcWrapped.exe` first.

---

## File Structure
```
src/PcWrapped.Core/Settings/AppSettings.cs        # MODIFY: Theme + Period fields
src/PcWrapped/AppController.cs                     # MODIFY: Theme/Period get/set + save
src/PcWrapped/Native/ClipboardImage.cs            # CREATE: Win32 CF_BITMAP clipboard
src/PcWrapped/Assets/app.ico                       # CREATE: generated app icon
src/PcWrapped/PcWrapped.csproj                     # MODIFY: ApplicationIcon + AvaloniaResource
src/PcWrapped/App.axaml                            # MODIFY: TrayIcon Icon
src/PcWrapped/Localization/Loc.cs                 # MODIFY: rail.copy, dash.empty
src/PcWrapped/Views/MainWindow.axaml(.cs)         # MODIFY: DPI, copy btn, empty state, restore theme/period, Window.Icon
tests/PcWrapped.Core.Tests/JsonSettingsStoreTests.cs # MODIFY: Theme/Period round-trip
```

---

## Task 1: AppSettings Theme/Period + AppController persistence (TDD for settings)

**Files:**
- Modify: `src/PcWrapped.Core/Settings/AppSettings.cs`
- Modify: `src/PcWrapped/AppController.cs`
- Test: `tests/PcWrapped.Core.Tests/JsonSettingsStoreTests.cs`

- [ ] **Step 1: Add fields to AppSettings**

Replace `src/PcWrapped.Core/Settings/AppSettings.cs` with:
```csharp
namespace PcWrapped.Core.Settings;

public sealed record AppSettings(
    bool HasOnboarded,
    bool CountInput,
    bool Autostart,
    string Language = "ru",
    string Theme = "gradient",
    string Period = "Week")
{
    public static AppSettings Default => new(false, true, true, "ru", "gradient", "Week");
}
```

- [ ] **Step 2: Failing test**

Append inside `JsonSettingsStoreTests`:
```csharp
    [Fact]
    public void ThemeAndPeriod_RoundTrip()
    {
        var path = Path.Combine(Path.GetTempPath(), $"pcw-{Guid.NewGuid():N}.json");
        try
        {
            var store = new JsonSettingsStore(path);
            store.Save(new AppSettings(true, true, true, "en", "terminal", "Year"));
            var loaded = store.Load();
            Assert.Equal("terminal", loaded.Theme);
            Assert.Equal("Year", loaded.Period);
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }
```

- [ ] **Step 3: Run — verify pass**

Run: `dotnet test --filter JsonSettingsStoreTests`
Expected: PASS (after Step 1; the new fields serialize/deserialize).

- [ ] **Step 4: Add Theme/Period to AppController**

In `src/PcWrapped/AppController.cs`:
- Add fields seeded from settings in the constructor (next to `_tracking`/`_autostart`):
```csharp
    private string _theme;
    private string _period;
```
and in the ctor body:
```csharp
        _theme = settings.Theme;
        _period = settings.Period;
```
- Add public accessors:
```csharp
    public string Theme => _theme;
    public string Period => _period;
    public void SetTheme(string id) { _theme = id; SaveSettings(); }
    public void SetPeriod(string period) { _period = period; SaveSettings(); }
```
- Update `SaveSettings` to persist them:
```csharp
    private void SaveSettings()
    {
        var s = _settingsStore.Load();
        _settingsStore.Save(s with
        {
            CountInput = _tracking,
            Autostart = _autostart,
            Theme = _theme,
            Period = _period,
        });
    }
```

- [ ] **Step 5: Build + full suite + commit**

Run: `dotnet build PcWrapped.slnx` (0 errors), `dotnet test PcWrapped.slnx` (all pass).
```bash
git add -A
git commit -m "feat: persist selected theme and period in settings"
```

---

## Task 2: MainWindow — real DPI, restore/save theme+period, empty state

**Files:**
- Modify: `src/PcWrapped/Localization/Loc.cs`
- Modify: `src/PcWrapped/Views/MainWindow.axaml`
- Modify: `src/PcWrapped/Views/MainWindow.axaml.cs`

- [ ] **Step 1: Loc key for empty state**

In `Loc.cs` add to **Ru**: `["dash.empty"] = "Собираю статистику… загляни позже",` and to **En**: `["dash.empty"] = "Collecting your stats… check back later",`.

- [ ] **Step 2: Add empty-state hint + names in MainWindow.axaml**

In the center `Grid` (the one with `RowDefinitions="Auto,Auto,Auto,*"`):
- Give the charts row `Grid` (Grid.Row="1") `x:Name="ChartsRow"`.
- Give the "ТОП ПРИЛОЖЕНИЙ" label `x:Name="LblTopApps"` (already named in localization task — keep).
- Give the apps `ScrollViewer` (Grid.Row="3") `x:Name="AppsScroll"`.
- Add an empty-hint TextBlock spanning rows 1–3, centered, hidden by default:
```xml
      <TextBlock x:Name="EmptyHint" Grid.Row="1" Grid.RowSpan="3"
                 Text="Собираю статистику…" Foreground="{StaticResource MutedBrush}"
                 FontSize="16" HorizontalAlignment="Center" VerticalAlignment="Center"
                 TextWrapping="Wrap" TextAlignment="Center" IsVisible="False"/>
```

- [ ] **Step 3: Real DPI in RefreshAsync**

In `MainWindow.axaml.cs` `RefreshAsync`, change the stats call's `mouseDpi: 96` to `mouseDpi: 96 * RenderScaling` (`RenderScaling` is a `Window`/`TopLevel` double; fallback is 1.0 so the default equals 96).

- [ ] **Step 4: Empty-state toggle in RefreshAsync**

In `RefreshAsync`, after `_current` is built and before/around populating the dashboard, compute and apply visibility:
```csharp
        bool empty = _current.TotalActive == TimeSpan.Zero && _current.TopApps.Count == 0;
        this.FindControl<TextBlock>("EmptyHint")!.Text = Loc.T("dash.empty");
        this.FindControl<TextBlock>("EmptyHint")!.IsVisible = empty;
        this.FindControl<Grid>("ChartsRow")!.IsVisible = !empty;
        this.FindControl<TextBlock>("LblTopApps")!.IsVisible = !empty;
        this.FindControl<ScrollViewer>("AppsScroll")!.IsVisible = !empty;
```

- [ ] **Step 5: Restore + save theme/period via Controller**

In `MainWindow.axaml.cs`:
- Add a private method to apply saved prefs to the UI (called once before the first refresh):
```csharp
    private void ApplyControllerPrefs()
    {
        if (Controller is null) return;
        var theme = CardThemes.All.FirstOrDefault(t => t.Id == Controller.Theme);
        if (theme is not null) Vm.SelectedTheme = theme;
        if (Enum.TryParse<ViewModels.StatsPeriod>(Controller.Period, out var p))
        {
            Vm.SelectedPeriod = p;
            var tabs = this.FindControl<StackPanel>("PeriodTabs")!;
            foreach (var child in tabs.Children)
                if (child is RadioButton rb && rb.Tag as string == p.ToString())
                    rb.IsChecked = true;
        }
    }
```
- In the `Opened` handler, call `ApplyControllerPrefs();` BEFORE the existing `await RefreshAsync();`.
- Persist on change: in `BuildThemeSwatches`, inside the swatch `PointerPressed` handler (after `Vm.SelectedTheme = captured;`), add `Controller?.SetTheme(captured.Id);`. In `RefreshAsync`, after `Vm.SelectedPeriod = CurrentPeriod();`, add `Controller?.SetPeriod(Vm.SelectedPeriod.ToString());`.
- Ensure `using System;` and `using System.Linq;` are present (Linq for FirstOrDefault — already used in the file).

- [ ] **Step 6: Build + test + manual**

Run: `dotnet build PcWrapped.slnx` (0 errors), `dotnet test PcWrapped.slnx` (all pass). Manual: run app; with data it shows dashboard; switch theme/period then restart → same theme/period restored. (To see empty state, the hint shows only when there is zero tracked time in the period.)

- [ ] **Step 7: Commit**

```bash
git add -A
git commit -m "feat: real mouse DPI, empty state, and restore theme/period"
```

---

## Task 3: Copy card to clipboard

**Files:**
- Create: `src/PcWrapped/Native/ClipboardImage.cs`
- Modify: `src/PcWrapped/Localization/Loc.cs`
- Modify: `src/PcWrapped/Views/MainWindow.axaml`
- Modify: `src/PcWrapped/Views/MainWindow.axaml.cs`

- [ ] **Step 1: ClipboardImage helper**

`src/PcWrapped/Native/ClipboardImage.cs`:
```csharp
using System;
using System.IO;
using System.Runtime.InteropServices;

namespace PcWrapped.Native;

/// <summary>Puts a PNG image on the Windows clipboard as CF_BITMAP. No-throw.</summary>
public static class ClipboardImage
{
    private const uint CF_BITMAP = 2;

    [DllImport("user32.dll")] private static extern bool OpenClipboard(IntPtr hWndNewOwner);
    [DllImport("user32.dll")] private static extern bool EmptyClipboard();
    [DllImport("user32.dll")] private static extern IntPtr SetClipboardData(uint uFormat, IntPtr hMem);
    [DllImport("user32.dll")] private static extern bool CloseClipboard();
    [DllImport("gdi32.dll")] private static extern bool DeleteObject(IntPtr hObject);

    public static void SetPng(Stream pngStream)
    {
        try
        {
            using var bmp = new System.Drawing.Bitmap(pngStream);
            IntPtr hBmp = bmp.GetHbitmap();
            bool handedOver = false;
            try
            {
                if (OpenClipboard(IntPtr.Zero))
                {
                    EmptyClipboard();
                    handedOver = SetClipboardData(CF_BITMAP, hBmp) != IntPtr.Zero;
                    CloseClipboard();
                }
            }
            finally
            {
                if (!handedOver) DeleteObject(hBmp); // clipboard owns it once handed over
            }
        }
        catch { /* clipboard may be locked; ignore */ }
    }
}
```

- [ ] **Step 2: Loc key**

In `Loc.cs` add **Ru**: `["rail.copy"] = "Копировать",` and **En**: `["rail.copy"] = "Copy",`.

- [ ] **Step 3: Copy button in rail**

In `MainWindow.axaml`, just after the `ExportBtn` (Поделиться) button, add:
```xml
        <Button x:Name="CopyBtn" Content="Копировать" HorizontalAlignment="Stretch" Margin="0,8,0,0"/>
```

- [ ] **Step 4: Wire copy in code-behind**

In `MainWindow.axaml.cs`:
- Add usings: `using System.IO;` (if missing).
- In the constructor wiring, add: `this.FindControl<Button>("CopyBtn")!.Click += OnCopy;`
- In `ApplyLanguage()`, add: `this.FindControl<Button>("CopyBtn")!.Content = Loc.T("rail.copy");`
- Add the handler:
```csharp
    private void OnCopy(object? sender, RoutedEventArgs e)
    {
        if (_current is null) return;
        using var bmp = CardRenderer.RenderToBitmap(_current, Vm.SelectedTheme, Vm.SelectedSize, CurrentIcons());
        using var ms = new MemoryStream();
        bmp.Save(ms);
        ms.Position = 0;
        PcWrapped.Native.ClipboardImage.SetPng(ms);
    }
```

- [ ] **Step 5: Build + test + commit**

Run: `dotnet build PcWrapped.slnx` (0 errors), `dotnet test PcWrapped.slnx` (all pass). Manual: run app, click «Копировать», paste into an image-capable app (e.g., Paint/chat) — the card appears.
```bash
git add -A
git commit -m "feat: copy card to clipboard as image"
```

---

## Task 4: App + tray icon

**Files:**
- Create: `src/PcWrapped/Assets/app.ico`
- Modify: `src/PcWrapped/PcWrapped.csproj`
- Modify: `src/PcWrapped/App.axaml`
- Modify: `src/PcWrapped/Views/MainWindow.axaml`

- [ ] **Step 1: Generate app.ico**

Ensure the Assets folder exists, then generate a 256×256 gradient glyph icon via PowerShell + System.Drawing. Run (PowerShell tool, single command):
```powershell
$dir = "C:\Users\090\pc-wrapped\src\PcWrapped\Assets"; New-Item -ItemType Directory -Force $dir | Out-Null
Add-Type -AssemblyName System.Drawing
$bmp = New-Object System.Drawing.Bitmap 256,256
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.SmoothingMode = 'AntiAlias'
$rect = New-Object System.Drawing.Rectangle 0,0,256,256
$c1 = [System.Drawing.Color]::FromArgb(123,47,247); $c2 = [System.Drawing.Color]::FromArgb(241,7,163)
$brush = New-Object System.Drawing.Drawing2D.LinearGradientBrush $rect,$c1,$c2,45
$g.FillRectangle($brush,$rect)
$white = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(235,255,255,255))
$g.FillRectangle($white, 60,150,28,56)
$g.FillRectangle($white, 114,100,28,106)
$g.FillRectangle($white, 168,130,28,76)
$g.Dispose()
$hicon = $bmp.GetHicon()
$icon = [System.Drawing.Icon]::FromHandle($hicon)
$fs = [System.IO.File]::Create("$dir\app.ico")
$icon.Save($fs); $fs.Close(); $icon.Dispose(); $bmp.Dispose()
Write-Output "icon written: $((Get-Item "$dir\app.ico").Length) bytes"
```
Expected: prints a non-zero byte size; `src/PcWrapped/Assets/app.ico` exists.

- [ ] **Step 2: csproj — ApplicationIcon + AvaloniaResource**

In `src/PcWrapped/PcWrapped.csproj`, inside the first `<PropertyGroup>` add:
```xml
    <ApplicationIcon>Assets\app.ico</ApplicationIcon>
```
And add an `<ItemGroup>`:
```xml
  <ItemGroup>
    <AvaloniaResource Include="Assets/app.ico" />
  </ItemGroup>
```

- [ ] **Step 3: Tray + window icons**

In `src/PcWrapped/App.axaml`, on the `<TrayIcon ... ToolTipText="PC Wrapped">` add the attribute `Icon="/Assets/app.ico"`.
In `src/PcWrapped/Views/MainWindow.axaml`, on the `<Window ...>` root add `Icon="/Assets/app.ico"`.

- [ ] **Step 4: Build + run + manual**

Run: `dotnet build PcWrapped.slnx` (0 errors). Run the app: taskbar/title-bar icon and tray icon now show the gradient glyph. `dotnet test PcWrapped.slnx` (all pass).

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "feat: app and tray icon"
```

---

## Final verification
- [ ] `dotnet test PcWrapped.slnx` — all pass.
- [ ] `dotnet run --project src/PcWrapped` — taskbar/tray icon shows; «Копировать» puts the card on the clipboard; mouse-km uses screen scaling; empty period shows the hint; theme/period persist across restart.
- [ ] All new commits authored by `090_TYPE`, no `Co-Authored-By` trailers.

## Spec Coverage Notes
- Theme/Period persistence (AppSettings + AppController + restore/save) → Tasks 1, 2.
- Real DPI → Task 2.
- Empty state → Task 2.
- Copy to clipboard → Task 3.
- App/tray icon → Task 4.
- Tests (settings round-trip) → Task 1.
- Out of scope (card-row icons already work, EDID DPI, non-Windows clipboard) → not included.
```
