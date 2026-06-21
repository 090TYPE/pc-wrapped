using Avalonia;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Skia;
using PcWrapped.Core.Models;
using PcWrapped.Rendering;
using Xunit;

[assembly: AvaloniaTestApplication(typeof(PcWrapped.App.Tests.TestAppBuilder))]

namespace PcWrapped.App.Tests;

public static class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<Application>()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions())
            .UseSkia();
}

public class CardRendererTests
{
    private static PeriodStats Sample() => new(
        new DateOnly(2026, 6, 9), new DateOnly(2026, 6, 15),
        TimeSpan.FromHours(37), new[] { new AppUsage("code", TimeSpan.FromHours(14)) },
        new Dictionary<Category, TimeSpan> { [Category.Work] = TimeSpan.FromHours(14) },
        22, new int[24], 5, 91204, 4200, 3.4);

    [AvaloniaFact]
    public void RenderToPng_AllThemes_BothSizes_ProducesValidPng()
    {
        var dir = Path.Combine(Path.GetTempPath(), "pcwrapped-test");
        Directory.CreateDirectory(dir);

        foreach (var theme in CardThemes.All)
        foreach (var size in new[] { CardRenderer.Square, CardRenderer.Story })
        {
            var path = Path.Combine(dir, $"{theme.Id}-{size.Width}x{size.Height}.png");
            CardRenderer.RenderToPng(Sample(), theme, size, path);

            Assert.True(File.Exists(path), $"PNG not found at {path}");
            Assert.True(new FileInfo(path).Length > 1000, "PNG should be non-trivial");
            using var fs = File.OpenRead(path);
            var sig = new byte[8];
            fs.Read(sig, 0, 8);
            Assert.Equal(0x89, sig[0]); // PNG magic
            Assert.Equal((byte)'P', sig[1]);
        }
    }
}
