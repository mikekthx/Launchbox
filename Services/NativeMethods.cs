using System;
using System.Runtime.InteropServices;
using System.Text;

namespace Launchbox.Services;

public delegate IntPtr WndProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

public static class NativeMethods
{
    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern uint PrivateExtractIcons(string l, int n, int cx, int cy, ref IntPtr p, IntPtr id, uint ni, uint fl);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern bool DestroyIcon(IntPtr hIcon);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern int GetPrivateProfileString(string s, string k, string d, StringBuilder r, int z, string f);

    // From MainWindow.xaml.cs
    public const int SW_RESTORE = 9;

    /// <summary>
    /// Sets a new address for the window procedure.
    /// </summary>
    public const int GWLP_WNDPROC = -4;

    /// <summary>
    /// Posted when the user presses a hot key registered by the RegisterHotKey function.
    /// </summary>
    public const int WM_HOTKEY = 0x0312;

    /// <summary>
    /// Posted when the user double-clicks the left mouse button while the cursor is within the nonclient area of a window.
    /// </summary>
    public const int WM_NCLBUTTONDBLCLK = 0x00A3;

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern bool IsIconic(IntPtr hWnd);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern IntPtr CallWindowProc(IntPtr lpPrevWndFunc, IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true, EntryPoint = "SetWindowLongPtr", CharSet = CharSet.Unicode)]
    private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, WndProcDelegate dwNewLong);

    [DllImport("user32.dll", SetLastError = true, EntryPoint = "SetWindowLong", CharSet = CharSet.Unicode)]
    private static extern IntPtr SetWindowLong32(IntPtr hWnd, int nIndex, WndProcDelegate dwNewLong);

    public static IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, WndProcDelegate dwNewLong)
    {
        if (IntPtr.Size == 8) return SetWindowLongPtr64(hWnd, nIndex, dwNewLong);
        else return SetWindowLong32(hWnd, nIndex, dwNewLong);
    }

    [DllImport("user32.dll", SetLastError = true, EntryPoint = "SetWindowLongPtr", CharSet = CharSet.Unicode)]
    private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [DllImport("user32.dll", SetLastError = true, EntryPoint = "SetWindowLong", CharSet = CharSet.Unicode)]
    private static extern IntPtr SetWindowLong32(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    public static IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong)
    {
        if (IntPtr.Size == 8) return SetWindowLongPtr64(hWnd, nIndex, dwNewLong);
        else return SetWindowLong32(hWnd, nIndex, dwNewLong);
    }
}
