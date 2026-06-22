using System;
using System.Collections.Generic;
using System.Linq;
using PcWrapped.Core.Categorization;
using PcWrapped.Core.Models;
using PcWrapped.Localization;

namespace PcWrapped.ViewModels;

public sealed record AppRowVm(string Name, string TimeText, double Fraction,
    string? ExecutablePath, Category Category)
{
    public static IReadOnlyList<AppRowVm> FromStats(
        PeriodStats stats, IReadOnlyDictionary<string, string> paths, Categorizer categorizer)
    {
        if (stats.TopApps.Count == 0) return Array.Empty<AppRowVm>();
        double max = stats.TopApps.Max(a => a.Duration.TotalSeconds);
        if (max <= 0) max = 1;
        return stats.TopApps.Select(a =>
        {
            paths.TryGetValue(a.ProcessName, out var p);
            return new AppRowVm(a.ProcessName, Loc.Hours(a.Duration),
                a.Duration.TotalSeconds / max, p, categorizer.Categorize(a.ProcessName));
        }).ToList();
    }
}
