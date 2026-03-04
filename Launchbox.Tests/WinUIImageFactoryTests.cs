using Launchbox.Services;
using System;
using System.Threading.Tasks;
using Xunit;

namespace Launchbox.Tests;

public class WinUIImageFactoryTests
{
    [Fact]
    public async Task CreateImageAsync_ReturnsNull_WhenBytesAreEmpty()
    {
        // Arrange
        var factory = new WinUIImageFactory();
        var bytes = Array.Empty<byte>();

        // Act
        var result = await factory.CreateImageAsync(bytes);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task CreateImageAsync_ReturnsNull_WhenValidImageButNoDispatcherQueue()
    {
        // Arrange
        var factory = new WinUIImageFactory();
        // A minimal 1x1 valid PNG image byte array
        var bytes = new byte[] {
            137, 80, 78, 71, 13, 10, 26, 10,
            0, 0, 0, 13, 73, 72, 68, 82, 0, 0, 0, 1, 0, 0, 0, 1, 8, 2, 0, 0, 0, 144, 119, 83, 222,
            0, 0, 0, 12, 73, 68, 65, 84, 8, 215, 99, 248, 255, 255, 63, 0, 5, 254, 2, 254, 220, 204, 89, 231,
            0, 0, 0, 0, 73, 69, 78, 68, 174, 66, 96, 130
        };

        // Act
        // On Linux or running tests without a WinUI message pump,
        // BitmapImage creation will throw and be caught, returning null.
        // It's not feasible to start a real WinUI DispatcherQueue in a cross-platform xUnit test directly.
        var result = await factory.CreateImageAsync(bytes);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task CreateImageAsync_ReturnsNull_WhenBytesAreNull()
    {
        // Arrange
        var factory = new WinUIImageFactory();
        byte[] bytes = null!;

        // Act
        var result = await factory.CreateImageAsync(bytes);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task CreateImageAsync_ReturnsNull_WhenBytesAreInvalidImage()
    {
        // Arrange
        var factory = new WinUIImageFactory();
        var bytes = new byte[] { 0x01, 0x02, 0x03, 0x04 };

        // Act
        var result = await factory.CreateImageAsync(bytes);

        // Assert
        Assert.Null(result);
    }
}
