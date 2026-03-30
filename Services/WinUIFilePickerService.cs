using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Windows.Storage.Pickers;

namespace Launchbox.Services;

public class WinUIFilePickerService : IFilePickerService
{
    public object? OwnerWindow { get; set; }

    public async Task<string?> PickSingleFolderAsync()
    {
        if (OwnerWindow == null)
        {
            Trace.WriteLine("Error showing folder picker: OwnerWindow is null.");
            return null;
        }

        try
        {
            var picker = new FolderPicker();
            picker.SuggestedStartLocation = PickerLocationId.Desktop;
            // FolderPicker requires at least one FileTypeFilter entry; without it the dialog will not open.
            picker.FileTypeFilter.Add("*");

            var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(OwnerWindow);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hWnd);

            var folder = await picker.PickSingleFolderAsync();
            return folder?.Path;
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"Error showing folder picker: {Launchbox.Helpers.PathSecurity.GetSafeExceptionMessage(ex)}");
            return null;
        }
    }

    public async Task<string?> ShowRenameFolderDialogAsync(string currentLabel)
    {
        if (OwnerWindow == null || OwnerWindow is not Microsoft.UI.Xaml.Window window)
        {
            return null;
        }

        var textBox = new Microsoft.UI.Xaml.Controls.TextBox { Text = currentLabel, PlaceholderText = currentLabel };
        var dialog = new Microsoft.UI.Xaml.Controls.ContentDialog
        {
            Title = Launchbox.Helpers.Localization.GetString("Settings_RenameFolder_Title"),
            PrimaryButtonText = Launchbox.Helpers.Localization.GetString("Settings_RenameFolder_OK"),
            CloseButtonText = Launchbox.Helpers.Localization.GetString("Settings_RenameFolder_Cancel"),
            Content = textBox,
            XamlRoot = window.Content.XamlRoot
        };

        var result = await dialog.ShowAsync();
        if (result == Microsoft.UI.Xaml.Controls.ContentDialogResult.Primary && !string.IsNullOrWhiteSpace(textBox.Text))
        {
            return textBox.Text;
        }

        return null;
    }
}
