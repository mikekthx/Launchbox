using Launchbox.Helpers;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using System;
using System.Diagnostics;

namespace Launchbox.Services;

public class WindowService : IWindowService, IDisposable
{
    private readonly Window _window;
    private readonly WindowPositionManager _positionManager;
    private readonly SettingsService _settingsService;
    private AppWindow? _appWindow;
    private IntPtr _hWnd;
    private IntPtr _oldWndProc;
    private WndProcDelegate? _wndProcDelegate;
    private bool _hasPositioned;

    public WindowService(Window window, WindowPositionManager positionManager, SettingsService settingsService)
    {
        _window = window;
        _positionManager = positionManager;
        _settingsService = settingsService;

        _settingsService.PropertyChanged += SettingsService_PropertyChanged;
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
        UpdateHotkey();

        // WndProc
        _wndProcDelegate = NewWndProc;
        _oldWndProc = NativeMethods.SetWindowLongPtr(_hWnd, NativeMethods.GWLP_WNDPROC, _wndProcDelegate);
        if (_oldWndProc == IntPtr.Zero)
        {
            Trace.WriteLine("Failed to set WndProc hook.");
        }
    }

    private void SettingsService_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SettingsService.HotkeyModifiers) ||
            e.PropertyName == nameof(SettingsService.HotkeyKey))
        {
            // Update on UI thread if needed? RegisterHotKey is thread-affine?
            // Usually RegisterHotKey must be called on the thread that created the window.
            // PropertyChanged might come from any thread?
            // SettingsService.PropertyChanged likely from UI thread if set from UI.
            // If set from background (e.g. startup check), hotkey not involved.
            // Assuming UI thread for now.
            UpdateHotkey();
        }
    }

    private void UpdateHotkey()
    {
        // Unregister existing first
        NativeMethods.UnregisterHotKey(_hWnd, Constants.HOTKEY_ID);

        int mod = _settingsService.HotkeyModifiers;
        int key = _settingsService.HotkeyKey;

        Trace.WriteLine(!NativeMethods.RegisterHotKey(_hWnd, Constants.HOTKEY_ID, (uint)mod, (uint)key)
            ? $"Failed to register hotkey: {mod}+{key}"
            : $"Registered hotkey: {mod}+{key}");
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
        if (msg == NativeMethods.WM_HOTKEY && wParam.ToInt32() == Constants.HOTKEY_ID)
        {
            ToggleVisibility();
            return IntPtr.Zero;
        }
        if (msg == NativeMethods.WM_NCLBUTTONDBLCLK) return IntPtr.Zero;

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

    public void Hide()
    {
        if (_appWindow == null) return;
        try
        {
            _appWindow.Hide();
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"Failed to hide window: {ex.Message}");
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
        Dispose();
    }

    private bool _disposed;

    ~WindowService()
    {
        Dispose(false);
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;

        if (disposing)
        {
            // Unsubscribe managed events
            _settingsService.PropertyChanged -= SettingsService_PropertyChanged;

            if (_appWindow != null)
            {
                _appWindow.Changed -= AppWindow_Changed;
            }
        }

        // Clean up unmanaged resources
        try
        {
            NativeMethods.UnregisterHotKey(_hWnd, Constants.HOTKEY_ID);

            if (_oldWndProc != IntPtr.Zero)
            {
                NativeMethods.SetWindowLongPtr(_hWnd, NativeMethods.GWLP_WNDPROC, _oldWndProc);
                _oldWndProc = IntPtr.Zero;
            }
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"Error during exit: {ex.Message}");
        }

        _disposed = true;
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
