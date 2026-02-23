using Launchbox.Helpers;
using Launchbox.Services;
using System;
using System.Threading.Tasks;
using Xunit;

namespace Launchbox.Tests;

public class BackdropServiceTests
{
    [Fact]
    public async Task UpdateBackdropAsync_WhenProcessRunning_ClearsBackdrop()
    {
        // Arrange
        var processService = new MockProcessService { ShouldReturnTrue = true };
        var windowWrapper = new MockBackdropWindowWrapper { IsBackdropSet = true, IsDesktopAcrylicBackdropSet = true };
        var service = new BackdropService(processService, windowWrapper);

        // Act
        await service.UpdateBackdropAsync();

        // Assert
        Assert.True(windowWrapper.ClearBackdropCalled);
        Assert.False(windowWrapper.IsBackdropSet);
    }

    [Fact]
    public async Task UpdateBackdropAsync_WhenProcessNotRunning_SetsDefaultBackdrop()
    {
        // Arrange
        var processService = new MockProcessService { ShouldReturnTrue = false };
        var windowWrapper = new MockBackdropWindowWrapper { IsBackdropSet = false, IsDesktopAcrylicBackdropSet = false };
        var service = new BackdropService(processService, windowWrapper);

        // Act
        await service.UpdateBackdropAsync();

        // Assert
        Assert.True(windowWrapper.SetDesktopAcrylicBackdropCalled);
        Assert.True(windowWrapper.IsDesktopAcrylicBackdropSet);
    }

    [Fact]
    public async Task UpdateBackdropAsync_WhenProcessCheckThrows_SetsDefaultBackdrop()
    {
        // Arrange
        var processService = new MockProcessService { ShouldThrow = true };
        var windowWrapper = new MockBackdropWindowWrapper { IsBackdropSet = false, IsDesktopAcrylicBackdropSet = false };
        var service = new BackdropService(processService, windowWrapper);

        // Act
        await service.UpdateBackdropAsync();

        // Assert
        Assert.True(windowWrapper.SetDesktopAcrylicBackdropCalled);
    }

    [Fact]
    public async Task UpdateBackdropAsync_Throttling_DoesNotCheckProcessAgain()
    {
        // Arrange
        var processService = new MockProcessService { ShouldReturnTrue = true };
        var windowWrapper = new MockBackdropWindowWrapper { IsBackdropSet = true, IsDesktopAcrylicBackdropSet = true };

        var time = new DateTime(2023, 1, 1, 12, 0, 0);
        // Inject time provider
        var service = new BackdropService(processService, windowWrapper, () => time);

        // Act 1
        await service.UpdateBackdropAsync();
        Assert.Equal(1, processService.CallCount);

        // Act 2 - Immediate (time hasn't changed)
        await service.UpdateBackdropAsync();
        Assert.Equal(1, processService.CallCount); // Still 1

        // Act 3 - Advance time by 61 seconds
        time = time.AddSeconds(61);
        await service.UpdateBackdropAsync();
        Assert.Equal(2, processService.CallCount);
    }
}
