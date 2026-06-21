using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using PcWrapped.Core.Tracking;

namespace PcWrapped.Native;

public sealed class Win32ForegroundWindowSource : IForegroundWindowSource
{
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);
    [DllImport("user32.dll")] private static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);

    [StructLayout(LayoutKind.Sequential)]
    private struct LASTINPUTINFO { public uint cbSize; public uint dwTime; }

    public ForegroundInfo? GetForeground()
    {
        var hwnd = GetForegroundWindow();
        if (hwnd == IntPtr.Zero) return null;
        GetWindowThreadProcessId(hwnd, out uint pid);
        if (pid == 0) return null;

        string process;
        string? path = null;
        try
        {
            var proc = Process.GetProcessById((int)pid);
            process = proc.ProcessName;
            try { path = proc.MainModule?.FileName; } catch { path = null; }
        }
        catch { return null; }

        var sb = new StringBuilder(512);
        GetWindowText(hwnd, sb, sb.Capacity);
        return new ForegroundInfo(process, sb.ToString(), path);
    }

    public int GetIdleSeconds()
    {
        var info = new LASTINPUTINFO { cbSize = (uint)Marshal.SizeOf<LASTINPUTINFO>() };
        if (!GetLastInputInfo(ref info)) return 0;
        uint idleMs = (uint)Environment.TickCount - info.dwTime;
        return (int)(idleMs / 1000);
    }
}
