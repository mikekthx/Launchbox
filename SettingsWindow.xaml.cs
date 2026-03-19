using Launchbox.Helpers;
using Launchbox.Services;
using Launchbox.ViewModels;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Windows.Graphics;

namespace Launchbox;

public sealed partial class SettingsWindow : Window
{
    public SettingsViewModel ViewModel { get; }

    private readonly IFilePickerService _filePickerService;
    private readonly object? _previousOwnerWindow;

    public SettingsWindow(SettingsService settingsService, IWindowService windowService, IFilePickerService filePickerService)
    {
        _filePickerService = filePickerService;
        _previousOwnerWindow = filePickerService.OwnerWindow;
        filePickerService.OwnerWindow = this;
        ViewModel = new SettingsViewModel(settingsService, windowService, filePickerService);

        this.InitializeComponent();

        this.Title = Localization.GetString("SettingsWindow_Title");
        this.ExtendsContentIntoTitleBar = true;
        this.SetTitleBar(AppTitleBar);

        var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hWnd);
        var appWindow = AppWindow.GetFromWindowId(windowId);
        appWindow.Resize(new SizeInt32(560, 480));

        this.Closed += SettingsWindow_Closed;
    }

    private void SettingsWindow_Closed(object sender, WindowEventArgs args)
    {
        this.Closed -= SettingsWindow_Closed;
        _filePickerService.OwnerWindow = _previousOwnerWindow;
        ViewModel.Dispose();
    }
}
