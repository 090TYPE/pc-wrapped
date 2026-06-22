using System;
using System.Globalization;
using Avalonia.Data.Converters;
using PcWrapped.Native;

namespace PcWrapped.Converters;

public sealed class PathToIconConverter : IValueConverter
{
    public static readonly PathToIconConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => AppIconProvider.GetIcon(value as string);

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
