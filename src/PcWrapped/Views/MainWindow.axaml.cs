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
    private record SizeOption(string Label, Avalonia.PixelSize Size);

    private PeriodStats? _current;

    public MainWindow()
    {
        InitializeComponent();
        var box = this.FindControl<ComboBox>("ThemeBox")!;
        box.ItemsSource = CardThemes.All;
        box.SelectedIndex = 0;
        box.DisplayMemberBinding = new Avalonia.Data.Binding("DisplayName");

        var sizeBox = this.FindControl<ComboBox>("SizeBox")!;
        sizeBox.ItemsSource = new[]
        {
            new SizeOption("Квадрат 1:1", CardRenderer.Square),
            new SizeOption("Сторис 9:16", CardRenderer.Story),
        };
        sizeBox.SelectedIndex = 0;
        sizeBox.DisplayMemberBinding = new Avalonia.Data.Binding("Label");

        this.FindControl<Button>("RefreshBtn")!.Click += async (_, _) => await RefreshAsync();
        this.FindControl<Button>("ExportBtn")!.Click += OnExport;
        Opened += async (_, _) => await RefreshAsync();
    }

    private MainViewModel Vm => (MainViewModel)DataContext!;

    private async System.Threading.Tasks.Task RefreshAsync()
    {
        var box = this.FindControl<ComboBox>("ThemeBox")!;
        if (box.SelectedItem is CardTheme t) Vm.SelectedTheme = t;

        var sizeBox = this.FindControl<ComboBox>("SizeBox")!;
        if (sizeBox.SelectedItem is SizeOption s) Vm.SelectedSize = s.Size;

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

        var sizeBox = this.FindControl<ComboBox>("SizeBox")!;
        if (sizeBox.SelectedItem is SizeOption s) Vm.SelectedSize = s.Size;

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
