using Launchbox.Helpers;
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
}
