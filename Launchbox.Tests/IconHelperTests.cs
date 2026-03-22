using Launchbox.Helpers;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace Launchbox.Tests;

public class IconHelperTests
{
    private class TestImage
    {
        public bool IsSourceSet { get; private set; }
        public byte[]? SourceData { get; private set; }

        public Task SetSourceAsync(MemoryStream stream)
        {
            IsSourceSet = true;
            SourceData = stream.ToArray();
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task CreateImageAsync_CreatesImageAndSetsSource()
    {
        // Arrange
        byte[] expectedBytes = { 1, 2, 3, 4, 5 };

        // Act
        var result = await IconHelper.CreateImageAsync(
            expectedBytes,
            () => new TestImage(),
            (img, stream) => img.SetSourceAsync(stream));

        // Assert
        Assert.NotNull(result);
        Assert.True(result.IsSourceSet);
        Assert.Equal(expectedBytes, result.SourceData);
    }

    [Fact]
    public async Task CreateImageAsync_ReturnsNull_WhenCreationThrows()
    {
        // Arrange
        byte[] bytes = { 1 };

        // Act
        var result = await IconHelper.CreateImageAsync<TestImage>(
            bytes,
            () => throw new Exception("Creation failed"),
            (img, stream) => Task.CompletedTask);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task CreateImageAsync_ReturnsNull_WhenSetSourceThrows()
    {
        // Arrange
        byte[] bytes = { 1 };

        // Act
        var result = await IconHelper.CreateImageAsync(
            bytes,
            () => new TestImage(),
            (img, stream) => throw new Exception("SetSource failed"));

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task CreateImageAsync_PassesReadableStream()
    {
        // Arrange
        byte[] bytes = { 10, 20 };
        bool streamReadable = false;
        long streamLength = 0;

        // Act
        await IconHelper.CreateImageAsync(
            bytes,
            () => new TestImage(),
            (img, stream) =>
            {
                streamReadable = stream.CanRead;
                streamLength = stream.Length;
                return Task.CompletedTask;
            });

        // Assert
        Assert.True(streamReadable);
        Assert.Equal(2, streamLength);
    }

    [Fact]
    public async Task CreateBitmapImageAsync_ReturnsNull_WhenBytesAreInvalid()
    {
        // Arrange
        byte[] bytes = { 0x01, 0x02, 0x03 };

        // Act - Testing the public 1-arg overload specifically.
        // Underlying generic error handling is tested via CreateImageAsync tests.
        var result = await IconHelper.CreateBitmapImageAsync(bytes);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task CreateBitmapImageAsync_CatchesException_WhenBytesAreCorrupted()
    {
        // Arrange
        // Using an incomplete JPEG header to reliably trigger exception during SetSourceAsync or Init()
        byte[] corruptedBytes = { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46, 0x00 };

        // Act
        var result = await IconHelper.CreateBitmapImageAsync(corruptedBytes);

        // Assert
        Assert.Null(result);
    }
}
