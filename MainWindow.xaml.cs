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

namespace Launchbox;

public sealed partial class MainWindow : Window
{
    private readonly string _shortcutFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "Shortcuts");

    public MainViewModel ViewModel { get; }

    private ScrollViewer? _internalScrollViewer;
    private readonly WindowService _windowService;

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

        try
        {
            if (Process.GetProcessesByName("DWMBlurGlass").Length > 0)
            {
                // DWMBlurGlass detected, disable system backdrop to let it handle transparency
                this.SystemBackdrop = null;
            }
            else
            {
                // Default behavior
                this.SystemBackdrop = new DesktopAcrylicBackdrop();
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error checking for DWMBlurGlass: {ex.Message}");
            // Fallback to default
            this.SystemBackdrop = new DesktopAcrylicBackdrop();
        }

        var settingsStore = new LocalSettingsStore();
        var windowPositionManager = new WindowPositionManager(settingsStore);
        _windowService = new WindowService(this, windowPositionManager);

        var fileSystem = new FileSystem();
        var shortcutService = new ShortcutService(fileSystem);
        var iconService = new IconService(fileSystem);
        var imageFactory = new WinUIImageFactory();
        var dispatcher = new WinUIDispatcher(this.DispatcherQueue);
        var launcher = new WinUILauncher();

        ViewModel = new MainViewModel(shortcutService, iconService, imageFactory, dispatcher, launcher, _shortcutFolder);

        ToggleWindowCommand = new SimpleCommand(() => _windowService.ToggleVisibility());
        ExitCommand = new SimpleCommand(ExitApplication);
        ResetPositionCommand = new SimpleCommand(() => _windowService.ResetPosition());

        // 1. WINDOW SETUP
        _windowService.Initialize();

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
    }

    private void ExitApplication()
    {
        _windowService.Cleanup();
        TrayIcon?.Dispose();
        this.Close();
    }

    private void AppGrid_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (ViewModel.LaunchAppCommand.CanExecute(e.ClickedItem))
        {
            ViewModel.LaunchAppCommand.Execute(e.ClickedItem);
            this.AppWindow.Hide();
        }
    }
}
