using System;
using System.Runtime.InteropServices;
using System.Threading;
using PcWrapped.Core.Models;
using PcWrapped.Core.Tracking;

namespace PcWrapped.Native;

/// <summary>
/// Низкоуровневые хуки клавиатуры/мыши. Считает ТОЛЬКО количество событий и
/// пройденное мышью расстояние. Содержимое нажатий нигде не сохраняется.
/// </summary>
public sealed class Win32InputCounterSource : IInputCounterSource, IDisposable
{
    private const int WH_KEYBOARD_LL = 13;
    private const int WH_MOUSE_LL = 14;
    private const int WM_KEYDOWN = 0x0100;
    private const int WM_SYSKEYDOWN = 0x0104;
    private const int WM_LBUTTONDOWN = 0x0201;
    private const int WM_RBUTTONDOWN = 0x0204;
    private const int WM_MOUSEMOVE = 0x0200;

    private delegate IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, HookProc lpfn, IntPtr hMod, uint dwThreadId);
    [DllImport("user32.dll")] private static extern bool UnhookWindowsHookEx(IntPtr hhk);
    [DllImport("user32.dll")] private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);
    [DllImport("kernel32.dll", CharSet = CharSet.Auto)] private static extern IntPtr GetModuleHandle(string? name);

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int x; public int y; }
    [StructLayout(LayoutKind.Sequential)]
    private struct MSLLHOOKSTRUCT { public POINT pt; public uint mouseData; public uint flags; public uint time; public IntPtr dwExtraInfo; }

    // Keep delegates alive for the lifetime of the object.
    private readonly HookProc _kbProc;
    private readonly HookProc _mouseProc;
    private IntPtr _kbHook;
    private IntPtr _mouseHook;

    private long _keystrokes;
    private long _clicks;
    private double _pixels;
    private bool _hasLast;
    private int _lastX, _lastY;
    private readonly object _lock = new();

    public bool IsActive => _kbHook != IntPtr.Zero || _mouseHook != IntPtr.Zero;

    public Win32InputCounterSource()
    {
        _kbProc = KeyboardHook;
        _mouseProc = MouseHook;
    }

    public void Start()
    {
        IntPtr mod = GetModuleHandle(null);
        _kbHook = SetWindowsHookEx(WH_KEYBOARD_LL, _kbProc, mod, 0);
        _mouseHook = SetWindowsHookEx(WH_MOUSE_LL, _mouseProc, mod, 0);
        // Если хук не поставился (антивирус и т.п.) — счётчики просто останутся нулевыми.
    }

    private IntPtr KeyboardHook(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            int msg = (int)wParam;
            if (msg == WM_KEYDOWN || msg == WM_SYSKEYDOWN)
                Interlocked.Increment(ref _keystrokes);
        }
        return CallNextHookEx(_kbHook, nCode, wParam, lParam);
    }

    private IntPtr MouseHook(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            int msg = (int)wParam;
            if (msg == WM_LBUTTONDOWN || msg == WM_RBUTTONDOWN)
                Interlocked.Increment(ref _clicks);
            else if (msg == WM_MOUSEMOVE)
            {
                var data = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
                lock (_lock)
                {
                    if (_hasLast)
                    {
                        double dx = data.pt.x - _lastX;
                        double dy = data.pt.y - _lastY;
                        _pixels += Math.Sqrt(dx * dx + dy * dy);
                    }
                    _lastX = data.pt.x; _lastY = data.pt.y; _hasLast = true;
                }
            }
        }
        return CallNextHookEx(_mouseHook, nCode, wParam, lParam);
    }

    public InputCounters DrainCounters()
    {
        long k = Interlocked.Exchange(ref _keystrokes, 0);
        long c = Interlocked.Exchange(ref _clicks, 0);
        double p;
        lock (_lock) { p = _pixels; _pixels = 0; }
        return new InputCounters(k, c, p);
    }

    public void Dispose()
    {
        if (_kbHook != IntPtr.Zero) UnhookWindowsHookEx(_kbHook);
        if (_mouseHook != IntPtr.Zero) UnhookWindowsHookEx(_mouseHook);
        _kbHook = _mouseHook = IntPtr.Zero;
    }
}
