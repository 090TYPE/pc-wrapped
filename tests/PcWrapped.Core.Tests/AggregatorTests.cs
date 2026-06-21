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
}
