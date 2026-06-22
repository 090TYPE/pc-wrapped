using PcWrapped.Core.Categorization;
using PcWrapped.Core.Models;

namespace PcWrapped.Core.Tests;

public class CategorizerTests
{
    [Fact]
    public void Categorize_KnownProcess_ReturnsMappedCategory()
    {
        var c = new Categorizer(new Dictionary<string, Category>
        {
            ["chrome"] = Category.Browser
        });
        Assert.Equal(Category.Browser, c.Categorize("chrome"));
    }

    [Fact]
    public void Categorize_IsCaseInsensitive_AndIgnoresExe()
    {
        var c = new Categorizer(new Dictionary<string, Category>
        {
            ["code"] = Category.Work
        });
        Assert.Equal(Category.Work, c.Categorize("Code.exe"));
        Assert.Equal(Category.Work, c.Categorize("CODE"));
    }

    [Fact]
    public void Categorize_Unknown_ReturnsOther()
    {
        var c = new Categorizer(new Dictionary<string, Category>());
        Assert.Equal(Category.Other, c.Categorize("whatever"));
    }

    [Fact]
    public void DefaultRules_ContainsCommonApps()
    {
        var c = new Categorizer(DefaultRules.Map);
        Assert.Equal(Category.Browser, c.Categorize("chrome"));
        Assert.Equal(Category.Work, c.Categorize("code"));
        Assert.Equal(Category.Work, c.Categorize("pycharm64"));
        Assert.Equal(Category.Social, c.Categorize("discord"));
        Assert.Equal(Category.Games, c.Categorize("steam"));
        Assert.Equal(Category.Other, c.Categorize("spotify"));
    }
}
