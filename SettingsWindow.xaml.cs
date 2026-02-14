using Launchbox.Helpers;
using Launchbox.Services;
using Launchbox.ViewModels;
using Microsoft.UI.Xaml;
using System;
using System.Runtime.InteropServices;
using Windows.Storage.Pickers;

namespace Launchbox;

public sealed partial class SettingsWindow : Window
{
    public SettingsViewModel ViewModel { get; }

    public SettingsWindow(SettingsService settingsService, WindowService windowService)
    {
        ViewModel = new SettingsViewModel(settingsService, windowService);
        ViewModel.BrowseFolderCommand = new SimpleCommand(BrowseFolderAsync);

        this.InitializeComponent();

        this.Title = "Launchbox Settings";
    }

    private async void BrowseFolderAsync()
    {
        try
        {
            var picker = new FolderPicker();
            picker.SuggestedStartLocation = PickerLocationId.Desktop;
            picker.FileTypeFilter.Add("*");

            // Initialize with window handle
            var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hWnd);

            var folder = await picker.PickSingleFolderAsync();
            if (folder != null)
            {
                ViewModel.ShortcutsPath = folder.Path;
            }
        }
        catch (Exception ex)
        {
            // Log or handle error? For now just ignore UI errors.
            // Maybe show message dialog?
            System.Diagnostics.Trace.WriteLine($"Error browsing folder: {ex.Message}");
        }
    }
}
