using PcWrapped.Core.Models;
using PcWrapped.Core.Storage;

namespace PcWrapped.Core.Tests;

public class SqliteStatsRepositoryTests
{
    // Each test uses a unique in-memory shared DB so the connection stays alive.
    private static async Task<SqliteStatsRepository> NewRepoAsync()
    {
        var cs = $"Data Source=file:{Guid.NewGuid():N}?mode=memory&cache=shared";
        var repo = new SqliteStatsRepository(cs);
        await repo.InitializeAsync();
        return repo;
    }

    [Fact]
    public async Task AddAndGetSamples_RoundTripsWithinRange()
    {
        var repo = await NewRepoAsync();
        var t = new DateTimeOffset(2026, 6, 9, 10, 0, 0, TimeSpan.Zero);
        await repo.AddSampleAsync(new UsageSample(t, "code", "main.cs", 60));
        await repo.AddSampleAsync(new UsageSample(t.AddDays(5), "chrome", "tab", 30));

        var inRange = await repo.GetSamplesAsync(t.AddHours(-1), t.AddHours(1));
        Assert.Single(inRange);
        Assert.Equal("code", inRange[0].ProcessName);
        Assert.Equal(60, inRange[0].DurationSeconds);
    }

    [Fact]
    public async Task InputCounters_AccumulateAcrossCallsAndDays()
    {
        var repo = await NewRepoAsync();
        var d = new DateOnly(2026, 6, 9);
        await repo.AddInputCountersAsync(d, new InputCounters(10, 2, 100));
        await repo.AddInputCountersAsync(d, new InputCounters(5, 1, 50));
        await repo.AddInputCountersAsync(d.AddDays(1), new InputCounters(7, 3, 25));

        var total = await repo.GetInputCountersAsync(d, d.AddDays(1));
        Assert.Equal(22, total.Keystrokes);
        Assert.Equal(6, total.Clicks);
        Assert.Equal(175, total.MousePixels);
    }

    [Fact]
    public async Task GetActiveDays_ReturnsDistinctSampleDays()
    {
        var repo = await NewRepoAsync();
        var t = new DateTimeOffset(2026, 6, 9, 10, 0, 0, TimeSpan.Zero);
        await repo.AddSampleAsync(new UsageSample(t, "code", "x", 60));
        await repo.AddSampleAsync(new UsageSample(t.AddHours(2), "code", "x", 60));
        await repo.AddSampleAsync(new UsageSample(t.AddDays(1), "code", "x", 60));

        var days = await repo.GetActiveDaysAsync();
        Assert.Equal(2, days.Count);
        Assert.Contains(new DateOnly(2026, 6, 9), days);
        Assert.Contains(new DateOnly(2026, 6, 10), days);
    }
}
