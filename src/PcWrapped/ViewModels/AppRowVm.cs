using System;
using System.Collections.Generic;
using System.Linq;
using PcWrapped.Core.Models;

namespace PcWrapped.ViewModels;

public sealed record AppRowVm(string Name, string TimeText, double Fraction, string? ExecutablePath)
{
    public static IReadOnlyList<AppRowVm> FromStats(
        PeriodStats stats, IReadOnlyDictionary<string, string> paths)
    {
        if (stats.TopApps.Count == 0) return Array.Empty<AppRowVm>();
        double max = stats.TopApps.Max(a => a.Duration.TotalSeconds);
        if (max <= 0) max = 1;
        return stats.TopApps.Select(a =>
        {
            paths.TryGetValue(a.ProcessName, out var p);
            return new AppRowVm(a.ProcessName, FormatHours(a.Duration),
                a.Duration.TotalSeconds / max, p);
        }).ToList();
    }

    private static string FormatHours(TimeSpan t) =>
        t.TotalHours >= 1 ? $"{(int)t.TotalHours}ч {t.Minutes:00}м" : $"{t.Minutes}м";
}
