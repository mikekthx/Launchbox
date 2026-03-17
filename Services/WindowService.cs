using Launchbox;
using Launchbox.Helpers;
using Microsoft.UI.Dispatching;
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
    private readonly IFilePickerService _filePickerService;
    private readonly IDispatcher _dispatcher;
    private AppWindow? _appWindow;
    private IntPtr _hWnd;
    private IntPtr _oldWndProc;
    private WndProcDelegate? _wndProcDelegate;
    // True once the window has been shown at a real screen position (not the initial -10000,-10000).
    // Prevents treating the off-screen start position as a valid saved position.
    private bool _hasPositioned;
    private SettingsWindow? _settingsWindow;
    private int _currentMod;
    private int _currentKey;
    private bool _isHotkeyRegistered;
    private DispatcherQueueTimer? _savePositionTimer;

    public bool IsVisible => _window.Visible;

    public event EventHandler<bool>? VisibilityChanged;
    public event EventHandler<string>? HotkeyRegistrationFailed;

    public WindowService(Window window, WindowPositionManager positionManager, SettingsService settingsService, IFilePickerService filePickerService, IDispatcher dispatcher)
    {
        ArgumentNullException.ThrowIfNull(window);
        ArgumentNullException.ThrowIfNull(positionManager);
        ArgumentNullException.ThrowIfNull(settingsService);
        ArgumentNullException.ThrowIfNull(filePickerService);
        ArgumentNullException.ThrowIfNull(dispatcher);

        _window = window;
        _positionManager = positionManager;
        _settingsService = settingsService;
        _filePickerService = filePickerService;
        _dispatcher = dispatcher;

        _window.VisibilityChanged += Window_VisibilityChanged;
        _settingsService.PropertyChanged += SettingsService_PropertyChanged;
    }

    private void Window_VisibilityChanged(object sender, WindowVisibilityChangedEventArgs args)
    {
        VisibilityChanged?.Invoke(this, args.Visible);
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

        // Debounce timer for window position persistence — saves after 500ms of no movement
        _savePositionTimer = _window.DispatcherQueue.CreateTimer();
        _savePositionTimer.Interval = TimeSpan.FromMilliseconds(500);
        _savePositionTimer.IsRepeating = false;
        _savePositionTimer.Tick += (_, _) => SaveWindowPosition();

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
            // RegisterHotKey is thread-affine and must be called on the thread that created the window.
            _dispatcher.TryEnqueue(() => UpdateHotkey());
        }
    }

    private void UpdateHotkey()
    {
        int mod = _settingsService.HotkeyModifiers;
        int key = _settingsService.HotkeyKey;

        // Unregister existing first
        NativeMethods.UnregisterHotKey(_hWnd, Constants.HOTKEY_ID);

        if (!NativeMethods.RegisterHotKey(_hWnd, Constants.HOTKEY_ID, (uint)mod, (uint)key))
        {
            var errorMessage = $"Failed to register hotkey: {mod}+{key}";
            Trace.WriteLine(errorMessage);
            HotkeyRegistrationFailed?.Invoke(this, errorMessage);

            if (_isHotkeyRegistered)
            {
                if (NativeMethods.RegisterHotKey(_hWnd, Constants.HOTKEY_ID, (uint)_currentMod, (uint)_currentKey))
                {
                    Trace.WriteLine($"Restored previous hotkey: {_currentMod}+{_currentKey}");
                }
                else
                {
                    Trace.WriteLine($"Failed to restore previous hotkey: {_currentMod}+{_currentKey}");
                    _isHotkeyRegistered = false;
                }
            }
        }
        else
        {
            Trace.WriteLine($"Registered hotkey: {mod}+{key}");
            _currentMod = mod;
            _currentKey = key;
            _isHotkeyRegistered = true;
        }
    }

    private void AppWindow_Changed(AppWindow sender, AppWindowChangedEventArgs args)
    {
        if (_hasPositioned && (args.DidPositionChange || args.DidSizeChange))
        {
            // Reset the debounce timer — only persists after 500ms of no movement
            _savePositionTimer?.Stop();
            _savePositionTimer?.Start();
        }
    }

    private IntPtr NewWndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        if (msg == NativeMethods.WM_HOTKEY && wParam.ToInt32() == Constants.HOTKEY_ID)
        {
            ToggleVisibility();
            return IntPtr.Zero;
        }
        // Suppress: prevents the default double-click-title-bar maximize behavior.
        // Launchbox has no title bar and maximizing would break the layout.
        if (msg == NativeMethods.WM_NCLBUTTONDBLCLK) return IntPtr.Zero;

        return NativeMethods.CallWindowProc(_oldWndProc, hWnd, msg, wParam, lParam);
    }

    /// <summary>
    /// Toggles the main window's visibility, restoring its previous position or centering it if displayed for the first time.
    /// Also restores the window state if it was minimized (iconic) and brings it to the foreground.
    /// </summary>
    public void ToggleVisibility()
    {
        if (_appWindow == null) return;

        try
        {
            bool isWindowCurrentlyVisible = _window.Visible && _hasPositioned;

            if (isWindowCurrentlyVisible)
            {
                _appWindow.Hide();
                return;
            }

            if (!_hasPositioned)
            {
                _hasPositioned = true;
                bool positionRestored = RestoreWindowPosition();

                if (!positionRestored)
                {
                    CenterWindow();
                }
            }

            ClampToWorkArea();
            _appWindow.Show();

            if (NativeMethods.IsIconic(_hWnd))
            {
                NativeMethods.ShowWindow(_hWnd, NativeMethods.SW_RESTORE);
            }

            NativeMethods.SetForegroundWindow(_hWnd);
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"Failed to toggle window visibility: {PathSecurity.GetSafeExceptionMessage(ex)}");
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
            Trace.WriteLine($"Failed to hide window: {PathSecurity.GetSafeExceptionMessage(ex)}");
        }
    }

    public void Exit()
    {
        _window.Close();
    }

    private void SettingsWindow_Closed(object sender, WindowEventArgs args)
    {
        if (_settingsWindow != null)
        {
            _settingsWindow.Closed -= SettingsWindow_Closed;
            _settingsWindow = null;
        }
    }

    public void OpenSettings()
    {
        if (_settingsWindow != null)
        {
            _settingsWindow.Activate();
            return;
        }

        try
        {
            _settingsWindow = new SettingsWindow(_settingsService, this, _filePickerService);
            _settingsWindow.Closed += SettingsWindow_Closed;
            _settingsWindow.Activate();
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"Error opening settings: {PathSecurity.GetSafeExceptionMessage(ex)}");
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
            Trace.WriteLine($"Failed to reset window position: {PathSecurity.GetSafeExceptionMessage(ex)}");
        }
    }

    private bool _disposed;

    // Finalizer to clean up unmanaged resources
    ~WindowService()
    {
        Dispose(false);
    }

    public void Dispose()
    {
        if (_disposed) return;
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;

        if (disposing)
        {
            // Unsubscribe managed events
            _window.VisibilityChanged -= Window_VisibilityChanged;
            _settingsService.PropertyChanged -= SettingsService_PropertyChanged;

            if (_appWindow != null)
            {
                _appWindow.Changed -= AppWindow_Changed;
            }

            if (_savePositionTimer != null)
            {
                if (_savePositionTimer.IsRunning)
                {
                    _savePositionTimer.Stop();
                    SaveWindowPosition();
                }
                _savePositionTimer = null;
            }

            if (_settingsWindow != null)
            {
                _settingsWindow.Closed -= SettingsWindow_Closed;
                _settingsWindow.Close();
                _settingsWindow = null;
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
            Trace.WriteLine($"Error during exit: {PathSecurity.GetSafeExceptionMessage(ex)}");
        }

        _disposed = true;
    }

    private void CenterWindow()
    {
        if (_appWindow == null) return;

        try
        {
            var displayArea = DisplayArea.GetFromWindowId(_appWindow.Id, DisplayAreaFallback.Primary);
            int height = Math.Min(Constants.WINDOW_HEIGHT, displayArea.WorkArea.Height - 40);
            _appWindow.Resize(new Windows.Graphics.SizeInt32(Constants.WINDOW_WIDTH, height));
            var x = (displayArea.WorkArea.Width - Constants.WINDOW_WIDTH) / 2;
            var y = (displayArea.WorkArea.Height - height) / 2;
            _appWindow.Move(new Windows.Graphics.PointInt32(x, y));
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"Failed to center window: {PathSecurity.GetSafeExceptionMessage(ex)}");
        }
    }

    /// <summary>
    /// Ensures the window fits within the current display's work area,
    /// shrinking its height if needed (e.g., small screen at high DPI scaling).
    /// </summary>
    private void ClampToWorkArea()
    {
        if (_appWindow == null) return;

        try
        {
            var displayArea = DisplayArea.GetFromWindowId(_appWindow.Id, DisplayAreaFallback.Primary);
            var size = _appWindow.Size;
            int maxHeight = displayArea.WorkArea.Height - 40;

            if (size.Height > maxHeight)
            {
                _appWindow.Resize(new Windows.Graphics.SizeInt32(size.Width, maxHeight));
            }
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"Failed to clamp window to work area: {PathSecurity.GetSafeExceptionMessage(ex)}");
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
            Trace.WriteLine($"Failed to save window position: {PathSecurity.GetSafeExceptionMessage(ex)}");
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
                // Fallback.None returns null if the saved position is off all connected displays
                // (e.g., after disconnecting a monitor). We detect this and center the window instead.
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
            Trace.WriteLine($"Failed to restore window position: {PathSecurity.GetSafeExceptionMessage(ex)}");
        }
        return false;
    }

    public void OnActivated(WindowActivatedEventArgs args)
    {
        if (_appWindow != null && args.WindowActivationState == WindowActivationState.Deactivated)
        {
            // Flush any pending debounced save before hiding
            if (_savePositionTimer != null && _savePositionTimer.IsRunning)
            {
                _savePositionTimer.Stop();
                SaveWindowPosition();
            }
            _appWindow.Hide();
        }
    }
}
