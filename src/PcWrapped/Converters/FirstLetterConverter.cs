using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace PcWrapped.Converters;

public sealed class FirstLetterConverter : IValueConverter
{
    public static readonly FirstLetterConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var s = value as string;
        return string.IsNullOrEmpty(s) ? "?" : s.Substring(0, 1).ToUpperInvariant();
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
