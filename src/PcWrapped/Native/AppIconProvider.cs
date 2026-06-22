using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

namespace PcWrapped.Native;

/// <summary>Извлекает иконку .exe и кэширует её. Windows-only.</summary>
public static class AppIconProvider
{
    private static readonly Dictionary<string, Avalonia.Media.Imaging.Bitmap?> _cache =
        new(StringComparer.OrdinalIgnoreCase);

    public static Avalonia.Media.Imaging.Bitmap? GetIcon(string? exePath)
    {
        if (string.IsNullOrEmpty(exePath)) return null;
        if (_cache.TryGetValue(exePath, out var cached)) return cached;

        Avalonia.Media.Imaging.Bitmap? result = null;
        try
        {
            if (File.Exists(exePath))
            {
                using var icon = Icon.ExtractAssociatedIcon(exePath);
                if (icon is not null)
                {
                    using var sysBmp = icon.ToBitmap();
                    using var ms = new MemoryStream();
                    sysBmp.Save(ms, ImageFormat.Png);
                    ms.Position = 0;
                    result = new Avalonia.Media.Imaging.Bitmap(ms);
                }
            }
        }
        catch { result = null; }

        _cache[exePath] = result;
        return result;
    }
}
