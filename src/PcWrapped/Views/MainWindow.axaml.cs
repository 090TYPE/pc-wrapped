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
using PcWrapped.Rendering;
using PcWrapped.ViewModels;

namespace PcWrapped.Views;

public partial class MainWindow : Window
{
    private PeriodStats? _current;
    private System.Collections.Generic.IReadOnlyDictionary<string, string>? _currentPaths;

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
        _currentPaths = paths;

        this.FindControl<TextBlock>("PeriodLabel")!.Text = PeriodLabelText(Vm.SelectedPeriod);
        this.FindControl<TextBlock>("TotalText")!.Text =
            $"{(int)_current.TotalActive.TotalHours}ч {_current.TotalActive.Minutes:00}м";
        this.FindControl<TextBlock>("StreakText")!.Text = $"{_current.StreakDays} дн.";
        this.FindControl<ItemsControl>("AppList")!.ItemsSource = AppRowVm.FromStats(_current, paths);

        UpdateCharts();
        RenderPreview();
        this.FindControl<TextBlock>("Status")!.Text = "Готово";
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
