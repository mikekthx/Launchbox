using Launchbox.Helpers;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using System;
using System.Diagnostics;

namespace Launchbox.Services;

public class WindowService
{
    private readonly Window _window;
    private readonly WindowPositionManager _positionManager;
    private AppWindow? _appWindow;
    private IntPtr _hWnd;
    private IntPtr _oldWndProc;
    private WndProcDelegate? _wndProcDelegate;
    private bool _hasPositioned = false;

    public WindowService(Window window, WindowPositionManager positionManager)
    {
        _window = window;
        _positionManager = positionManager;
    }

    public void Initialize()
    {
        _hWnd = WinRT.Interop.WindowNative.GetWindowHandle(_window);
        var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(_hWnd);
        _appWindow = AppWindow.GetFromWindowId(windowId);

        // Window Setup
        _window.ExtendsContentIntoTitleBar = true;
        _appWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Collapsed;
        _appWindow.IsShownInSwitchers = false;

        // Start Off-Screen
        _appWindow.Resize(new Windows.Graphics.SizeInt32(Constants.WINDOW_WIDTH, Constants.WINDOW_HEIGHT));
        _appWindow.Move(new Windows.Graphics.PointInt32(-10000, -10000));
        _appWindow.Changed += AppWindow_Changed;

        // Hotkey
        if (!NativeMethods.RegisterHotKey(_hWnd, Constants.HOTKEY_ID, Constants.MOD_ALT, Constants.VK_S))
        {
            Trace.WriteLine("Failed to register Alt+S hotkey.");
        }

        // WndProc
        _wndProcDelegate = new WndProcDelegate(NewWndProc);
        _oldWndProc = NativeMethods.SetWindowLongPtr(_hWnd, -4, _wndProcDelegate);
        if (_oldWndProc == IntPtr.Zero)
        {
            Trace.WriteLine("Failed to set WndProc hook.");
        }
    }

    private void AppWindow_Changed(AppWindow sender, AppWindowChangedEventArgs args)
    {
        if (_hasPositioned && (args.DidPositionChange || args.DidSizeChange))
        {
            SaveWindowPosition();
        }
    }

    private IntPtr NewWndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        const uint WM_HOTKEY = 0x0312;
        const uint WM_NCLBUTTONDBLCLK = 0x00A3;

        if (msg == WM_HOTKEY && wParam.ToInt32() == Constants.HOTKEY_ID)
        {
            ToggleVisibility();
            return IntPtr.Zero;
        }
        if (msg == WM_NCLBUTTONDBLCLK) return IntPtr.Zero;

        return NativeMethods.CallWindowProc(_oldWndProc, hWnd, msg, wParam, lParam);
    }

    public void ToggleVisibility()
    {
        if (_appWindow == null) return;

        try
        {
            if (_window.Visible && _hasPositioned)
            {
                _appWindow.Hide();
            }
            else
            {
                if (!_hasPositioned)
                {
                    _hasPositioned = true;
                    if (!RestoreWindowPosition()) CenterWindow();
                }
                _appWindow.Show();
                if (NativeMethods.IsIconic(_hWnd)) NativeMethods.ShowWindow(_hWnd, NativeMethods.SW_RESTORE);
                NativeMethods.SetForegroundWindow(_hWnd);
            }
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"Failed to toggle window visibility: {ex.Message}");
        }
    }

    public void ResetPosition()
    {
        if (_appWindow == null) return;

        try
        {
            _hasPositioned = true;
            CenterWindow();
            _appWindow.Show();
            NativeMethods.SetForegroundWindow(_hWnd);
            SaveWindowPosition();
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"Failed to reset window position: {ex.Message}");
        }
    }

    public void Cleanup()
    {
        try
        {
            NativeMethods.UnregisterHotKey(_hWnd, Constants.HOTKEY_ID);
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"Error during exit: {ex.Message}");
        }
    }

    private void CenterWindow()
    {
        if (_appWindow == null) return;

        try
        {
            var displayArea = DisplayArea.GetFromWindowId(_appWindow.Id, DisplayAreaFallback.Primary);
            _appWindow.Resize(new Windows.Graphics.SizeInt32(Constants.WINDOW_WIDTH, Constants.WINDOW_HEIGHT));
            var x = (displayArea.WorkArea.Width - Constants.WINDOW_WIDTH) / 2;
            var y = (displayArea.WorkArea.Height - Constants.WINDOW_HEIGHT) / 2;
            _appWindow.Move(new Windows.Graphics.PointInt32(x, y));
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"Failed to center window: {ex.Message}");
        }
    }

    private void SaveWindowPosition()
    {
        if (_appWindow == null) return;

        try
        {
            var pos = _appWindow.Position;
            var size = _appWindow.Size;
            _positionManager.SaveWindowPosition(pos.X, pos.Y, size.Width, size.Height);
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"Failed to save window position: {ex.Message}");
        }
    }

    private bool RestoreWindowPosition()
    {
        if (_appWindow == null) return false;

        try
        {
            if (_positionManager.TryGetWindowPosition(out int x, out int y, out int w, out int h))
            {
                var rect = new Windows.Graphics.RectInt32(x, y, w, h);
                var displayArea = DisplayArea.GetFromRect(rect, DisplayAreaFallback.None);
                if (displayArea != null)
                {
                    _appWindow.MoveAndResize(rect);
                    return true;
                }
            }
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"Failed to restore window position: {ex.Message}");
        }
        return false;
    }

    public void OnActivated(WindowActivatedEventArgs args)
    {
        if (_appWindow != null && args.WindowActivationState == WindowActivationState.Deactivated)
        {
            _appWindow.Hide();
        }
    }
}
