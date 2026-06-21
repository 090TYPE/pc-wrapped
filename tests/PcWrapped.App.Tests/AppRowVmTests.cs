using System;
using System.Collections.Generic;
using PcWrapped.Core.Models;
using PcWrapped.ViewModels;
using Xunit;

namespace PcWrapped.App.Tests;

public class AppRowVmTests
{
    private static PeriodStats Stats(params AppUsage[] top) => new(
        new DateOnly(2026, 6, 9), new DateOnly(2026, 6, 15),
        TimeSpan.FromHours(3), top,
        new Dictionary<Category, TimeSpan>(), 22, new int[24], 5, 0, 0, 0);

    [Fact]
    public void FromStats_ComputesFractionTimeAndPath()
    {
        var stats = Stats(
            new AppUsage("code", TimeSpan.FromHours(2)),
            new AppUsage("chrome", TimeSpan.FromHours(1)));
        var rows = AppRowVm.FromStats(stats,
            new Dictionary<string, string> { ["code"] = @"C:\code.exe" });

        Assert.Equal(2, rows.Count);
        Assert.Equal("code", rows[0].Name);
        Assert.Equal(1.0, rows[0].Fraction, 3);
        Assert.Equal(0.5, rows[1].Fraction, 3);
        Assert.Equal("2ч 00м", rows[0].TimeText);
        Assert.Equal(@"C:\code.exe", rows[0].ExecutablePath);
        Assert.Null(rows[1].ExecutablePath);
    }

    [Fact]
    public void FromStats_EmptyTopApps_ReturnsEmpty()
    {
        Assert.Empty(AppRowVm.FromStats(Stats(), new Dictionary<string, string>()));
    }
}
