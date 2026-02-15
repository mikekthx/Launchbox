using Launchbox.Helpers;
using Launchbox.Services;
using Launchbox.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using System;
using System.Diagnostics;
using System.IO;
using H.NotifyIcon;

namespace Launchbox;

public sealed partial class MainWindow : Window
{
    public MainViewModel ViewModel { get; }

    private ScrollViewer? _internalScrollViewer;
    private readonly WindowService _windowService;
    private readonly SettingsService _settingsService;
    private readonly IFilePickerService _filePickerService;
    private SettingsWindow? _settingsWindow;
    private TaskbarIcon? _trayIcon;

    private DateTime _lastBackdropCheck = DateTime.MinValue;
    private bool _isDwmBlurGlassRunning = false;
    private static readonly TimeSpan BackdropCheckInterval = TimeSpan.FromSeconds(60);

    // Window dragging state
    private bool _isDraggingWindow = false;
    private Windows.Graphics.PointInt32 _dragStartWindowPos;
    private Windows.Foundation.Point _dragStartPointerPos;

    public System.Windows.Input.ICommand ExitCommand { get; }
    public System.Windows.Input.ICommand OpenSettingsCommand { get; }

    public MainWindow()
    {
        var settingsStore = new LocalSettingsStore();
        var startupService = new WinUIStartupService();
        _settingsService = new SettingsService(settingsStore, startupService);
        _filePickerService = new WinUIFilePickerService();

        var windowPositionManager = new WindowPositionManager(settingsStore);
        _windowService = new WindowService(this, windowPositionManager, _settingsService);

        var fileSystem = new FileSystem();
        var shortcutService = new ShortcutService(fileSystem);
        var iconService = new IconService(fileSystem);
        var imageFactory = new WinUIImageFactory();
        var dispatcher = new WinUIDispatcher(this.DispatcherQueue);
        var launcher = new WinUILauncher();

        ViewModel = new MainViewModel(shortcutService, iconService, imageFactory, dispatcher, launcher, fileSystem, _settingsService, _windowService);

        this.InitializeComponent();
        RootGrid.DataContext = this;

        UpdateSystemBackdrop();

        ExitCommand = new SimpleCommand(ExitApplication);
        OpenSettingsCommand = new SimpleCommand(OpenSettings);

        // Initialize TaskbarIcon in code-behind to avoid WinUI 3 XAML compiler issues with out-of-tree elements
        InitializeTrayIcon();

        // 1. WINDOW SETUP
        _windowService.Initialize();

        // Initialize settings (async)
        _ = _settingsService.InitializeAsync();

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

        // 5. EVENT HOOKS
        this.Activated += MainWindow_Activated;

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

    private void MainWindow_Activated(object sender, WindowActivatedEventArgs args)
    {
        _windowService.OnActivated(args);

        // Re-check backdrop on activation in case DWMBlurGlass started after Launchbox
        if (args.WindowActivationState != WindowActivationState.Deactivated)
        {
            UpdateSystemBackdrop();
        }
    }

    private void UpdateSystemBackdrop()
    {
        try
        {
            if (DateTime.Now - _lastBackdropCheck >= BackdropCheckInterval)
            {
                _lastBackdropCheck = DateTime.Now;
                var processes = Process.GetProcessesByName("DWMBlurGlass");
                _isDwmBlurGlassRunning = processes.Length > 0;
                foreach (var p in processes)
                {
                    p.Dispose();
                }
            }

            if (_isDwmBlurGlassRunning)
            {
                // DWMBlurGlass detected, disable system backdrop to let it handle transparency
                if (this.SystemBackdrop != null)
                {
                    this.SystemBackdrop = null;
                }
            }
            else
            {
                // Default behavior
                if (this.SystemBackdrop is not DesktopAcrylicBackdrop)
                {
                    this.SystemBackdrop = new DesktopAcrylicBackdrop();
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error checking for DWMBlurGlass: {ex.Message}");
            // Fallback to default
            if (this.SystemBackdrop is not DesktopAcrylicBackdrop)
            {
                this.SystemBackdrop = new DesktopAcrylicBackdrop();
            }
        }
    }

    private void OpenSettings()
    {
        if (_settingsWindow != null)
        {
            _settingsWindow.Activate();
            return;
        }

        try
        {
            _settingsWindow = new SettingsWindow(_settingsService, _windowService, _filePickerService);
            _settingsWindow.Closed += (s, e) => _settingsWindow = null;
            _settingsWindow.Activate();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error opening settings: {ex.Message}");
        }
    }

    private void InitializeTrayIcon()
    {
        _trayIcon = new TaskbarIcon
        {
            ToolTipText = "Launchbox (Alt+S)",
            IconSource = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage(new Uri("ms-appx:///Assets/Square44x44Logo.targetsize-24_altform-unplated.png"))
        };

        _trayIcon.LeftClickCommand = ViewModel.ToggleWindowCommand;
        _trayIcon.DoubleClickCommand = ViewModel.ToggleWindowCommand;

        var flyout = new MenuFlyout();

        var showItem = new MenuFlyoutItem { Text = "Show", Command = ViewModel.ToggleWindowCommand };
        var settingsItem = new MenuFlyoutItem { Text = "Settings", Command = OpenSettingsCommand };
        var exitItem = new MenuFlyoutItem { Text = "Exit", Command = ExitCommand };

        flyout.Items.Add(showItem);
        flyout.Items.Add(settingsItem);
        flyout.Items.Add(new MenuFlyoutSeparator());
        flyout.Items.Add(exitItem);

        _trayIcon.ContextFlyout = flyout;

        // Add to the visual tree to ensure proper lifecycle management
        RootGrid.Children.Add(_trayIcon);
    }

    private void ExitApplication()
    {
        _windowService.Cleanup();
        _trayIcon?.Dispose();
        this.Close();
    }

    private void AppGrid_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (ViewModel.LaunchAppCommand.CanExecute(e.ClickedItem))
        {
            ViewModel.LaunchAppCommand.Execute(e.ClickedItem);
        }
    }
}
