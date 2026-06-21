using PcWrapped.Core.Models;
using PcWrapped.Core.Storage;
using PcWrapped.Core.Tracking;

namespace PcWrapped.Core.Tests;

public class ActivityTrackerTests
{
    private sealed class FakeForeground : IForegroundWindowSource
    {
        public ForegroundInfo? Current;
        public int Idle;
        public ForegroundInfo? GetForeground() => Current;
        public int GetIdleSeconds() => Idle;
    }

    private sealed class FakeInput : IInputCounterSource
    {
        public InputCounters Pending = InputCounters.Zero;
        public InputCounters DrainCounters()
        {
            var c = Pending; Pending = InputCounters.Zero; return c;
        }
    }

    private static async Task<SqliteStatsRepository> NewRepoAsync()
    {
        var repo = new SqliteStatsRepository(
            $"Data Source=file:{Guid.NewGuid():N}?mode=memory&cache=shared");
        await repo.InitializeAsync();
        return repo;
    }

    [Fact]
    public async Task Tick_ActiveWindow_RecordsSampleWithIntervalSeconds()
    {
        var repo = await NewRepoAsync();
        var fg = new FakeForeground { Current = new ForegroundInfo("code", "main.cs"), Idle = 0 };
        var input = new FakeInput();
        var now = new DateTimeOffset(2026, 6, 9, 10, 0, 0, TimeSpan.Zero);
        var tracker = new ActivityTracker(repo, fg, input, intervalSeconds: 2, idleThresholdSeconds: 60);

        await tracker.TickAsync(now);

        var samples = await repo.GetSamplesAsync(now.AddMinutes(-1), now.AddMinutes(1));
        Assert.Single(samples);
        Assert.Equal("code", samples[0].ProcessName);
        Assert.Equal(2, samples[0].DurationSeconds);
    }

    [Fact]
    public async Task Tick_WhenIdleBeyondThreshold_RecordsNoSample()
    {
        var repo = await NewRepoAsync();
        var fg = new FakeForeground { Current = new ForegroundInfo("code", "x"), Idle = 120 };
        var tracker = new ActivityTracker(repo, fg, new FakeInput(), 2, idleThresholdSeconds: 60);

        await tracker.TickAsync(new DateTimeOffset(2026, 6, 9, 10, 0, 0, TimeSpan.Zero));

        var samples = await repo.GetSamplesAsync(
            DateTimeOffset.MinValue.AddDays(1), DateTimeOffset.MaxValue.AddDays(-1));
        Assert.Empty(samples);
    }

    [Fact]
    public async Task Tick_DrainsInputCountersIntoRepository()
    {
        var repo = await NewRepoAsync();
        var fg = new FakeForeground { Current = new ForegroundInfo("code", "x"), Idle = 0 };
        var input = new FakeInput { Pending = new InputCounters(5, 2, 40) };
        var now = new DateTimeOffset(2026, 6, 9, 10, 0, 0, TimeSpan.Zero);
        var tracker = new ActivityTracker(repo, fg, input, 2, 60);

        await tracker.TickAsync(now);

        var counters = await repo.GetInputCountersAsync(
            new DateOnly(2026, 6, 9), new DateOnly(2026, 6, 9));
        Assert.Equal(5, counters.Keystrokes);
        Assert.Equal(2, counters.Clicks);
        Assert.Equal(40, counters.MousePixels);
    }
}
