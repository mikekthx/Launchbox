using Launchbox;
using Launchbox.Helpers;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

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
    // Suppresses position persistence during display-time clamping so that a
    // temporarily reduced size on a small display is not saved as the user's intent.
    private bool _suppressSave;

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

        // WndProc — delegate stored in _wndProcDelegate field to satisfy GC-root requirement;
        // see NativeMethods.SetWindowLongPtr(WndProcDelegate) remarks.
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
        if (_suppressSave) return;

        if (_hasPositioned && (args.DidPositionChange || args.DidSizeChange))
        {
            // Reset the debounce timer — only persists after 500ms of no movement
            _savePositionTimer?.Stop();
            _savePositionTimer?.Start();
        }
    }

    private IntPtr NewWndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        if (msg == NativeMethods.WM_GETMINMAXINFO)
        {
            var mmi = Marshal.PtrToStructure<NativeMethods.MINMAXINFO>(lParam);
            mmi.ptMinTrackSize.X = Constants.MIN_WINDOW_WIDTH;
            mmi.ptMinTrackSize.Y = Constants.MIN_WINDOW_HEIGHT;
            Marshal.StructureToPtr(mmi, lParam, false);
            return IntPtr.Zero;
        }
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
    /// When KeepCentered is enabled, re-centers on every show using the current window size.
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

            // Set unconditionally BEFORE any positioning path — required for hide toggle to work
            bool firstShow = !_hasPositioned;
            _hasPositioned = true;

            if (_settingsService.KeepCentered)
            {
                if (firstShow)
                {
                    // First show: restore saved size (not position), then center at that size
                    RestoreWindowPosition();
                }
                CenterOnCurrentDisplay();
            }
            else if (firstShow)
            {
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

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

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

        // UnregisterHotKey and SetWindowLongPtr are thread-affine Win32 APIs that must
        // run on the window-owning thread. They belong here in Dispose() (called on the
        // UI thread), not in a finalizer which runs on the GC thread.
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
    }

    private void CenterWindow()
    {
        if (_appWindow == null) return;

        try
        {
            var displayArea = DisplayArea.GetFromWindowId(_appWindow.Id, DisplayAreaFallback.Primary);
            int height = Math.Min(Constants.WINDOW_HEIGHT, displayArea.WorkArea.Height - 40);
            _appWindow.Resize(new Windows.Graphics.SizeInt32(Constants.WINDOW_WIDTH, height));
            CenterOnCurrentDisplay();
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"Failed to center window: {PathSecurity.GetSafeExceptionMessage(ex)}");
        }
    }

    private void CenterOnCurrentDisplay()
    {
        if (_appWindow == null) return;

        try
        {
            var displayArea = DisplayArea.GetFromWindowId(_appWindow.Id, DisplayAreaFallback.Primary);
            var currentSize = _appWindow.Size;
            // Add display origin so centering works on secondary monitors (non-zero WorkArea.X/Y)
            var x = displayArea.WorkArea.X + (displayArea.WorkArea.Width - currentSize.Width) / 2;
            var y = displayArea.WorkArea.Y + (displayArea.WorkArea.Height - currentSize.Height) / 2;
            _appWindow.Move(new Windows.Graphics.PointInt32(x, y));
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"Failed to center window on display: {PathSecurity.GetSafeExceptionMessage(ex)}");
        }
    }

    /// <summary>
    /// Ensures the window fits within the current display's work area.
    /// Shrinks height and width if needed and repositions if the window is off-screen horizontally
    /// (e.g., after a monitor disconnect). Clamped changes are display-time only and
    /// are not persisted, so the user's preferred size restores on a larger display.
    /// </summary>
    private void ClampToWorkArea()
    {
        if (_appWindow == null) return;

        try
        {
            var displayArea = DisplayArea.GetFromWindowId(_appWindow.Id, DisplayAreaFallback.Primary);
            var workArea = displayArea.WorkArea;
            var pos = _appWindow.Position;
            var size = _appWindow.Size;

            int maxHeight = workArea.Height - 40;
            bool needsResize = size.Height > maxHeight;
            int clampedHeight = needsResize ? maxHeight : size.Height;

            int maxWidth = workArea.Width - 40;
            bool needsWidthClamp = size.Width > maxWidth;
            int clampedWidth = needsWidthClamp ? maxWidth : size.Width;
            needsResize = needsResize || needsWidthClamp;

            // Check if the window is entirely off-screen on either axis
            bool offScreenHorizontally = pos.X >= workArea.X + workArea.Width
                                         || pos.X + clampedWidth <= workArea.X;
            bool offScreenVertically = pos.Y >= workArea.Y + workArea.Height
                                       || pos.Y + clampedHeight <= workArea.Y;

            if (!needsResize && !offScreenHorizontally && !offScreenVertically) return;

            ApplyClampedSizeAndPosition();

            void ApplyClampedSizeAndPosition()
            {
                _suppressSave = true;
                try
                {
                    if (needsResize)
                    {
                        _appWindow.Resize(new Windows.Graphics.SizeInt32(clampedWidth, clampedHeight));
                    }

                    int newX = pos.X;
                    int newY = pos.Y;
                    bool needsMove = false;

                    if (offScreenHorizontally)
                    {
                        newX = workArea.X + (workArea.Width - clampedWidth) / 2;
                        needsMove = true;
                    }

                    if (offScreenVertically)
                    {
                        newY = workArea.Y + (workArea.Height - clampedHeight) / 2;
                        needsMove = true;
                    }

                    if (needsMove)
                    {
                        _appWindow.Move(new Windows.Graphics.PointInt32(newX, newY));
                    }
                }
                finally
                {
                    _suppressSave = false;
                }
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

    public void OnActivated(bool isDeactivated)
    {
        if (_appWindow != null && isDeactivated)
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
