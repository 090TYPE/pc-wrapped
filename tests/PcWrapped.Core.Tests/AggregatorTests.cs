using PcWrapped.Core.Aggregation;
using PcWrapped.Core.Categorization;
using PcWrapped.Core.Models;

namespace PcWrapped.Core.Tests;

public class AggregatorTests
{
    private static UsageSample S(string proc, string day, int hour, int seconds) =>
        new(new DateTimeOffset(DateTime.Parse($"{day}T{hour:00}:00:00")), proc, proc, seconds);

    [Fact]
    public void TotalActive_SumsAllDurations()
    {
        var samples = new[] { S("code", "2026-06-09", 10, 60), S("chrome", "2026-06-09", 11, 120) };
        Assert.Equal(TimeSpan.FromSeconds(180), Aggregator.TotalActive(samples));
    }

    [Fact]
    public void TopApps_OrdersByDurationDescending_AndLimits()
    {
        var samples = new[]
        {
            S("code", "2026-06-09", 10, 300),
            S("chrome", "2026-06-09", 11, 100),
            S("code", "2026-06-09", 12, 200),
            S("steam", "2026-06-09", 13, 50),
        };
        var top = Aggregator.TopApps(samples, 2);
        Assert.Equal(2, top.Count);
        Assert.Equal("code", top[0].ProcessName);
        Assert.Equal(TimeSpan.FromSeconds(500), top[0].Duration);
        Assert.Equal("chrome", top[1].ProcessName);
    }

    [Fact]
    public void ByCategory_GroupsDurationsUsingCategorizer()
    {
        var cat = new Categorizer(DefaultRules.Map);
        var samples = new[]
        {
            S("code", "2026-06-09", 10, 100),
            S("chrome", "2026-06-09", 11, 50),
            S("unknownApp", "2026-06-09", 12, 25),
        };
        var byCat = Aggregator.ByCategory(samples, cat);
        Assert.Equal(TimeSpan.FromSeconds(100), byCat[Category.Work]);
        Assert.Equal(TimeSpan.FromSeconds(50), byCat[Category.Browser]);
        Assert.Equal(TimeSpan.FromSeconds(25), byCat[Category.Other]);
    }

    [Fact]
    public void HourlySeconds_BucketsByHourOfDay_Length24()
    {
        var samples = new[] { S("code", "2026-06-09", 10, 60), S("code", "2026-06-09", 10, 30), S("x", "2026-06-09", 22, 90) };
        var hourly = Aggregator.HourlySeconds(samples);
        Assert.Equal(24, hourly.Count);
        Assert.Equal(90, hourly[10]);
        Assert.Equal(90, hourly[22]);
        Assert.Equal(0, hourly[0]);
    }

    [Fact]
    public void PeakHour_ReturnsHourWithMostSeconds()
    {
        var samples = new[] { S("code", "2026-06-09", 10, 60), S("x", "2026-06-09", 22, 200) };
        Assert.Equal(22, Aggregator.PeakHour(samples));
    }

    [Fact]
    public void PeakHour_NoData_ReturnsMinusOne()
    {
        Assert.Equal(-1, Aggregator.PeakHour(Array.Empty<UsageSample>()));
    }

    [Fact]
    public void Streak_CountsConsecutiveDaysEndingToday()
    {
        var today = new DateOnly(2026, 6, 15);
        var activeDays = new[]
        {
            new DateOnly(2026, 6, 15),
            new DateOnly(2026, 6, 14),
            new DateOnly(2026, 6, 13),
            new DateOnly(2026, 6, 11), // gap on the 12th
        };
        Assert.Equal(3, Aggregator.Streak(activeDays, today));
    }

    [Fact]
    public void Streak_NoActivityToday_IsZero()
    {
        var today = new DateOnly(2026, 6, 15);
        var activeDays = new[] { new DateOnly(2026, 6, 14) };
        Assert.Equal(0, Aggregator.Streak(activeDays, today));
    }

    [Fact]
    public void BuildPeriodStats_ComposesAllMetrics()
    {
        var cat = new Categorizer(DefaultRules.Map);
        var samples = new[]
        {
            S("code", "2026-06-09", 10, 300),
            S("chrome", "2026-06-09", 22, 100),
        };
        var stats = Aggregator.BuildPeriodStats(
            from: new DateOnly(2026, 6, 9),
            to: new DateOnly(2026, 6, 9),
            samples: samples,
            categorizer: cat,
            counters: new InputCounters(1000, 200, 96 * 1000),
            activeDays: new[] { new DateOnly(2026, 6, 9) },
            today: new DateOnly(2026, 6, 9),
            topAppLimit: 5,
            mouseDpi: 96);

        Assert.Equal(TimeSpan.FromSeconds(400), stats.TotalActive);
        Assert.Equal("code", stats.TopApps[0].ProcessName);
        Assert.Equal(10, stats.PeakHour);
        Assert.Equal(1, stats.StreakDays);
        Assert.Equal(1000, stats.Keystrokes);
        Assert.Equal(200, stats.Clicks);
        // 96*1000 px @ 96 dpi = 1000 inch = 25.4 m = 0.0254 km
        Assert.Equal(0.0254, stats.MouseKilometers, 6);
        Assert.Equal(TimeSpan.FromSeconds(300), stats.ByCategory[Category.Work]);
    }
}
