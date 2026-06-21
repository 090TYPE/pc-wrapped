using PcWrapped.Core.Aggregation;

namespace PcWrapped.Core.Tests;

public class VanityMathTests
{
    [Fact]
    public void PixelsToKilometers_At96Dpi_ConvertsCorrectly()
    {
        // 96 px @ 96 dpi = 1 inch = 0.0254 m = 0.0000254 km
        double km = VanityMath.PixelsToKilometers(96, dpi: 96);
        Assert.Equal(0.0000254, km, 9);
    }

    [Fact]
    public void PixelsToKilometers_Zero_IsZero()
    {
        Assert.Equal(0, VanityMath.PixelsToKilometers(0, 96));
    }

    [Fact]
    public void PixelsToKilometers_InvalidDpi_FallsBackTo96()
    {
        double a = VanityMath.PixelsToKilometers(96, dpi: 0);
        Assert.Equal(0.0000254, a, 9);
    }
}
