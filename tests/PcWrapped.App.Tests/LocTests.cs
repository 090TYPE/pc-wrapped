using System;
using PcWrapped.Localization;
using Xunit;

namespace PcWrapped.App.Tests;

public class LocTests
{
    [Fact]
    public void T_ReturnsStringForCurrentLanguage()
    {
        Loc.Current = AppLanguage.Ru;
        Assert.Equal("Работа", Loc.T("cat.work"));
        Loc.Current = AppLanguage.En;
        Assert.Equal("Work", Loc.T("cat.work"));
    }

    [Fact]
    public void T_MissingKey_ReturnsKey()
    {
        Loc.Current = AppLanguage.En;
        Assert.Equal("nope.nope", Loc.T("nope.nope"));
    }

    [Fact]
    public void Hours_FormatsPerLanguage()
    {
        Loc.Current = AppLanguage.Ru;
        Assert.Equal("2ч 05м", Loc.Hours(TimeSpan.FromMinutes(125)));
        Loc.Current = AppLanguage.En;
        Assert.Equal("2h 05m", Loc.Hours(TimeSpan.FromMinutes(125)));
        Assert.Equal("7m", Loc.Hours(TimeSpan.FromMinutes(7)));
    }

    [Fact]
    public void Days_FormatsPerLanguage()
    {
        Loc.Current = AppLanguage.Ru;
        Assert.Equal("5 дн.", Loc.Days(5));
        Loc.Current = AppLanguage.En;
        Assert.Equal("5d", Loc.Days(5));
    }

    [Fact]
    public void ParseAndCode_RoundTrip()
    {
        Assert.Equal(AppLanguage.En, Loc.Parse("en"));
        Assert.Equal(AppLanguage.Ru, Loc.Parse("ru"));
        Assert.Equal(AppLanguage.Ru, Loc.Parse("xx")); // unknown -> Ru
        Assert.Equal("en", Loc.Code(AppLanguage.En));
        Assert.Equal("ru", Loc.Code(AppLanguage.Ru));
    }
}
