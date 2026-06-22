using PcWrapped.Core.Models;

namespace PcWrapped.Core.Categorization;

public static class CategoryRules
{
    public static IReadOnlyDictionary<string, Category> Merge(
        IReadOnlyDictionary<string, Category> defaults,
        IReadOnlyDictionary<string, Category> overrides)
    {
        var result = new Dictionary<string, Category>(StringComparer.OrdinalIgnoreCase);
        foreach (var kv in defaults) result[Categorizer.Normalize(kv.Key)] = kv.Value;
        foreach (var kv in overrides) result[Categorizer.Normalize(kv.Key)] = kv.Value;
        return result;
    }
}
