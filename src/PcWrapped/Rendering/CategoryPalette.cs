using Avalonia.Media;
using PcWrapped.Core.Models;
using PcWrapped.Localization;

namespace PcWrapped.Rendering;

public static class CategoryPalette
{
    public static Color Of(Category c) => c switch
    {
        Category.Work => Color.Parse("#7B2FF7"),
        Category.Games => Color.Parse("#F107A3"),
        Category.Social => Color.Parse("#3FB950"),
        Category.Browser => Color.Parse("#58A6FF"),
        _ => Color.Parse("#8A8F9B"),
    };

    public static string Name(Category c) => c switch
    {
        Category.Work => Loc.T("cat.work"),
        Category.Games => Loc.T("cat.games"),
        Category.Social => Loc.T("cat.social"),
        Category.Browser => Loc.T("cat.browser"),
        _ => Loc.T("cat.other"),
    };
}
