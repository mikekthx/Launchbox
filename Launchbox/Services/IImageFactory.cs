using System.Threading.Tasks;

namespace Launchbox.Services;

public interface IImageFactory
{
    Task<object?> CreateImageAsync(byte[] imageBytes);
}
