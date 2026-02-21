using Launchbox.Services;
using System.Threading.Tasks;

namespace Launchbox.Tests;

public class MockImageFactory : IImageFactory
{
    public Task<object?> CreateImageAsync(byte[] imageBytes)
    {
        return Task.FromResult<object?>("MockImage");
    }
}
