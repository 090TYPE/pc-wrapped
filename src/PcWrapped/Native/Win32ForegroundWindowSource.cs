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

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(int access, bool inherit, uint pid);
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr h);
    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool QueryFullProcessImageName(IntPtr h, int flags, StringBuilder buf, ref int size);
    private const int PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;

    [StructLayout(LayoutKind.Sequential)]
    private struct LASTINPUTINFO { public uint cbSize; public uint dwTime; }

    public ForegroundInfo? GetForeground()
    {
        var hwnd = GetForegroundWindow();
        if (hwnd == IntPtr.Zero) return null;
        GetWindowThreadProcessId(hwnd, out uint pid);
        if (pid == 0) return null;

        string process;
        try
        {
            var proc = Process.GetProcessById((int)pid);
            process = proc.ProcessName;
        }
        catch { return null; }

        string? path = null;
        var ph = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
        if (ph != IntPtr.Zero)
        {
            try
            {
                var buf = new StringBuilder(1024);
                int len = buf.Capacity;
                if (QueryFullProcessImageName(ph, 0, buf, ref len))
                    path = buf.ToString();
            }
            finally { CloseHandle(ph); }
        }

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
