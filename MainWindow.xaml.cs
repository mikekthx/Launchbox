using Launchbox.Services;
using Launchbox.ViewModels;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace Launchbox;

public sealed partial class MainWindow : Window
{
    private readonly string _shortcutFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "Shortcuts");

    public MainViewModel ViewModel { get; }

    private static WndProcDelegate? _wndProcDelegate;
    private readonly IntPtr _oldWndProc;
    private bool _hasPositioned = false;
    private ScrollViewer? _internalScrollViewer;
    private readonly WindowPositionManager _windowPositionManager;

    // Window dragging state
    private bool _isDraggingWindow = false;
    private Windows.Graphics.PointInt32 _dragStartWindowPos;
    private Windows.Foundation.Point _dragStartPointerPos;

    public System.Windows.Input.ICommand ToggleWindowCommand { get; }
    public System.Windows.Input.ICommand ExitCommand { get; }
    public System.Windows.Input.ICommand ResetPositionCommand { get; }

    public MainWindow()
    {
        this.InitializeComponent();

        var settingsStore = new LocalSettingsStore();
        _windowPositionManager = new WindowPositionManager(settingsStore);
        var fileSystem = new FileSystem();
        var shortcutService = new ShortcutService(fileSystem);
        var iconService = new IconService(fileSystem);
        var imageFactory = new WinUIImageFactory();
        var dispatcher = new WinUIDispatcher(this.DispatcherQueue);
        var launcher = new WinUILauncher();

        ViewModel = new MainViewModel(shortcutService, iconService, imageFactory, dispatcher, launcher, _shortcutFolder);

        ToggleWindowCommand = new SimpleCommand(ToggleWindowVisibility);
        ExitCommand = new SimpleCommand(ExitApplication);
        ResetPositionCommand = new SimpleCommand(ResetWindowPosition);

        // 1. WINDOW SETUP
        this.ExtendsContentIntoTitleBar = true;
        this.AppWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Collapsed;
        this.AppWindow.IsShownInSwitchers = false;

        // 2. WINDOW DRAGGING - Use custom pointer tracking
        RootGrid.PointerPressed += RootGrid_PointerPressed;
        RootGrid.PointerMoved += RootGrid_PointerMoved;
        RootGrid.PointerReleased += RootGrid_PointerReleased;

        // 3. SCROLL SHOULD WORK NATIVELY NOW
        AppGrid.Loaded += (s, e) =>
        {
            var finder = new VisualTreeFinder(new WinUIVisualTreeService());
            _internalScrollViewer = finder.FindFirstDescendant<ScrollViewer>(AppGrid);
            if (_internalScrollViewer != null)
            {
                _internalScrollViewer.VerticalScrollMode = ScrollMode.Enabled;
                _internalScrollViewer.VerticalScrollBarVisibility = ScrollBarVisibility.Hidden;
                Debug.WriteLine($"ScrollViewer found! Scrollable height: {_internalScrollViewer.ScrollableHeight}");
            }
            Debug.WriteLine($"AppGrid loaded. Scrollable height: {_internalScrollViewer?.ScrollableHeight ?? 0}");
        };

        // 4. START OFF-SCREEN
        IntPtr hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hWnd);
        var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);

        appWindow.Resize(new Windows.Graphics.SizeInt32(Constants.WINDOW_WIDTH, Constants.WINDOW_HEIGHT));
        appWindow.Move(new Windows.Graphics.PointInt32(-10000, -10000));
        appWindow.Changed += AppWindow_Changed;

        // 5. EVENT HOOKS
        this.Activated += MainWindow_Activated;

        if (!RegisterHotKey(hWnd, Constants.HOTKEY_ID, Constants.MOD_ALT, Constants.VK_S))
        {
            Trace.WriteLine("Failed to register Alt+S hotkey.");
        }

        _wndProcDelegate = new WndProcDelegate(NewWndProc);
        _oldWndProc = SetWindowLongPtr(hWnd, -4, _wndProcDelegate);
        if (_oldWndProc == IntPtr.Zero)
        {
            Trace.WriteLine("Failed to set WndProc hook.");
        }

        // 6. LOAD APPS
        AppGrid.ItemsSource = ViewModel.Apps;
        if (ViewModel.LoadAppsCommand.CanExecute(null))
        {
             ViewModel.LoadAppsCommand.Execute(null);
        }
    }

    // --- WINDOW DRAGGING ---
    private void RootGrid_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        // Check if we're clicking inside a GridViewItem
        var clickedElement = e.OriginalSource as DependencyObject;
        if (clickedElement != null)
        {
            var parent = clickedElement;
            int depth = 0;
            while (parent != null && parent != RootGrid && depth < 20)
            {
                if (parent is GridViewItem)
                {
                    // Clicked on an item, don't start dragging
                    return;
                }
                parent = VisualTreeHelper.GetParent(parent);
                depth++;
            }
        }

        // Start dragging - capture pointer and record initial positions
        _isDraggingWindow = true;
        _dragStartWindowPos = this.AppWindow.Position;
        _dragStartPointerPos = e.GetCurrentPoint(null).Position;
        RootGrid.CapturePointer(e.Pointer);
        e.Handled = true;
    }

    private void RootGrid_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (!_isDraggingWindow) return;

        var currentPointerPos = e.GetCurrentPoint(null).Position;
        var deltaX = (int)(currentPointerPos.X - _dragStartPointerPos.X);
        var deltaY = (int)(currentPointerPos.Y - _dragStartPointerPos.Y);

        var newX = _dragStartWindowPos.X + deltaX;
        var newY = _dragStartWindowPos.Y + deltaY;

        this.AppWindow.Move(new Windows.Graphics.PointInt32(newX, newY));
        e.Handled = true;
    }

    private void RootGrid_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (_isDraggingWindow)
        {
            _isDraggingWindow = false;
            RootGrid.ReleasePointerCapture(e.Pointer);
            e.Handled = true;
        }
    }

    // --- WINDOW LOGIC ---
    private void AppWindow_Changed(AppWindow sender, AppWindowChangedEventArgs args)
    {
        if (_hasPositioned && (args.DidPositionChange || args.DidSizeChange))
        {
            SaveWindowPosition();
        }
    }

    private void MainWindow_Activated(object sender, WindowActivatedEventArgs args)
    {
        if (args.WindowActivationState == WindowActivationState.Deactivated)
        {
            this.AppWindow.Hide();
        }
    }

    private void ExitApplication()
    {
        try
        {
            IntPtr hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            UnregisterHotKey(hWnd, Constants.HOTKEY_ID);
            TrayIcon?.Dispose();
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"Error during exit: {ex.Message}");
        }
        this.Close();
    }

    private void ResetWindowPosition()
    {
        try
        {
            _hasPositioned = true;
            CenterWindow();
            this.AppWindow.Show();
            IntPtr hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            SetForegroundWindow(hWnd);
            SaveWindowPosition();
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"Failed to reset window position: {ex.Message}");
        }
    }

    private void SaveWindowPosition()
    {
        try
        {
            var pos = this.AppWindow.Position;
            var size = this.AppWindow.Size;
            _windowPositionManager.SaveWindowPosition(pos.X, pos.Y, size.Width, size.Height);
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"Failed to save window position: {ex.Message}");
        }
    }

    private bool RestoreWindowPosition()
    {
        try
        {
            if (_windowPositionManager.TryGetWindowPosition(out int x, out int y, out int w, out int h))
            {
                var rect = new Windows.Graphics.RectInt32(x, y, w, h);
                var displayArea = DisplayArea.GetFromRect(rect, DisplayAreaFallback.None);
                if (displayArea != null)
                {
                    this.AppWindow.MoveAndResize(rect);
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

    private void ToggleWindowVisibility()
    {
        try
        {
            if (this.Visible && _hasPositioned)
            {
                this.AppWindow.Hide();
            }
            else
            {
                if (!_hasPositioned)
                {
                    _hasPositioned = true;
                    if (!RestoreWindowPosition()) CenterWindow();
                }
                this.AppWindow.Show();
                IntPtr hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
                if (IsIconic(hWnd)) ShowWindow(hWnd, SW_RESTORE);
                SetForegroundWindow(hWnd);
            }
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"Failed to toggle window visibility: {ex.Message}");
        }
    }

    private void CenterWindow()
    {
        try
        {
            IntPtr hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hWnd);
            var displayArea = DisplayArea.GetFromWindowId(windowId, DisplayAreaFallback.Primary);
            var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);
            appWindow.Resize(new Windows.Graphics.SizeInt32(Constants.WINDOW_WIDTH, Constants.WINDOW_HEIGHT));
            var x = (displayArea.WorkArea.Width - Constants.WINDOW_WIDTH) / 2;
            var y = (displayArea.WorkArea.Height - Constants.WINDOW_HEIGHT) / 2;
            appWindow.Move(new Windows.Graphics.PointInt32(x, y));
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"Failed to center window: {ex.Message}");
        }
    }

    private delegate IntPtr WndProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    private IntPtr NewWndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        const uint WM_HOTKEY = 0x0312;
        const uint WM_NCLBUTTONDBLCLK = 0x00A3;

        if (msg == WM_HOTKEY && wParam.ToInt32() == Constants.HOTKEY_ID)
        {
            ToggleWindowVisibility();
            return IntPtr.Zero;
        }
        if (msg == WM_NCLBUTTONDBLCLK) return IntPtr.Zero;

        return CallWindowProc(_oldWndProc, hWnd, msg, wParam, lParam);
    }

    private void AppGrid_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (ViewModel.LaunchAppCommand.CanExecute(e.ClickedItem))
        {
            ViewModel.LaunchAppCommand.Execute(e.ClickedItem);
            this.AppWindow.Hide();
        }
    }

    // --- WIN32 IMPORTS ---
    private const int SW_RESTORE = 9; // Used in ToggleWindowVisibility

    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern bool IsIconic(IntPtr hWnd);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr CallWindowProc(IntPtr lpPrevWndFunc, IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr", CharSet = CharSet.Unicode)] private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, WndProcDelegate dwNewLong);
    [DllImport("user32.dll", EntryPoint = "SetWindowLong", CharSet = CharSet.Unicode)] private static extern IntPtr SetWindowLong32(IntPtr hWnd, int nIndex, WndProcDelegate dwNewLong);

    private static IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, WndProcDelegate dwNewLong)
    {
        if (IntPtr.Size == 8) return SetWindowLongPtr64(hWnd, nIndex, dwNewLong);
        else return SetWindowLong32(hWnd, nIndex, dwNewLong);
    }
}
