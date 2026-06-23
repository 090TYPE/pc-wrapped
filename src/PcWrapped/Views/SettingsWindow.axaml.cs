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
