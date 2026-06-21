using PcWrapped.Core.Categorization;
using PcWrapped.Core.Models;

namespace PcWrapped.Core.Aggregation;

public static class Aggregator
{
    public static TimeSpan TotalActive(IEnumerable<UsageSample> samples) =>
        TimeSpan.FromSeconds(samples.Sum(s => (long)s.DurationSeconds));

    public static IReadOnlyList<AppUsage> TopApps(IEnumerable<UsageSample> samples, int limit) =>
        samples
            .GroupBy(s => s.ProcessName)
            .Select(g => new AppUsage(g.Key, TimeSpan.FromSeconds(g.Sum(x => (long)x.DurationSeconds))))
            .OrderByDescending(a => a.Duration)
            .Take(limit)
            .ToList();

    public static IReadOnlyDictionary<Category, TimeSpan> ByCategory(
        IEnumerable<UsageSample> samples, Categorizer categorizer)
    {
        var result = new Dictionary<Category, TimeSpan>();
        foreach (var s in samples)
        {
            var cat = categorizer.Categorize(s.ProcessName);
            result.TryGetValue(cat, out var cur);
            result[cat] = cur + TimeSpan.FromSeconds(s.DurationSeconds);
        }
        return result;
    }
}
