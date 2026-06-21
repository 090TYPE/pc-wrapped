namespace PcWrapped.Core.Aggregation;

public static class VanityMath
{
    private const double MetersPerInch = 0.0254;

    public static double PixelsToKilometers(double pixels, double dpi)
    {
        if (dpi <= 0) dpi = 96;
        double inches = pixels / dpi;
        double meters = inches * MetersPerInch;
        return meters / 1000.0;
    }
}
