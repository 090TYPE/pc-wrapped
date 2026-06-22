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

    [Fact]
    public async Task RollupOlderThan_CollapsesSamplesPerProcessPerHour()
    {
        var repo = await NewRepoAsync();
        var old = new DateTimeOffset(2026, 1, 1, 10, 5, 0, TimeSpan.Zero);
        await repo.AddSampleAsync(new UsageSample(old, "code", "a", 60));
        await repo.AddSampleAsync(new UsageSample(old.AddMinutes(10), "code", "b", 120));
        await repo.AddSampleAsync(new UsageSample(old.AddMinutes(20), "chrome", "c", 30));

        await repo.RollupOlderThanAsync(new DateOnly(2026, 1, 2));

        var all = await repo.GetSamplesAsync(
            new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 1, 2, 0, 0, 0, TimeSpan.Zero));
        Assert.Equal(2, all.Count); // code (180s) + chrome (30s), collapsed to the hour
        var code = all.First(s => s.ProcessName == "code");
        Assert.Equal(180, code.DurationSeconds);
        Assert.Equal(10, code.Start.Hour); // bucketed to start of hour
        Assert.Equal(0, code.Start.Minute);
    }

    [Fact]
    public async Task AppPaths_UpsertAndGet_RoundTrips()
    {
        var repo = await NewRepoAsync();
        await repo.UpsertAppPathAsync("code", @"C:\apps\code.exe");
        await repo.UpsertAppPathAsync("chrome", @"C:\apps\chrome.exe");

        var map = await repo.GetAppPathsAsync();
        Assert.Equal(@"C:\apps\code.exe", map["code"]);
        Assert.Equal(@"C:\apps\chrome.exe", map["chrome"]);
    }

    [Fact]
    public async Task AppPaths_Upsert_UpdatesExistingPath()
    {
        var repo = await NewRepoAsync();
        await repo.UpsertAppPathAsync("code", @"C:\old\code.exe");
        await repo.UpsertAppPathAsync("code", @"C:\new\code.exe");

        var map = await repo.GetAppPathsAsync();
        Assert.Single(map);
        Assert.Equal(@"C:\new\code.exe", map["code"]);
    }

    [Fact]
    public async Task CategoryOverrides_UpsertAndGet_RoundTrips()
    {
        var repo = await NewRepoAsync();
        await repo.UpsertCategoryOverrideAsync("pycharm64", Category.Work);
        await repo.UpsertCategoryOverrideAsync("spotify", Category.Social);

        var map = await repo.GetCategoryOverridesAsync();
        Assert.Equal(Category.Work, map["pycharm64"]);
        Assert.Equal(Category.Social, map["spotify"]);
    }

    [Fact]
    public async Task CategoryOverrides_Upsert_UpdatesExisting()
    {
        var repo = await NewRepoAsync();
        await repo.UpsertCategoryOverrideAsync("vlc", Category.Social);
        await repo.UpsertCategoryOverrideAsync("vlc", Category.Other);

        var map = await repo.GetCategoryOverridesAsync();
        Assert.Single(map);
        Assert.Equal(Category.Other, map["vlc"]);
    }
}
