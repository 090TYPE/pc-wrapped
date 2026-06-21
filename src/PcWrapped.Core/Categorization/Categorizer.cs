using PcWrapped.Core.Models;

namespace PcWrapped.Core.Categorization;

public sealed class Categorizer
{
    private readonly Dictionary<string, Category> _rules;

    public Categorizer(IReadOnlyDictionary<string, Category> rules)
    {
        _rules = new Dictionary<string, Category>(StringComparer.OrdinalIgnoreCase);
        foreach (var kv in rules) _rules[Normalize(kv.Key)] = kv.Value;
    }

    public Category Categorize(string processName)
    {
        if (string.IsNullOrWhiteSpace(processName)) return Category.Other;
        return _rules.TryGetValue(Normalize(processName), out var cat) ? cat : Category.Other;
    }

    private static string Normalize(string name)
    {
        name = name.Trim();
        if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            name = name[..^4];
        return name;
    }
}
