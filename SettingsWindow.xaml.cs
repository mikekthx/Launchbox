using Launchbox.Helpers;
using Launchbox.Models;
using Launchbox.Services;
using Launchbox.ViewModels;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
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
        SettingsContent.DataContext = this;

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

    private async void RenameFolder_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is not Button { DataContext: ShortcutFolder folder }) return;

            var textBox = new TextBox { Text = folder.Label, PlaceholderText = folder.Label };
            var dialog = new ContentDialog
            {
                Title = Localization.GetString("Settings_RenameFolder_Title"),
                PrimaryButtonText = Localization.GetString("Settings_RenameFolder_OK"),
                CloseButtonText = Localization.GetString("Settings_RenameFolder_Cancel"),
                Content = textBox,
                XamlRoot = Content.XamlRoot
            };

            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary && !string.IsNullOrWhiteSpace(textBox.Text))
            {
                ViewModel.ApplyRename(folder.Order, textBox.Text);
            }
        }
        catch (System.Exception ex)
        {
            System.Diagnostics.Trace.WriteLine($"Failed to rename folder: {PathSecurity.GetSafeExceptionMessage(ex)}");
        }
    }
}
