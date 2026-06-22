using PcWrapped.Core.Settings;

namespace PcWrapped.Core.Tests;

public class JsonSettingsStoreTests
{
    private static string TempPath() =>
        Path.Combine(Path.GetTempPath(), $"pcwrapped-settings-{Guid.NewGuid():N}.json");

    [Fact]
    public void Load_WhenFileMissing_ReturnsDefault()
    {
        var path = TempPath();
        try
        {
            var store = new JsonSettingsStore(path);
            var settings = store.Load();
            Assert.Equal(AppSettings.Default, settings);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void SaveThenLoad_RoundTripsNonDefault()
    {
        var path = TempPath();
        try
        {
            var store = new JsonSettingsStore(path);
            var expected = new AppSettings(true, false, true);
            store.Save(expected);
            var actual = store.Load();
            Assert.Equal(expected, actual);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void Load_OnCorruptFile_ReturnsDefault()
    {
        var path = TempPath();
        try
        {
            File.WriteAllText(path, "this is not valid json {{{");
            var store = new JsonSettingsStore(path);
            var settings = store.Load();
            Assert.Equal(AppSettings.Default, settings);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void Language_RoundTrips()
    {
        var path = Path.Combine(Path.GetTempPath(), $"pcw-{Guid.NewGuid():N}.json");
        try
        {
            var store = new JsonSettingsStore(path);
            store.Save(new AppSettings(true, true, false, "en"));
            Assert.Equal("en", store.Load().Language);
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }
}
