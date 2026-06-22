using System;
using System.IO;
using PcWrapped.Core.Settings;

namespace PcWrapped.Localization;

/// <summary>Persists only the language into the app's settings.json (preserving other fields).</summary>
public static class LanguagePersistence
{
    private static string Path_ => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PcWrapped", "settings.json");

    public static void Save(AppLanguage lang)
    {
        var store = new JsonSettingsStore(Path_);
        var s = store.Load();
        store.Save(s with { Language = Loc.Code(lang) });
    }
}
