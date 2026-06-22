using PcWrapped.Core.Models;

namespace PcWrapped.Core.Categorization;

public static class DefaultRules
{
    public static readonly IReadOnlyDictionary<string, Category> Map =
        new Dictionary<string, Category>(StringComparer.OrdinalIgnoreCase)
        {
            // Browsers
            ["chrome"] = Category.Browser, ["msedge"] = Category.Browser,
            ["firefox"] = Category.Browser, ["opera"] = Category.Browser,
            ["opera_gx"] = Category.Browser, ["brave"] = Category.Browser,
            ["browser"] = Category.Browser, ["vivaldi"] = Category.Browser,
            ["arc"] = Category.Browser,
            // Work / dev / office
            ["code"] = Category.Work, ["devenv"] = Category.Work,
            ["rider64"] = Category.Work, ["idea64"] = Category.Work,
            ["pycharm64"] = Category.Work, ["webstorm64"] = Category.Work,
            ["clion64"] = Category.Work, ["goland64"] = Category.Work,
            ["sublime_text"] = Category.Work, ["notepad++"] = Category.Work,
            ["excel"] = Category.Work, ["winword"] = Category.Work,
            ["powerpnt"] = Category.Work, ["onenote"] = Category.Work,
            ["outlook"] = Category.Work, ["notion"] = Category.Work,
            ["obsidian"] = Category.Work, ["figma"] = Category.Work,
            ["photoshop"] = Category.Work, ["illustrator"] = Category.Work,
            ["blender"] = Category.Work, ["windowsterminal"] = Category.Work,
            ["wt"] = Category.Work, ["powershell"] = Category.Work,
            ["cmd"] = Category.Work,
            // Games / launchers
            ["steam"] = Category.Games, ["steamwebhelper"] = Category.Games,
            ["epicgameslauncher"] = Category.Games, ["battle.net"] = Category.Games,
            ["riotclient"] = Category.Games, ["leagueclient"] = Category.Games,
            ["dota2"] = Category.Games, ["csgo"] = Category.Games,
            ["cs2"] = Category.Games, ["valorant"] = Category.Games,
            ["minecraft"] = Category.Games, ["javaw"] = Category.Games,
            ["galaxyclient"] = Category.Games,
            // Social / chat
            ["discord"] = Category.Social, ["telegram"] = Category.Social,
            ["slack"] = Category.Social, ["teams"] = Category.Social,
            ["ms-teams"] = Category.Social, ["whatsapp"] = Category.Social,
            ["zoom"] = Category.Social, ["skype"] = Category.Social,
            ["viber"] = Category.Social, ["signal"] = Category.Social,
            // Media (no Media category -> Other)
            ["spotify"] = Category.Other, ["vlc"] = Category.Other,
            ["music"] = Category.Other, ["wmplayer"] = Category.Other,
            ["mpc-hc"] = Category.Other, ["foobar2000"] = Category.Other,
        };
}
