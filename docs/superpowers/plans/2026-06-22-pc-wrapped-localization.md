# PC Wrapped — Localization (RU/EN) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Сделать интерфейс и карточку двуязычными (русский/английский): язык по ОС на первом запуске, ручной переключатель RU/EN в рейле, сохранение выбора.

**Architecture:** Статический `Loc` (словари ru/en по ключам) в app-слое отдаёт строки и хелперы единиц. `AppSettings.Language` хранит выбор; App определяет язык на старте (ОС или сохранённый). UI/карточка/категории/онбординг берут строки из `Loc`; смена языка вызывает `ApplyLanguage()` + refresh.

**Tech Stack:** .NET 8, Avalonia 11.0.10, C#, xUnit.

**Commit author:** ВСЕ коммиты — обычный `git commit -m "..."` (git config user = `090_TYPE`). НИКАКИХ `Co-Authored-By` трейлеров, без `--author`, без упоминания Claude/AI. Solution: `PcWrapped.slnx`. App project БЕЗ ImplicitUsings (явные `using`); Core — ImplicitUsings ON.

---

## File Structure
```
src/PcWrapped/Localization/Loc.cs                # CREATE: AppLanguage + Loc.T/Hours/Days/Parse/Code + ru/en dicts
src/PcWrapped.Core/Settings/AppSettings.cs       # MODIFY: + Language field
src/PcWrapped/App.axaml.cs                       # MODIFY: set Loc.Current on startup; preserve Language on onboarding save
src/PcWrapped/Rendering/CardRenderer.cs          # MODIFY: card strings via Loc
src/PcWrapped/Rendering/CategoryPalette.cs       # MODIFY: Name via Loc
src/PcWrapped/ViewModels/AppRowVm.cs             # MODIFY: time via Loc.Hours
src/PcWrapped/Views/MainWindow.axaml(.cs)        # MODIFY: x:Name labels, RU/EN pills, ApplyLanguage, dynamic via Loc
src/PcWrapped/Views/OnboardingWindow.axaml(.cs)  # MODIFY: strings via Loc
tests/PcWrapped.App.Tests/LocTests.cs            # CREATE
tests/PcWrapped.Core.Tests/JsonSettingsStoreTests.cs # MODIFY: Language round-trip
tests/PcWrapped.App.Tests/CardRendererTests.cs   # MODIFY: render in English
```

---

## Task 1: Loc system (TDD)

**Files:**
- Create: `src/PcWrapped/Localization/Loc.cs`
- Test: `tests/PcWrapped.App.Tests/LocTests.cs`

- [ ] **Step 1: Write failing tests**

`tests/PcWrapped.App.Tests/LocTests.cs`:
```csharp
using System;
using PcWrapped.Localization;
using Xunit;

namespace PcWrapped.App.Tests;

public class LocTests
{
    [Fact]
    public void T_ReturnsStringForCurrentLanguage()
    {
        Loc.Current = AppLanguage.Ru;
        Assert.Equal("Работа", Loc.T("cat.work"));
        Loc.Current = AppLanguage.En;
        Assert.Equal("Work", Loc.T("cat.work"));
    }

    [Fact]
    public void T_MissingKey_ReturnsKey()
    {
        Loc.Current = AppLanguage.En;
        Assert.Equal("nope.nope", Loc.T("nope.nope"));
    }

    [Fact]
    public void Hours_FormatsPerLanguage()
    {
        Loc.Current = AppLanguage.Ru;
        Assert.Equal("2ч 05м", Loc.Hours(TimeSpan.FromMinutes(125)));
        Loc.Current = AppLanguage.En;
        Assert.Equal("2h 05m", Loc.Hours(TimeSpan.FromMinutes(125)));
        Assert.Equal("7m", Loc.Hours(TimeSpan.FromMinutes(7)));
    }

    [Fact]
    public void Days_FormatsPerLanguage()
    {
        Loc.Current = AppLanguage.Ru;
        Assert.Equal("5 дн.", Loc.Days(5));
        Loc.Current = AppLanguage.En;
        Assert.Equal("5d", Loc.Days(5));
    }

    [Fact]
    public void ParseAndCode_RoundTrip()
    {
        Assert.Equal(AppLanguage.En, Loc.Parse("en"));
        Assert.Equal(AppLanguage.Ru, Loc.Parse("ru"));
        Assert.Equal(AppLanguage.Ru, Loc.Parse("xx")); // unknown -> Ru
        Assert.Equal("en", Loc.Code(AppLanguage.En));
        Assert.Equal("ru", Loc.Code(AppLanguage.Ru));
    }
}
```

- [ ] **Step 2: Run — verify fail**

Run: `dotnet test --filter LocTests`
Expected: FAIL (Loc missing).

- [ ] **Step 3: Implement Loc**

`src/PcWrapped/Localization/Loc.cs`:
```csharp
using System;
using System.Collections.Generic;

namespace PcWrapped.Localization;

public enum AppLanguage { Ru, En }

public static class Loc
{
    public static AppLanguage Current { get; set; } = AppLanguage.Ru;

    public static string T(string key)
    {
        var dict = Current == AppLanguage.En ? En : Ru;
        return dict.TryGetValue(key, out var v) ? v : key;
    }

    public static string Hours(TimeSpan t)
    {
        int h = (int)t.TotalHours, m = t.Minutes;
        if (Current == AppLanguage.En) return h >= 1 ? $"{h}h {m:00}m" : $"{m}m";
        return h >= 1 ? $"{h}ч {m:00}м" : $"{m}м";
    }

    public static string Days(int n) => Current == AppLanguage.En ? $"{n}d" : $"{n} дн.";

    public static AppLanguage Parse(string? code) =>
        string.Equals(code, "en", StringComparison.OrdinalIgnoreCase) ? AppLanguage.En : AppLanguage.Ru;

    public static string Code(AppLanguage lang) => lang == AppLanguage.En ? "en" : "ru";

    private static readonly Dictionary<string, string> Ru = new()
    {
        ["tab.today"] = "Сегодня", ["tab.week"] = "Неделя", ["tab.year"] = "Год",
        ["rail.theme"] = "ТЕМА", ["rail.format"] = "ФОРМАТ", ["rail.language"] = "ЯЗЫК",
        ["rail.share"] = "Поделиться ↗", ["rail.streak"] = "СЕРИЯ 🔥",
        ["rail.hourly"] = "АКТИВНОСТЬ ПО ЧАСАМ", ["rail.topapps"] = "ТОП ПРИЛОЖЕНИЙ",
        ["rail.preview"] = "ПРЕВЬЮ", ["status.ready"] = "Готово",
        ["period.day"] = "ТВОЙ ДЕНЬ ЗА ПК", ["period.week"] = "ТВОЯ НЕДЕЛЯ ЗА ПК",
        ["period.year"] = "ТВОЙ ГОД ЗА ПК",
        ["card.mouse"] = "🖱️ Мышь проехала", ["card.keys"] = "⌨️ Нажатий",
        ["card.peak"] = "🔥 Пик", ["card.streak"] = "📅 Серия",
        ["unit.km"] = "км",
        ["cat.work"] = "Работа", ["cat.games"] = "Игры", ["cat.social"] = "Соцсети",
        ["cat.browser"] = "Браузер", ["cat.other"] = "Прочее",
        ["onb.title"] = "Добро пожаловать в PC Wrapped",
        ["onb.privacyTitle"] = "Приватность прежде всего",
        ["onb.privacyBody"] = "PC Wrapped считает время в приложениях и количество нажатий/кликов/движений мыши, чтобы строить красивые карточки. Все данные хранятся только на этом компьютере и никогда не отправляются в сеть. Содержимое набранного текста не сохраняется — только счётчики.",
        ["onb.vanity"] = "Считать нажатия клавиш и движение мыши",
        ["onb.autostart"] = "Запускать при старте Windows",
        ["onb.start"] = "Начать",
    };

    private static readonly Dictionary<string, string> En = new()
    {
        ["tab.today"] = "Today", ["tab.week"] = "Week", ["tab.year"] = "Year",
        ["rail.theme"] = "THEME", ["rail.format"] = "FORMAT", ["rail.language"] = "LANGUAGE",
        ["rail.share"] = "Share ↗", ["rail.streak"] = "STREAK 🔥",
        ["rail.hourly"] = "ACTIVITY BY HOUR", ["rail.topapps"] = "TOP APPS",
        ["rail.preview"] = "PREVIEW", ["status.ready"] = "Done",
        ["period.day"] = "YOUR DAY ON PC", ["period.week"] = "YOUR WEEK ON PC",
        ["period.year"] = "YOUR YEAR ON PC",
        ["card.mouse"] = "🖱️ Mouse traveled", ["card.keys"] = "⌨️ Keystrokes",
        ["card.peak"] = "🔥 Peak", ["card.streak"] = "📅 Streak",
        ["unit.km"] = "km",
        ["cat.work"] = "Work", ["cat.games"] = "Games", ["cat.social"] = "Social",
        ["cat.browser"] = "Browser", ["cat.other"] = "Other",
        ["onb.title"] = "Welcome to PC Wrapped",
        ["onb.privacyTitle"] = "Privacy first",
        ["onb.privacyBody"] = "PC Wrapped counts time in apps and the number of keystrokes/clicks/mouse movement to build beautiful cards. All data stays on this computer and is never sent anywhere. The text you type is never stored — only counts.",
        ["onb.vanity"] = "Count keystrokes and mouse movement",
        ["onb.autostart"] = "Launch on Windows startup",
        ["onb.start"] = "Start",
    };
}
```

- [ ] **Step 4: Run — verify pass**

Run: `dotnet test --filter LocTests`
Expected: PASS (5). After this file exists, leave `Loc.Current` default Ru (tests set it explicitly).

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "feat: add Loc localization store (RU/EN)"
```

---

## Task 2: AppSettings.Language + startup language

**Files:**
- Modify: `src/PcWrapped.Core/Settings/AppSettings.cs`
- Modify: `src/PcWrapped/App.axaml.cs`
- Test: `tests/PcWrapped.Core.Tests/JsonSettingsStoreTests.cs`

- [ ] **Step 1: Add Language to AppSettings**

Replace `src/PcWrapped.Core/Settings/AppSettings.cs` with:
```csharp
namespace PcWrapped.Core.Settings;

public sealed record AppSettings(bool HasOnboarded, bool CountInput, bool Autostart, string Language = "ru")
{
    public static AppSettings Default => new(false, true, true, "ru");
}
```
(net8.0 System.Text.Json honors the `Language = "ru"` default when the field is absent in an old settings.json.)

- [ ] **Step 2: Write failing test**

Append inside `JsonSettingsStoreTests`:
```csharp
    [Fact]
    public void Language_RoundTrips()
    {
        var path = Path.Combine(Path.GetTempPath(), $"pcw-{Guid.NewGuid():N}.json");
        try
        {
            var store = new JsonSettingsStore(path);
            store.Save(new AppSettings(true, true, false, "en"));
            Assert.Equal("en", store.Load().Language);
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }
```

- [ ] **Step 3: Run — verify fail**

Run: `dotnet test --filter JsonSettingsStoreTests`
Expected: FAIL (Language member missing / not persisted) — fails to compile until Step 1 done, then passes.

- [ ] **Step 4: Set language on startup in App.axaml.cs**

In `src/PcWrapped/App.axaml.cs`:
1. Add usings:
```csharp
using System.Globalization;
using PcWrapped.Localization;
```
2. Immediately AFTER `var settings = settingsStore.Load();` add:
```csharp
        Loc.Current = settings.HasOnboarded
            ? Loc.Parse(settings.Language)
            : (CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ru"
                ? AppLanguage.Ru : AppLanguage.En);
```
3. In the onboarding `Closed` handler, change the save line to preserve the chosen language:
```csharp
                    settingsStore.Save(new AppSettings(true, countInput, autostart, Loc.Code(Loc.Current)));
```

- [ ] **Step 5: Build + test**

Run: `dotnet build PcWrapped.slnx` (0 errors; taskkill PcWrapped.exe if locked) then `dotnet test PcWrapped.slnx` (all pass).

- [ ] **Step 6: Commit**

```bash
git add -A
git commit -m "feat: persist language and detect it from OS on first run"
```

---

## Task 3: Localize card + categories + app-row time

**Files:**
- Modify: `src/PcWrapped/Rendering/CategoryPalette.cs`
- Modify: `src/PcWrapped/Rendering/CardRenderer.cs`
- Modify: `src/PcWrapped/ViewModels/AppRowVm.cs`
- Test: `tests/PcWrapped.App.Tests/CardRendererTests.cs`

- [ ] **Step 1: Localize CategoryPalette.Name**

In `src/PcWrapped/Rendering/CategoryPalette.cs` add `using PcWrapped.Localization;` and replace the `Name` method body with:
```csharp
    public static string Name(Category c) => c switch
    {
        Category.Work => Loc.T("cat.work"),
        Category.Games => Loc.T("cat.games"),
        Category.Social => Loc.T("cat.social"),
        Category.Browser => Loc.T("cat.browser"),
        _ => Loc.T("cat.other"),
    };
```

- [ ] **Step 2: Localize CardRenderer strings**

In `src/PcWrapped/Rendering/CardRenderer.cs` add `using PcWrapped.Localization;`. Make these edits:
- Replace the local `Header` function with:
```csharp
        static string Header(PeriodStats s)
        {
            int days = (s.To.DayNumber - s.From.DayNumber);
            if (days <= 0) return Loc.T("period.day");
            if (days <= 31) return Loc.T("period.week");
            return Loc.T("period.year");
        }
```
- Replace the four vanity `Row(...)` lines with:
```csharp
        Row(Loc.T("card.mouse"), $"{stats.MouseKilometers:0.0} {Loc.T("unit.km")}");
        Row(Loc.T("card.keys"), $"{stats.Keystrokes:N0}");
        if (stats.PeakHour >= 0) Row(Loc.T("card.peak"), $"{stats.PeakHour:00}:00");
        Row(Loc.T("card.streak"), Loc.Days(stats.StreakDays));
```
- Replace the private `FormatHours` usages: change the two calls that format hours (`FormatHours(stats.TotalActive)` and `FormatHours(app.Duration)`) to `Loc.Hours(...)`, and DELETE the now-unused private `FormatHours` method.

- [ ] **Step 3: Localize AppRowVm time**

In `src/PcWrapped/ViewModels/AppRowVm.cs` add `using PcWrapped.Localization;`, change the time text to `Loc.Hours(a.Duration)`, and delete the private `FormatHours` method:
```csharp
            return new AppRowVm(a.ProcessName, Loc.Hours(a.Duration),
                a.Duration.TotalSeconds / max, p, categorizer.Categorize(a.ProcessName));
```
Also make `tests/PcWrapped.App.Tests/AppRowVmTests.cs` deterministic against the global `Loc.Current`: add `using PcWrapped.Localization;` and insert `Loc.Current = AppLanguage.Ru;` as the FIRST line of both test methods (`FromStats_ComputesFractionTimeAndPath` and `FromStats_EmptyTopApps_ReturnsEmpty`), so the "2ч 00м" assertion never depends on test order.

- [ ] **Step 4: Add English render test**

In `tests/PcWrapped.App.Tests/CardRendererTests.cs` add (ensure `using PcWrapped.Localization;`):
```csharp
    [AvaloniaFact]
    public void RenderToPng_English_ProducesValidPng()
    {
        Loc.Current = AppLanguage.En;
        try
        {
            var dir = Path.Combine(Path.GetTempPath(), "pcwrapped-test");
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, "card-en.png");
            CardRenderer.RenderToPng(SampleWithCharts(), CardThemes.Gradient, CardRenderer.Square, path);
            Assert.True(File.Exists(path));
            Assert.True(new FileInfo(path).Length > 1000);
        }
        finally { Loc.Current = AppLanguage.Ru; }
    }
```
(Uses existing `SampleWithCharts()` helper.)

- [ ] **Step 5: Build + test**

Run: `dotnet build PcWrapped.slnx` (0 errors) then `dotnet test PcWrapped.slnx` (all pass — AppRowVmTests stay green under default Ru).

- [ ] **Step 6: Commit**

```bash
git add -A
git commit -m "feat: localize card, category names, and app-row time"
```

---

## Task 4: Localize main window + onboarding + RU/EN switch

**Files:**
- Modify: `src/PcWrapped/Views/MainWindow.axaml`
- Modify: `src/PcWrapped/Views/MainWindow.axaml.cs`
- Modify: `src/PcWrapped/Views/OnboardingWindow.axaml`
- Modify: `src/PcWrapped/Views/OnboardingWindow.axaml.cs`

- [ ] **Step 1: Name the static labels + add RU/EN pills (MainWindow.axaml)**

Add `x:Name` to the static labels and period tabs so code-behind can set their text. In the rail and center, make these exact attribute additions/edits (keep all other attributes):
- Period tabs: `<RadioButton ... Content="Сегодня" Tag="Today"/>` → add `x:Name="TabToday"`; `Content="Неделя"` → add `x:Name="TabWeek"`; `Content="Год"` → add `x:Name="TabYear"`.
- `<TextBlock Classes="lab" Text="ТЕМА" .../>` → add `x:Name="LblTheme"`.
- `<TextBlock Classes="lab" Text="ФОРМАТ" .../>` → add `x:Name="LblFormat"`.
- `<TextBlock Classes="lab" Text="СЕРИЯ 🔥" .../>` → add `x:Name="LblStreakCaption"`.
- `<TextBlock Classes="lab" Text="АКТИВНОСТЬ ПО ЧАСАМ"/>` → add `x:Name="LblHourly"`.
- `<TextBlock Grid.Row="2" Classes="lab" Text="ТОП ПРИЛОЖЕНИЙ" .../>` → add `x:Name="LblTopApps"`.
- `<TextBlock Classes="lab" Text="ПРЕВЬЮ"/>` → add `x:Name="LblPreview"`.

Add a LANGUAGE section to the rail: insert after the FORMAT pills `StackPanel` (the one with SizeSquare/SizeStory), before the ExportBtn:
```xml
        <TextBlock x:Name="LblLanguage" Classes="lab" Text="ЯЗЫК" Margin="0,14,0,6"/>
        <StackPanel Orientation="Horizontal" Spacing="6">
          <RadioButton x:Name="LangRu" GroupName="lang" Classes="pill" Content="RU" IsChecked="True"/>
          <RadioButton x:Name="LangEn" GroupName="lang" Classes="pill" Content="EN"/>
        </StackPanel>
```

- [ ] **Step 2: ApplyLanguage + wire switch (MainWindow.axaml.cs)**

Add usings: `using PcWrapped.Localization;` (System.Linq, PcWrapped.Core.Models already present).

In the constructor, after the existing wiring (before `Opened += ...`), set the language radios to reflect `Loc.Current` and wire them, then apply language:
```csharp
        var langRu = this.FindControl<RadioButton>("LangRu")!;
        var langEn = this.FindControl<RadioButton>("LangEn")!;
        langRu.IsChecked = Loc.Current == AppLanguage.Ru;
        langEn.IsChecked = Loc.Current == AppLanguage.En;
        langRu.IsCheckedChanged += async (_, _) => { if (langRu.IsChecked == true) await SetLanguage(AppLanguage.Ru); };
        langEn.IsCheckedChanged += async (_, _) => { if (langEn.IsChecked == true) await SetLanguage(AppLanguage.En); };
        ApplyLanguage();
```

Add these methods to the class:
```csharp
    private async System.Threading.Tasks.Task SetLanguage(AppLanguage lang)
    {
        if (Loc.Current == lang) return;
        Loc.Current = lang;
        PcWrapped.Localization.LanguagePersistence.Save(lang);
        ApplyLanguage();
        await RefreshAsync();
    }

    private void ApplyLanguage()
    {
        this.FindControl<RadioButton>("TabToday")!.Content = Loc.T("tab.today");
        this.FindControl<RadioButton>("TabWeek")!.Content = Loc.T("tab.week");
        this.FindControl<RadioButton>("TabYear")!.Content = Loc.T("tab.year");
        this.FindControl<TextBlock>("LblTheme")!.Text = Loc.T("rail.theme");
        this.FindControl<TextBlock>("LblFormat")!.Text = Loc.T("rail.format");
        this.FindControl<TextBlock>("LblLanguage")!.Text = Loc.T("rail.language");
        this.FindControl<TextBlock>("LblStreakCaption")!.Text = Loc.T("rail.streak");
        this.FindControl<TextBlock>("LblHourly")!.Text = Loc.T("rail.hourly");
        this.FindControl<TextBlock>("LblTopApps")!.Text = Loc.T("rail.topapps");
        this.FindControl<TextBlock>("LblPreview")!.Text = Loc.T("rail.preview");
        this.FindControl<Button>("ExportBtn")!.Content = Loc.T("rail.share");
    }
```

In `RefreshAsync`, localize the dynamic strings — replace the period-label / total / streak / status lines with:
```csharp
        this.FindControl<TextBlock>("PeriodLabel")!.Text = Loc.T(Vm.SelectedPeriod switch
        {
            StatsPeriod.Today => "period.day",
            StatsPeriod.Year => "period.year",
            _ => "period.week",
        });
        this.FindControl<TextBlock>("TotalText")!.Text = Loc.Hours(_current.TotalActive);
        this.FindControl<TextBlock>("StreakText")!.Text = Loc.Days(_current.StreakDays);
```
and change the final status line to:
```csharp
        this.FindControl<TextBlock>("Status")!.Text = Loc.T("status.ready");
```
(Delete the old `PeriodLabelText` static method if present, and remove its usage.)

- [ ] **Step 3: Language persistence helper**

So the View can save the language without a repo, add a tiny helper that writes the same settings.json. Create `src/PcWrapped/Localization/LanguagePersistence.cs`:
```csharp
using System;
using System.IO;
using PcWrapped.Core.Settings;

namespace PcWrapped.Localization;

/// <summary>Persists only the language into the app's settings.json (preserving other fields).</summary>
public static class LanguagePersistence
{
    private static string Path_ => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PcWrapped", "settings.json");

    public static void Save(AppLanguage lang)
    {
        var store = new JsonSettingsStore(Path_);
        var s = store.Load();
        store.Save(s with { Language = Loc.Code(lang) });
    }
}
```

- [ ] **Step 4: Localize OnboardingWindow**

In `OnboardingWindow.axaml`, give the two text blocks names and drop the literal Russian (we set text in code-behind): change line `<TextBlock Text="Приватность прежде всего" .../>` to add `x:Name="PrivacyTitle"`, and the wrapping body `<TextBlock TextWrapping="Wrap" Text="...">` to `x:Name="PrivacyBody"` (keep `TextWrapping="Wrap"`). Keep `VanityToggle`/`AutostartToggle`/`StartBtn` names.

In `OnboardingWindow.axaml.cs`, add `using PcWrapped.Localization;` and in the constructor (after `InitializeComponent();`) set localized strings:
```csharp
        Title = Loc.T("onb.title");
        this.FindControl<TextBlock>("PrivacyTitle")!.Text = Loc.T("onb.privacyTitle");
        this.FindControl<TextBlock>("PrivacyBody")!.Text = Loc.T("onb.privacyBody");
        this.FindControl<CheckBox>("VanityToggle")!.Content = Loc.T("onb.vanity");
        this.FindControl<CheckBox>("AutostartToggle")!.Content = Loc.T("onb.autostart");
        this.FindControl<Button>("StartBtn")!.Content = Loc.T("onb.start");
```
(Ensure `using Avalonia.Controls;` is present — it is.)

- [ ] **Step 5: Build**

Run: `dotnet build PcWrapped.slnx`
Expected: 0 errors (taskkill PcWrapped.exe if locked). If `RadioButton.IsCheckedChanged` event name differs in Avalonia 11, it does not — it's used elsewhere in this file already.

- [ ] **Step 6: Tests**

Run: `dotnet test PcWrapped.slnx`
Expected: all pass.

- [ ] **Step 7: Manual verification**

Run the app. UI is in the OS language (or last choice). Click EN → all rail labels, period title, "Top apps", streak, preview, export button switch to English instantly; donut legend categories switch; export a card → card text is English. Switch back to RU. Restart → last language persists. Close the app.

- [ ] **Step 8: Commit**

```bash
git add -A
git commit -m "feat: localize main window and onboarding with RU/EN switch"
```

---

## Final verification
- [ ] `dotnet test PcWrapped.slnx` — all pass.
- [ ] `dotnet run --project src/PcWrapped` — language follows OS on first run; RU/EN switch flips all UI + card text live and persists across restart.
- [ ] All new commits authored by `090_TYPE`, no `Co-Authored-By` trailers.

## Spec Coverage Notes
- Loc store + units helpers → Task 1.
- AppSettings.Language + OS detection on first run + persistence → Task 2, plus LanguagePersistence (Task 4) for the in-UI toggle.
- Card / category / app-row strings → Task 3.
- Rail labels, period titles, RU/EN switch, onboarding → Task 4.
- Tests (Loc, settings round-trip, English card render) → Tasks 1–3.
- Out of scope (other languages, number/date formats) → not included.
```
