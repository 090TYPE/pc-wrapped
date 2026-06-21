using PcWrapped.Core.Models;

namespace PcWrapped.Core.Storage;

public interface IStatsRepository
{
    Task InitializeAsync();
    Task AddSampleAsync(UsageSample sample);
    Task<IReadOnlyList<UsageSample>> GetSamplesAsync(DateTimeOffset fromInclusive, DateTimeOffset toExclusive);
    Task AddInputCountersAsync(DateOnly day, InputCounters delta);
    Task<InputCounters> GetInputCountersAsync(DateOnly fromInclusive, DateOnly toInclusive);
    Task<IReadOnlyList<DateOnly>> GetActiveDaysAsync();
}
