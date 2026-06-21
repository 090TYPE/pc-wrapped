using PcWrapped.Core.Models;

namespace PcWrapped.Core.Categorization;

public static class DefaultRules
{
    public static readonly IReadOnlyDictionary<string, Category> Map =
        new Dictionary<string, Category>(StringComparer.OrdinalIgnoreCase)
        {
            ["chrome"] = Category.Browser,
            ["msedge"] = Category.Browser,
            ["firefox"] = Category.Browser,
            ["code"] = Category.Work,
            ["devenv"] = Category.Work,
            ["rider64"] = Category.Work,
            ["excel"] = Category.Work,
            ["winword"] = Category.Work,
            ["discord"] = Category.Social,
            ["telegram"] = Category.Social,
            ["slack"] = Category.Social,
            ["steam"] = Category.Games,
            ["steamwebhelper"] = Category.Games,
        };
}
