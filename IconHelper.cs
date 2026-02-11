using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Windows.Storage.Streams;

namespace Launchbox;

public static class IconHelper
{
    public static async Task<BitmapImage?> CreateBitmapImageAsync(byte[] imageBytes)
    {
        try
        {
            var image = new BitmapImage();
            using var stream = new InMemoryRandomAccessStream();
            using var writer = new DataWriter(stream.GetOutputStreamAt(0));
            writer.WriteBytes(imageBytes);
            await writer.StoreAsync();
            stream.Seek(0);
            await image.SetSourceAsync(stream);
            return image;
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"Failed to create BitmapImage: {ex.Message}");
            return null;
        }
    }
}
