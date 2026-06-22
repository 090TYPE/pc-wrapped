using Avalonia.Media;
using PcWrapped.Core.Models;

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
        Category.Work => "Работа",
        Category.Games => "Игры",
        Category.Social => "Соцсети",
        Category.Browser => "Браузер",
        _ => "Прочее",
    };
}
