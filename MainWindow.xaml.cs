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

    private void MainWindow_Activated(object sender, WindowActivatedEventArgs args)
    {
        _windowService.OnActivated(args.WindowActivationState == WindowActivationState.Deactivated);

        // Re-check backdrop on activation in case DWMBlurGlass started after Launchbox
        if (args.WindowActivationState != WindowActivationState.Deactivated)
        {
            _ = _backdropService.UpdateBackdropAsync();
            ViewModel.FilterText = string.Empty;
            SearchBox.Focus(FocusState.Programmatic);
        }
    }

    private void WindowService_HotkeyRegistrationFailed(object? sender, string e)
    {
        this.DispatcherQueue.TryEnqueue(() =>
        {
            TrayIcon?.ShowNotification(Localization.GetString("Error_NotificationTitle"), e);
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
}
