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
using System.Threading.Tasks;

namespace Launchbox;

public sealed partial class MainWindow : Window
{
    public MainViewModel ViewModel { get; }

    private ScrollViewer? _internalScrollViewer;
    private readonly IWindowService _windowService;
    private readonly SettingsService _settingsService;
    private readonly IFilePickerService _filePickerService;
    private readonly IBackdropService _backdropService;

    // Window dragging state
    private bool _isDraggingWindow = false;
    private Windows.Graphics.PointInt32 _dragStartWindowPos;
    private Windows.Foundation.Point _dragStartPointerPos;

    public MainWindow()
    {
        var settingsStore = new LocalSettingsStore();
        var startupService = new WinUIStartupService();
        _settingsService = new SettingsService(settingsStore, startupService);
        _filePickerService = new WinUIFilePickerService();

        var windowPositionManager = new WindowPositionManager(settingsStore);
        _windowService = new WindowService(this, windowPositionManager, _settingsService, _filePickerService);

        var fileSystem = new FileSystem();
        var shortcutService = new ShortcutService(fileSystem);
        var iconService = new IconService(fileSystem);
        var imageFactory = new WinUIImageFactory();
        var dispatcher = new WinUIDispatcher(this.DispatcherQueue);
        var shortcutResolver = new WindowsShortcutResolver(fileSystem);
        var processStarter = new ProcessStarter();
        var launcher = new WinUILauncher(shortcutResolver, processStarter, fileSystem);

        ViewModel = new MainViewModel(shortcutService, iconService, imageFactory, dispatcher, launcher, fileSystem, _settingsService, _windowService);

        var processService = new ProcessService();
        var backdropWrapper = new BackdropWindowWrapper(this);
        _backdropService = new BackdropService(processService, backdropWrapper);

        this.InitializeComponent();
        RootGrid.DataContext = this;

        _ = _backdropService.UpdateBackdropAsync();

        // 1. WINDOW SETUP
        _windowService.Initialize();

        // Initialize settings (async)
        _ = _settingsService.InitializeAsync();

        // 2. WINDOW DRAGGING - Use custom pointer tracking
        RootGrid.PointerPressed += RootGrid_PointerPressed;
        RootGrid.PointerMoved += RootGrid_PointerMoved;
        RootGrid.PointerReleased += RootGrid_PointerReleased;
        RootGrid.PointerCanceled += RootGrid_PointerReleased;
        RootGrid.PointerCaptureLost += RootGrid_PointerCaptureLost;

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
        this.Closed += MainWindow_Closed;

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
        var scale = RootGrid.XamlRoot?.RasterizationScale ?? 1.0;
        var deltaX = (int)((currentPointerPos.X - _dragStartPointerPos.X) * scale);
        var deltaY = (int)((currentPointerPos.Y - _dragStartPointerPos.Y) * scale);

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

    private void RootGrid_PointerCaptureLost(object sender, PointerRoutedEventArgs e)
    {
        _isDraggingWindow = false;
        e.Handled = true;
    }

    private void MainWindow_Activated(object sender, WindowActivatedEventArgs args)
    {
        _windowService.OnActivated(args);

        // Re-check backdrop on activation in case DWMBlurGlass started after Launchbox
        if (args.WindowActivationState != WindowActivationState.Deactivated)
        {
            _ = _backdropService.UpdateBackdropAsync();
        }
    }

    private void MainWindow_Closed(object sender, WindowEventArgs args)
    {
        _windowService.Cleanup();
        TrayIcon?.Dispose();
        ViewModel?.Dispose();
    }

    private void AppGrid_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (ViewModel.LaunchAppCommand.CanExecute(e.ClickedItem))
        {
            ViewModel.LaunchAppCommand.Execute(e.ClickedItem);
        }
    }
}
