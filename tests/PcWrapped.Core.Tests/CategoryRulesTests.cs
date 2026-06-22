using PcWrapped.Core.Categorization;
using PcWrapped.Core.Models;

namespace PcWrapped.Core.Tests;

public class CategoryRulesTests
{
    [Fact]
    public void Merge_OverrideBeatsDefault()
    {
        var defaults = new Dictionary<string, Category> { ["code"] = Category.Work };
        var overrides = new Dictionary<string, Category> { ["code"] = Category.Games };
        var merged = CategoryRules.Merge(defaults, overrides);
        Assert.Equal(Category.Games, merged["code"]);
    }

    [Fact]
    public void Merge_NormalizesNames_ExeOverrideBeatsBareDefault()
    {
        var defaults = new Dictionary<string, Category> { ["code"] = Category.Work };
        var overrides = new Dictionary<string, Category> { ["Code.exe"] = Category.Social };
        var merged = CategoryRules.Merge(defaults, overrides);
        Assert.Single(merged);
        Assert.Equal(Category.Social, merged["code"]);
    }

    [Fact]
    public void Merge_EmptyInputs_Empty()
    {
        var merged = CategoryRules.Merge(
            new Dictionary<string, Category>(), new Dictionary<string, Category>());
        Assert.Empty(merged);
    }
}
