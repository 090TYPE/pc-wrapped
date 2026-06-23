using System;
using System.IO;
using System.Runtime.InteropServices;

namespace PcWrapped.Native;

/// <summary>Puts a PNG image on the Windows clipboard as CF_BITMAP. No-throw.</summary>
public static class ClipboardImage
{
    private const uint CF_BITMAP = 2;

    [DllImport("user32.dll")] private static extern bool OpenClipboard(IntPtr hWndNewOwner);
    [DllImport("user32.dll")] private static extern bool EmptyClipboard();
    [DllImport("user32.dll")] private static extern IntPtr SetClipboardData(uint uFormat, IntPtr hMem);
    [DllImport("user32.dll")] private static extern bool CloseClipboard();
    [DllImport("gdi32.dll")] private static extern bool DeleteObject(IntPtr hObject);

    public static void SetPng(Stream pngStream)
    {
        try
        {
            using var bmp = new System.Drawing.Bitmap(pngStream);
            IntPtr hBmp = bmp.GetHbitmap();
            bool handedOver = false;
            try
            {
                if (OpenClipboard(IntPtr.Zero))
                {
                    EmptyClipboard();
                    handedOver = SetClipboardData(CF_BITMAP, hBmp) != IntPtr.Zero;
                    CloseClipboard();
                }
            }
            finally
            {
                if (!handedOver) DeleteObject(hBmp); // clipboard owns it once handed over
            }
        }
        catch { /* clipboard may be locked; ignore */ }
    }
}
