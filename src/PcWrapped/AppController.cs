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
