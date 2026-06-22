using PcWrapped.Core.Aggregation;
using PcWrapped.Core.Models;

namespace PcWrapped.Core.Tests;

public class ChartDataTests
{
    [Fact]
    public void Segments_ComputesFractionsDescending()
    {
        var byCat = new Dictionary<Category, TimeSpan>
        {
            [Category.Work] = TimeSpan.FromHours(3),
            [Category.Games] = TimeSpan.FromHours(1),
        };
        var segs = ChartData.Segments(byCat);
        Assert.Equal(2, segs.Count);
        Assert.Equal(Category.Work, segs[0].Category);
        Assert.Equal(0.75, segs[0].Fraction, 3);
        Assert.Equal(0.25, segs[1].Fraction, 3);
        Assert.Equal(1.0, segs.Sum(s => s.Fraction), 3);
    }

    [Fact]
    public void Segments_SkipsZeroAndEmpty()
    {
        Assert.Empty(ChartData.Segments(new Dictionary<Category, TimeSpan>()));
        var byCat = new Dictionary<Category, TimeSpan> { [Category.Work] = TimeSpan.Zero };
        Assert.Empty(ChartData.Segments(byCat));
    }

    [Fact]
    public void NormalizeHours_DividesByMax_PreservesLength()
    {
        var hours = new int[24];
        hours[10] = 50; hours[22] = 100;
        var n = ChartData.NormalizeHours(hours);
        Assert.Equal(24, n.Count);
        Assert.Equal(0.5, n[10], 3);
        Assert.Equal(1.0, n[22], 3);
        Assert.Equal(0.0, n[0], 3);
    }

    [Fact]
    public void NormalizeHours_AllZero_ReturnsAllZero()
    {
        var n = ChartData.NormalizeHours(new int[24]);
        Assert.Equal(24, n.Count);
        Assert.All(n, v => Assert.Equal(0.0, v, 6));
    }
}
