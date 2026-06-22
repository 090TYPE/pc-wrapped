using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;

namespace PcWrapped.Native;

/// <summary>Извлекает иконку .exe и кэширует её. Windows-only.</summary>
public static class AppIconProvider
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct SHFILEINFO
    {
        public IntPtr hIcon;
        public int iIcon;
        public uint dwAttributes;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string szDisplayName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)] public string szTypeName;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes, ref SHFILEINFO psfi, uint cbSizeFileInfo, uint uFlags);
    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr hIcon);
    private const uint SHGFI_ICON = 0x000000100;
    private const uint SHGFI_LARGEICON = 0x000000000;

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
                var shfi = new SHFILEINFO();
                IntPtr hImg = SHGetFileInfo(exePath, 0, ref shfi, (uint)Marshal.SizeOf<SHFILEINFO>(),
                    SHGFI_ICON | SHGFI_LARGEICON);
                if (shfi.hIcon != IntPtr.Zero)
                {
                    try { result = HIconToBitmap(shfi.hIcon); }
                    finally { DestroyIcon(shfi.hIcon); }
                }

                if (result is null)
                {
                    using var icon = Icon.ExtractAssociatedIcon(exePath);
                    if (icon is not null)
                        result = HIconToBitmap(icon.Handle);
                }
            }
        }
        catch { result = null; }

        _cache[exePath] = result;
        return result;
    }

    private static Avalonia.Media.Imaging.Bitmap HIconToBitmap(IntPtr hIcon)
    {
        using var icon = Icon.FromHandle(hIcon);
        using var sysBmp = icon.ToBitmap();
        using var ms = new MemoryStream();
        sysBmp.Save(ms, ImageFormat.Png);
        ms.Position = 0;
        return new Avalonia.Media.Imaging.Bitmap(ms);
    }
}
