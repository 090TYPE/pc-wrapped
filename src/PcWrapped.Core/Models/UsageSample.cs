namespace PcWrapped.Core.Models;

/// <summary>Один зафиксированный интервал активности в приложении.</summary>
public sealed record UsageSample(
    DateTimeOffset Start,
    string ProcessName,
    string WindowTitle,
    int DurationSeconds);
