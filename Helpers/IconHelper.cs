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
        try
        {
            var image = new BitmapImage();
            using var stream = new MemoryStream(imageBytes);
            await image.SetSourceAsync(stream.AsRandomAccessStream());
            return image;
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"Failed to create BitmapImage: {ex.Message}");
            return null;
        }
    }
}
