using PcWrapped.Core.Models;

namespace PcWrapped.Core.Aggregation;

public static class ChartData
{
    public static IReadOnlyList<CategorySlice> Segments(IReadOnlyDictionary<Category, TimeSpan> byCategory)
    {
        double total = byCategory.Values.Sum(t => t.TotalSeconds);
        if (total <= 0) return Array.Empty<CategorySlice>();
        return byCategory
            .Where(kv => kv.Value.TotalSeconds > 0)
            .OrderByDescending(kv => kv.Value.TotalSeconds)
            .Select(kv => new CategorySlice(kv.Key, kv.Value.TotalSeconds / total))
            .ToList();
    }

    public static IReadOnlyList<double> NormalizeHours(IReadOnlyList<int> hours)
    {
        if (hours is null || hours.Count == 0) return Array.Empty<double>();
        int max = hours.Max();
        if (max <= 0) return hours.Select(_ => 0.0).ToList();
        return hours.Select(h => (double)h / max).ToList();
    }
}

public readonly record struct CategorySlice(Category Category, double Fraction);
