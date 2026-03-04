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
        var result = await service.PickSingleFolderAsync(new object());
        Assert.Null(result);
    }

    [Fact]
    public async Task PickSingleFolderAsync_NullWindow_CatchesExceptionAndReturnsNull()
    {
        var service = new WinUIFilePickerService();
        var result = await service.PickSingleFolderAsync(null!);
        Assert.Null(result);
    }
}
