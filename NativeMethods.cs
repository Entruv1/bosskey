using System.Runtime.InteropServices;
using System.Text;

namespace BossKey;

internal static class NativeMethods
{
    internal const int SwHide = 0;
    internal const int SwShowNormal = 1;
    internal const int SwShowMinimized = 2;
    internal const int SwShowMaximized = 3;
    internal const int SwShow = 5;
    internal const int SwMinimize = 6;
    internal const int SwRestore = 9;

    internal const uint GaRoot = 2;
    internal const uint GwChild = 5;
    internal const uint GwHwndNext = 2;
    internal const int GwlExStyle = -20;
    internal const int WsExTransparent = 0x00000020;
    internal const uint SwpNoSize = 0x0001;
    internal const uint SwpNoZOrder = 0x0004;
    internal const uint SwpNoActivate = 0x0010;

    internal const int WhKeyboardLl = 13;
    internal const int WmKeyDown = 0x0100;
    internal const int WmKeyUp = 0x0101;
    internal const int WmSysKeyDown = 0x0104;
    internal const int WmSysKeyUp = 0x0105;

    internal const int VkShift = 0x10;
    internal const int VkControl = 0x11;
    internal const int VkMenu = 0x12;
    internal const int VkLShift = 0xA0;
    internal const int VkRShift = 0xA1;
    internal const int VkLControl = 0xA2;
    internal const int VkRControl = 0xA3;
    internal const int VkLMenu = 0xA4;
    internal const int VkRMenu = 0xA5;
    internal const int VkLWin = 0x5B;
    internal const int VkRWin = 0x5C;

    [StructLayout(LayoutKind.Sequential)]
    internal struct PointApi
    {
        internal int X;
        internal int Y;

        internal PointApi(int x, int y)
        {
            X = x;
            Y = y;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct RectApi
    {
        internal int Left;
        internal int Top;
        internal int Right;
        internal int Bottom;

        internal int Width => Right - Left;
        internal int Height => Bottom - Top;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct KbdLlHookStruct
    {
        internal uint VkCode;
        internal uint ScanCode;
        internal uint Flags;
        internal uint Time;
        internal IntPtr ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct WindowPlacement
    {
        internal int Length;
        internal int Flags;
        internal int ShowCmd;
        internal PointApi MinPosition;
        internal PointApi MaxPosition;
        internal RectApi NormalPosition;
    }

    internal delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
    internal delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern IntPtr SetWindowsHookEx(
        int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    internal static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    internal static extern IntPtr GetModuleHandle(string? lpModuleName);

    [DllImport("user32.dll")]
    internal static extern short GetAsyncKeyState(int vKey);

    [DllImport("user32.dll")]
    internal static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    internal static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    internal static extern bool IsIconic(IntPtr hWnd);

    [DllImport("user32.dll")]
    internal static extern bool IsWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    internal static extern IntPtr GetAncestor(IntPtr hwnd, uint gaFlags);

    [DllImport("user32.dll")]
    internal static extern bool GetCursorPos(out PointApi lpPoint);

    [DllImport("user32.dll")]
    internal static extern IntPtr WindowFromPoint(PointApi point);

    [DllImport("user32.dll")]
    internal static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    internal static extern int GetWindowTextLength(IntPtr hWnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    internal static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    internal static extern int GetClassName(IntPtr hWnd, StringBuilder text, int count);

    [DllImport("user32.dll")]
    internal static extern bool GetWindowRect(IntPtr hWnd, out RectApi rect);

    [DllImport("user32.dll")]
    internal static extern bool MoveWindow(IntPtr hWnd, int x, int y, int width, int height, bool repaint);

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern bool SetWindowPos(IntPtr hWnd, IntPtr insertAfter, int x, int y, int cx, int cy, uint flags);

    [DllImport("user32.dll")]
    internal static extern bool GetWindowPlacement(IntPtr hWnd, ref WindowPlacement placement);

    [DllImport("user32.dll")]
    internal static extern bool SetWindowPlacement(IntPtr hWnd, ref WindowPlacement placement);

    [DllImport("user32.dll")]
    internal static extern bool EnumWindows(EnumWindowsProc callback, IntPtr lParam);

    [DllImport("user32.dll")]
    internal static extern IntPtr GetTopWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    internal static extern IntPtr GetWindow(IntPtr hWnd, uint uCmd);

    [DllImport("user32.dll")]
    internal static extern IntPtr GetDesktopWindow();

    [DllImport("user32.dll")]
    internal static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    internal static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    internal static string GetWindowTitle(IntPtr hWnd)
    {
        int len = GetWindowTextLength(hWnd);
        if (len <= 0)
            return string.Empty;

        var sb = new StringBuilder(len + 1);
        GetWindowText(hWnd, sb, sb.Capacity);
        return sb.ToString();
    }

    internal static string GetWindowClassName(IntPtr hWnd)
    {
        var sb = new StringBuilder(256);
        GetClassName(hWnd, sb, sb.Capacity);
        return sb.ToString();
    }
}
