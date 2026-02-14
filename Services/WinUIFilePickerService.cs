using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Windows.Storage.Pickers;

namespace Launchbox.Services;

public class WinUIFilePickerService : IFilePickerService
{
    public async Task<string?> PickSingleFolderAsync(object window)
    {
        try
        {
            var picker = new FolderPicker();
            picker.SuggestedStartLocation = PickerLocationId.Desktop;
            picker.FileTypeFilter.Add("*");

            var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hWnd);

            var folder = await picker.PickSingleFolderAsync();
            return folder?.Path;
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"Error showing folder picker: {ex.Message}");
            return null;
        }
    }
}
