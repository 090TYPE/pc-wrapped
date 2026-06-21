using PcWrapped.Core.Storage;

namespace PcWrapped.Core.Tracking;

public sealed class ActivityTracker
{
    private readonly IStatsRepository _repo;
    private readonly IForegroundWindowSource _foreground;
    private readonly IInputCounterSource _input;
    private readonly int _intervalSeconds;
    private readonly int _idleThresholdSeconds;

    public ActivityTracker(
        IStatsRepository repo,
        IForegroundWindowSource foreground,
        IInputCounterSource input,
        int intervalSeconds,
        int idleThresholdSeconds)
    {
        _repo = repo;
        _foreground = foreground;
        _input = input;
        _intervalSeconds = intervalSeconds;
        _idleThresholdSeconds = idleThresholdSeconds;
    }

    /// <summary>Один тик опроса. Вызывается по таймеру в приложении.</summary>
    public async Task TickAsync(DateTimeOffset now)
    {
        // Счётчики ввода забираем всегда (даже если окно неизвестно).
        var counters = _input.DrainCounters();
        if (counters.Keystrokes != 0 || counters.Clicks != 0 || counters.MousePixels != 0)
            await _repo.AddInputCountersAsync(DateOnly.FromDateTime(now.DateTime), counters);

        if (_foreground.GetIdleSeconds() >= _idleThresholdSeconds)
            return; // пользователь отошёл — время не накручиваем

        var fg = _foreground.GetForeground();
        if (fg is null) return;

        await _repo.AddSampleAsync(new Models.UsageSample(
            now, fg.Value.ProcessName, fg.Value.WindowTitle, _intervalSeconds));

        if (!string.IsNullOrEmpty(fg.Value.ExecutablePath))
            await _repo.UpsertAppPathAsync(fg.Value.ProcessName, fg.Value.ExecutablePath!);
    }
}
