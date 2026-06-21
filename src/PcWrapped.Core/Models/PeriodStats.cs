namespace PcWrapped.Core.Models;

public sealed record PeriodStats(
    DateOnly From,
    DateOnly To,
    TimeSpan TotalActive,
    IReadOnlyList<AppUsage> TopApps,
    IReadOnlyDictionary<Category, TimeSpan> ByCategory,
    int PeakHour,                 // 0..23, -1 если данных нет
    IReadOnlyList<int> HourlySeconds,  // длина 24
    int StreakDays,
    long Keystrokes,
    long Clicks,
    double MouseKilometers);
