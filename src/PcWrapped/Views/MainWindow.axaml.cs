using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using PcWrapped.Controls;
using PcWrapped.Core.Aggregation;
using PcWrapped.Core.Models;
using PcWrapped.Localization;
using PcWrapped.Rendering;
using PcWrapped.ViewModels;

namespace PcWrapped.Views;

public partial class MainWindow : Window
{
    private PeriodStats? _current;
    private System.Collections.Generic.IReadOnlyDictionary<string, string>? _currentPaths;

    public PcWrapped.AppController? Controller { get; set; }

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

        var langRu = this.FindControl<RadioButton>("LangRu")!;
        var langEn = this.FindControl<RadioButton>("LangEn")!;
        langRu.IsChecked = Loc.Current == AppLanguage.Ru;
        langEn.IsChecked = Loc.Current == AppLanguage.En;
        langRu.IsCheckedChanged += async (_, _) => { if (langRu.IsChecked == true) await SetLanguage(AppLanguage.Ru); };
        langEn.IsCheckedChanged += async (_, _) => { if (langEn.IsChecked == true) await SetLanguage(AppLanguage.En); };
        ApplyLanguage();

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

    private async Task RefreshAsync()
    {
        Vm.SelectedPeriod = CurrentPeriod();
        Vm.SelectedSize = this.FindControl<RadioButton>("SizeStory")!.IsChecked == true
            ? CardRenderer.Story : CardRenderer.Square;

        _current = await Vm.BuildStatsAsync(DateOnly.FromDateTime(DateTime.Now), mouseDpi: 96);
        var paths = await Vm.GetAppPathsAsync();
        _currentPaths = paths;

        this.FindControl<TextBlock>("PeriodLabel")!.Text = Loc.T(Vm.SelectedPeriod switch
        {
            StatsPeriod.Today => "period.day",
            StatsPeriod.Year => "period.year",
            _ => "period.week",
        });
        this.FindControl<TextBlock>("TotalText")!.Text = Loc.Hours(_current.TotalActive);
        this.FindControl<TextBlock>("StreakText")!.Text = Loc.Days(_current.StreakDays);
        this.FindControl<ItemsControl>("AppList")!.ItemsSource = AppRowVm.FromStats(_current, paths, Vm.Categorizer);

        UpdateCharts();
        RenderPreview();
        this.FindControl<TextBlock>("Status")!.Text = Loc.T("status.ready");
    }

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

    private void RenderPreview()
    {
        if (_current is null) return;
        var bmp = CardRenderer.RenderToBitmap(_current, Vm.SelectedTheme, Vm.SelectedSize, CurrentIcons());
        this.FindControl<Image>("PreviewImage")!.Source = bmp;
    }

    private void OnCategoryMenuOpening(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        // Avalonia 11.0.10 MenuItem has no ToggleType/IsChecked; show the current
        // category with a check glyph in the MenuItem.Icon instead.
        if (sender is ContextMenu cm && cm.DataContext is AppRowVm row)
            foreach (var item in cm.Items.OfType<MenuItem>())
            {
                var tag = item.Tag as string;
                item.Header = Loc.T("cat." + (tag ?? "other").ToLowerInvariant());
                item.Icon = tag == row.Category.ToString() ? new TextBlock { Text = "✓" } : null;
            }
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
        await Vm.ExportAsync(_current, file.Path.LocalPath, CurrentIcons());
    }
}
