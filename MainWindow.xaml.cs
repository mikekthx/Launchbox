using Launchbox.Helpers;
using Launchbox.Models;
using Launchbox.Services;
using Launchbox.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.System;

namespace Launchbox;

public sealed partial class MainWindow : Window
{
    public MainViewModel ViewModel { get; }

    private readonly IWindowService _windowService;
    private readonly SettingsService _settingsService;
    private readonly IFilePickerService _filePickerService;
    private readonly IBackdropService _backdropService;

    // Window dragging state — uses screen-relative cursor coordinates (via GetCursorPos)
    // to avoid drift caused by window-relative coordinates shifting after AppWindow.Move.
    private bool _isDraggingWindow = false;
    private Windows.Graphics.PointInt32 _dragStartWindowPos;
    private NativeMethods.POINT _dragStartCursorPos;

    // Set when the window transitions from hidden to visible (Alt+S show path).
    // Consumed in MainWindow_Activated to gate filter-clear and SearchBox focus.
    // Without this, every focus-regain (e.g., returning from a dialog) would silently
    // discard the user's active search filter.
    private bool _freshShow;

    public MainWindow()
    {
        var settingsStore = new LocalSettingsStore();
        var startupService = new WinUIStartupService();
        var folderManager = new ShortcutFolderManager(settingsStore);
        _settingsService = new SettingsService(settingsStore, startupService, folderManager);
        _filePickerService = new WinUIFilePickerService();

        var dispatcher = new WinUIDispatcher(this.DispatcherQueue);
        var windowPositionManager = new WindowPositionManager(settingsStore);
        _windowService = new WindowService(this, windowPositionManager, _settingsService, _filePickerService, dispatcher);

        var fileSystem = new FileSystem();
        var shortcutService = new ShortcutService(fileSystem);
        var iconService = new IconService(fileSystem);
        var imageFactory = new WinUIImageFactory();
        var shortcutResolver = new WindowsShortcutResolver(fileSystem);
        var processStarter = new ProcessStarter();
        var launcher = new WinUILauncher(shortcutResolver, processStarter, fileSystem);

        ViewModel = new MainViewModel(shortcutService, iconService, imageFactory, dispatcher, launcher, fileSystem, _settingsService, _windowService);

        var processService = new ProcessService();
        var backdropWrapper = new BackdropWindowWrapper(this);
        _backdropService = new BackdropService(processService, backdropWrapper);

        this.InitializeComponent();
        // Set tray icon accessibility name from localized resources
        TrayIcon.SetValue(
            Microsoft.UI.Xaml.Automation.AutomationProperties.NameProperty,
            Localization.GetString("Tray_AutomationName"));
        // DataContext required for {Binding} on out-of-tree elements (TrayIcon, ContextFlyout)
        // that cannot use {x:Bind} because they are not in the visual tree.
        RootGrid.DataContext = this;
        // AppGrid/GroupedAppGrid.Tag relays ViewModel to the DataTemplate, which cannot reach
        // ViewModel directly because x:DataType="models:AppItem" restricts the binding context to AppItem.
        AppGrid.Tag = ViewModel;
        GroupedAppGrid.Tag = ViewModel;

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

        // 3. EVENT HOOKS
        this.Activated += MainWindow_Activated;
        this.Closed += MainWindow_Closed;
        _windowService.HotkeyRegistrationFailed += WindowService_HotkeyRegistrationFailed;
        _windowService.Showing += WindowService_Showing;
        _windowService.VisibilityChanged += WindowService_VisibilityChanged;
        ViewModel.LaunchFailed += ViewModel_LaunchFailed;

        // 4. LOAD APPS
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

        // Start dragging - capture pointer and record initial screen-relative cursor position
        _isDraggingWindow = true;
        _dragStartWindowPos = this.AppWindow.Position;
        NativeMethods.GetCursorPos(out _dragStartCursorPos);
        RootGrid.CapturePointer(e.Pointer);
        e.Handled = true;
    }

    private void RootGrid_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (!_isDraggingWindow) return;

        // Use screen-relative cursor coordinates to compute delta. Window-relative
        // coordinates from GetCurrentPoint(null) shift after AppWindow.Move, causing drift.
        NativeMethods.GetCursorPos(out var currentCursorPos);
        var deltaX = currentCursorPos.X - _dragStartCursorPos.X;
        var deltaY = currentCursorPos.Y - _dragStartCursorPos.Y;

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
        if (_isDraggingWindow)
        {
            _isDraggingWindow = false;
            e.Handled = true;
        }
    }

    // Set _freshShow before AppWindow.Show() fires so it is ready when Activated arrives.
    // VisibilityChanged is raised asynchronously after Show(), so it can arrive after Activated
    // and miss the fresh-show window. Showing fires synchronously before Show().
    private void WindowService_Showing(object? sender, EventArgs e)
    {
        _freshShow = true;
    }

    private void WindowService_VisibilityChanged(object? sender, bool isVisible) { }

    private void MainWindow_Activated(object sender, WindowActivatedEventArgs args)
    {
        _windowService.OnActivated(args.WindowActivationState == WindowActivationState.Deactivated);

        // Re-check backdrop on activation in case DWMBlurGlass started after Launchbox
        if (args.WindowActivationState != WindowActivationState.Deactivated)
        {
            _ = _backdropService.UpdateBackdropAsync();

            // Only reset filter and focus on a fresh show (Alt+S), not on every focus-regain.
            // Without this guard, returning focus from a dialog would silently discard the
            // user's active search filter.
            if (_freshShow)
            {
                _freshShow = false;
                ViewModel.FilterText = string.Empty;
                SearchBox.Focus(FocusState.Programmatic);
            }
        }
    }

    private void WindowService_HotkeyRegistrationFailed(object? sender, string e)
    {
        this.DispatcherQueue.TryEnqueue(() =>
        {
            TrayIcon?.ShowNotification(Localization.GetString("Error_NotificationTitle"), e);
        });
    }

    private void ViewModel_LaunchFailed(object? sender, string appName)
    {
        this.DispatcherQueue.TryEnqueue(() =>
        {
            TrayIcon?.ShowNotification(
                Localization.GetString("Error_NotificationTitle"),
                string.Format(Localization.GetString("Error_LaunchFailedMessage"), appName));
        });
    }

    private void MainWindow_Closed(object sender, WindowEventArgs args)
    {
        RootGrid.PointerPressed -= RootGrid_PointerPressed;
        RootGrid.PointerMoved -= RootGrid_PointerMoved;
        RootGrid.PointerReleased -= RootGrid_PointerReleased;
        RootGrid.PointerCanceled -= RootGrid_PointerReleased;
        RootGrid.PointerCaptureLost -= RootGrid_PointerCaptureLost;

        this.Activated -= MainWindow_Activated;
        this.Closed -= MainWindow_Closed;

        _windowService.HotkeyRegistrationFailed -= WindowService_HotkeyRegistrationFailed;
        _windowService.Showing -= WindowService_Showing;
        _windowService.VisibilityChanged -= WindowService_VisibilityChanged;
        ViewModel.LaunchFailed -= ViewModel_LaunchFailed;

        // Dispose all IDisposable services and the ViewModel. Each disposal is isolated
        // so that a failure in one does not prevent the others from being cleaned up.
        DisposeService(_windowService);
        DisposeService(TrayIcon);
        DisposeService(ViewModel);
        DisposeService(_backdropService as IDisposable);
        DisposeService(_settingsService as IDisposable);
    }

    private static void DisposeService(IDisposable? service)
    {
        try
        {
            service?.Dispose();
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"Error disposing {service?.GetType().Name}: {PathSecurity.GetSafeExceptionMessage(ex)}");
        }
    }

    // x:Load generates FindName calls in the XAML compiler output. Window does not inherit
    // FrameworkElement, so FindName is absent from the base class; this delegates to the root
    // content element to satisfy the generated code's contract.
    private object? FindName(string name) => ((FrameworkElement)Content).FindName(name);

    // --- KEYBOARD NAVIGATION ---
    private void SearchBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter && ViewModel.SelectedItem != null)
        {
            if (ViewModel.LaunchAppCommand.CanExecute(ViewModel.SelectedItem))
            {
                e.Handled = true;
                ViewModel.LaunchAppCommand.Execute(ViewModel.SelectedItem);
            }
        }
        else if (e.Key == VirtualKey.Down)
        {
            Control activeGrid = ViewModel.IsMergedMode ? AppGrid : GroupedAppGrid;
            activeGrid.Focus(FocusState.Keyboard);
            e.Handled = true;
        }
    }

    // Typing while the grid has focus redirects characters to the search box,
    // so the user can start typing to filter without clicking the search box first.
    private void Grid_CharacterReceived(UIElement sender, CharacterReceivedRoutedEventArgs args)
    {
        if (char.IsControl(args.Character)) return;
        // Route input through the ViewModel so the binding isn't bypassed.
        ViewModel.FilterText += args.Character;
        SearchBox.Focus(FocusState.Programmatic);
        SearchBox.SelectionStart = SearchBox.Text.Length;
        args.Handled = true;
    }

}
