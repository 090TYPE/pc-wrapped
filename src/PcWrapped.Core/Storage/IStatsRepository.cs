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
    Task RollupOlderThanAsync(DateOnly cutoffDay);
    Task UpsertAppPathAsync(string process, string path);
    Task<IReadOnlyDictionary<string, string>> GetAppPathsAsync();
    Task UpsertCategoryOverrideAsync(string process, Category category);
    Task<IReadOnlyDictionary<string, Category>> GetCategoryOverridesAsync();
    Task AddExclusionAsync(string process);
    Task RemoveExclusionAsync(string process);
    Task<IReadOnlySet<string>> GetExclusionsAsync();
    Task ClearAllDataAsync();
}
