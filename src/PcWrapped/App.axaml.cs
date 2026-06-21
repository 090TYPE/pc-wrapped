using System;
using System.IO;
using System.Timers;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using PcWrapped.Core.Settings;
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

        var settingsStore = new JsonSettingsStore(Path.Combine(dir, "settings.json"));
        var settings = settingsStore.Load();

        _repo = new SqliteStatsRepository($"Data Source={Path.Combine(dir, "stats.db")}");
        await _repo.InitializeAsync();
        await _repo.RollupOlderThanAsync(DateOnly.FromDateTime(DateTime.Now).AddDays(-30));

        // Always create the input source and tracker so the timer drains counters
        // regardless of whether hooks are running. Hooks are only started if opted in.
        _input = new Win32InputCounterSource();
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

            if (!settings.HasOnboarded)
            {
                var onboarding = new OnboardingWindow();
                onboarding.Closed += (_, _) =>
                {
                    var countInput = onboarding.CountInput;
                    var autostart = onboarding.Autostart;
                    settingsStore.Save(new AppSettings(true, countInput, autostart));
                    try { AutostartManager.SetEnabled(autostart, Environment.ProcessPath!); }
                    catch { /* registry may be unavailable */ }
                    if (countInput) _input!.Start();
                    ShowMainWindow(desktop);
                };
                desktop.MainWindow = onboarding;
                onboarding.Show();
            }
            else
            {
                if (settings.CountInput) _input.Start();
                ShowMainWindow(desktop);
            }
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void ShowMainWindow(IClassicDesktopStyleApplicationLifetime desktop)
    {
        var vm = new MainViewModel(_repo!);
        var window = new MainWindow { DataContext = vm };
        desktop.MainWindow = window;
        window.Show();
    }

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
}
