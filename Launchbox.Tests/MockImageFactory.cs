using System.Threading.Tasks;
using Launchbox.Services;

namespace Launchbox.Tests;

public class MockImageFactory : IImageFactory
{
    public Task<object?> CreateImageAsync(byte[] imageBytes)
    {
        return Task.FromResult<object?>("MockImage");
    }
}
