using System;
using System.Collections.Generic;

namespace PcWrapped.Localization;

public enum AppLanguage { Ru, En }

public static class Loc
{
    public static AppLanguage Current { get; set; } = AppLanguage.Ru;

    public static string T(string key)
    {
        var dict = Current == AppLanguage.En ? En : Ru;
        return dict.TryGetValue(key, out var v) ? v : key;
    }

    public static string Hours(TimeSpan t)
    {
        int h = (int)t.TotalHours, m = t.Minutes;
        if (Current == AppLanguage.En) return h >= 1 ? $"{h}h {m:00}m" : $"{m}m";
        return h >= 1 ? $"{h}ч {m:00}м" : $"{m}м";
    }

    public static string Days(int n) => Current == AppLanguage.En ? $"{n}d" : $"{n} дн.";

    public static AppLanguage Parse(string? code) =>
        string.Equals(code, "en", StringComparison.OrdinalIgnoreCase) ? AppLanguage.En : AppLanguage.Ru;

    public static string Code(AppLanguage lang) => lang == AppLanguage.En ? "en" : "ru";

    private static readonly Dictionary<string, string> Ru = new()
    {
        ["tab.today"] = "Сегодня", ["tab.week"] = "Неделя", ["tab.year"] = "Год",
        ["rail.theme"] = "ТЕМА", ["rail.format"] = "ФОРМАТ", ["rail.language"] = "ЯЗЫК",
        ["rail.share"] = "Поделиться ↗", ["rail.streak"] = "СЕРИЯ 🔥",
        ["rail.hourly"] = "АКТИВНОСТЬ ПО ЧАСАМ", ["rail.topapps"] = "ТОП ПРИЛОЖЕНИЙ",
        ["rail.preview"] = "ПРЕВЬЮ", ["status.ready"] = "Готово",
        ["period.day"] = "ТВОЙ ДЕНЬ ЗА ПК", ["period.week"] = "ТВОЯ НЕДЕЛЯ ЗА ПК",
        ["period.year"] = "ТВОЙ ГОД ЗА ПК",
        ["card.mouse"] = "🖱️ Мышь проехала", ["card.keys"] = "⌨️ Нажатий",
        ["card.peak"] = "🔥 Пик", ["card.streak"] = "📅 Серия",
        ["unit.km"] = "км",
        ["cat.work"] = "Работа", ["cat.games"] = "Игры", ["cat.social"] = "Соцсети",
        ["cat.browser"] = "Браузер", ["cat.other"] = "Прочее",
        ["onb.title"] = "Добро пожаловать в PC Wrapped",
        ["onb.privacyTitle"] = "Приватность прежде всего",
        ["onb.privacyBody"] = "PC Wrapped считает время в приложениях и количество нажатий/кликов/движений мыши, чтобы строить красивые карточки. Все данные хранятся только на этом компьютере и никогда не отправляются в сеть. Содержимое набранного текста не сохраняется — только счётчики.",
        ["onb.vanity"] = "Считать нажатия клавиш и движение мыши",
        ["onb.autostart"] = "Запускать при старте Windows",
        ["onb.start"] = "Начать",
    };

    private static readonly Dictionary<string, string> En = new()
    {
        ["tab.today"] = "Today", ["tab.week"] = "Week", ["tab.year"] = "Year",
        ["rail.theme"] = "THEME", ["rail.format"] = "FORMAT", ["rail.language"] = "LANGUAGE",
        ["rail.share"] = "Share ↗", ["rail.streak"] = "STREAK 🔥",
        ["rail.hourly"] = "ACTIVITY BY HOUR", ["rail.topapps"] = "TOP APPS",
        ["rail.preview"] = "PREVIEW", ["status.ready"] = "Done",
        ["period.day"] = "YOUR DAY ON PC", ["period.week"] = "YOUR WEEK ON PC",
        ["period.year"] = "YOUR YEAR ON PC",
        ["card.mouse"] = "🖱️ Mouse traveled", ["card.keys"] = "⌨️ Keystrokes",
        ["card.peak"] = "🔥 Peak", ["card.streak"] = "📅 Streak",
        ["unit.km"] = "km",
        ["cat.work"] = "Work", ["cat.games"] = "Games", ["cat.social"] = "Social",
        ["cat.browser"] = "Browser", ["cat.other"] = "Other",
        ["onb.title"] = "Welcome to PC Wrapped",
        ["onb.privacyTitle"] = "Privacy first",
        ["onb.privacyBody"] = "PC Wrapped counts time in apps and the number of keystrokes/clicks/mouse movement to build beautiful cards. All data stays on this computer and is never sent anywhere. The text you type is never stored — only counts.",
        ["onb.vanity"] = "Count keystrokes and mouse movement",
        ["onb.autostart"] = "Launch on Windows startup",
        ["onb.start"] = "Start",
    };
}
