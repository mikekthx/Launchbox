using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Windows.Storage.Streams;

namespace Launchbox.Helpers;

public static class IconHelper
{
    public static async Task<BitmapImage?> CreateBitmapImageAsync(byte[] imageBytes)
    {
        return await CreateImageAsync(
            imageBytes,
            () => new BitmapImage(),
            async (img, stream) => await img.SetSourceAsync(stream.AsRandomAccessStream()));
    }

    internal static async Task<T?> CreateImageAsync<T>(
        byte[] imageBytes,
        Func<T> createInstance,
        Func<T, MemoryStream, Task> setSourceAction)
        where T : class
    {
        try
        {
            var image = createInstance();
            // Do NOT use 'using' here. The MemoryStream must remain open for the lifetime of the BitmapImage (or T).
            // Since MemoryStream over a byte array holds no unmanaged resources, let GC handle it.
            var stream = new MemoryStream(imageBytes);
            await setSourceAction(image, stream);
            return image;
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"Failed to create BitmapImage: {ex.Message}");
            return null;
        }
    }
}
