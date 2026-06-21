using System;
using System.Threading.Tasks;
using PcWrapped.Core.Aggregation;
using PcWrapped.Core.Categorization;
using PcWrapped.Core.Models;
using PcWrapped.Core.Storage;
using PcWrapped.Rendering;

namespace PcWrapped.ViewModels;

public sealed class MainViewModel
{
    private readonly IStatsRepository _repo;
    private readonly Categorizer _categorizer = new(DefaultRules.Map);

    public CardTheme SelectedTheme { get; set; } = CardThemes.Gradient;
    public Avalonia.PixelSize SelectedSize { get; set; } = CardRenderer.Square;

    public MainViewModel(IStatsRepository repo) => _repo = repo;

    public async Task<PeriodStats> BuildWeekStatsAsync(DateOnly today, double mouseDpi)
    {
        var from = today.AddDays(-6);
        var fromDt = new DateTimeOffset(from.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var toDt = new DateTimeOffset(today.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);

        var samples = await _repo.GetSamplesAsync(fromDt, toDt);
        var counters = await _repo.GetInputCountersAsync(from, today);
        var activeDays = await _repo.GetActiveDaysAsync();

        return Aggregator.BuildPeriodStats(from, today, samples, _categorizer,
            counters, activeDays, today, topAppLimit: 5, mouseDpi: mouseDpi);
    }

    public Task ExportAsync(PeriodStats stats, string path) =>
        Task.Run(() => CardRenderer.RenderToPng(stats, SelectedTheme, SelectedSize, path));
}
