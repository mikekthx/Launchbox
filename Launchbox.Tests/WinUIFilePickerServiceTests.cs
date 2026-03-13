using Launchbox.Services;
using System;
using System.Threading.Tasks;
using Xunit;

namespace Launchbox.Tests;

public class WinUIFilePickerServiceTests
{
    [Fact]
    public async Task PickSingleFolderAsync_InvalidWindow_CatchesExceptionAndReturnsNull()
    {
        var service = new WinUIFilePickerService();
        service.OwnerWindow = new object();
        var result = await service.PickSingleFolderAsync();
        Assert.Null(result);
    }

    [Fact]
    public async Task PickSingleFolderAsync_NullWindow_ReturnsNull()
    {
        var service = new WinUIFilePickerService();
        service.OwnerWindow = null;
        var result = await service.PickSingleFolderAsync();
        Assert.Null(result);
    }
}
