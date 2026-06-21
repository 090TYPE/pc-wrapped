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

    public static IReadOnlyList<int> HourlySeconds(IEnumerable<UsageSample> samples)
    {
        var buckets = new int[24];
        foreach (var s in samples)
            buckets[s.Start.Hour] += s.DurationSeconds;
        return buckets;
    }

    public static int PeakHour(IEnumerable<UsageSample> samples)
    {
        var hourly = HourlySeconds(samples);
        if (hourly.All(v => v == 0)) return -1;
        int best = 0;
        for (int h = 1; h < 24; h++)
            if (hourly[h] > hourly[best]) best = h;
        return best;
    }

    public static int Streak(IEnumerable<DateOnly> activeDays, DateOnly today)
    {
        var set = activeDays.ToHashSet();
        int streak = 0;
        var day = today;
        while (set.Contains(day))
        {
            streak++;
            day = day.AddDays(-1);
        }
        return streak;
    }
}
