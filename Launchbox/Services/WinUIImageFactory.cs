using System.Threading.Tasks;

namespace Launchbox.Services;

public class WinUIImageFactory : IImageFactory
{
    public async Task<object?> CreateImageAsync(byte[] imageBytes)
    {
        return await IconHelper.CreateBitmapImageAsync(imageBytes);
    }
}
