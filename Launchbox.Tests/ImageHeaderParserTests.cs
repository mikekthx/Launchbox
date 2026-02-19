using System.IO;
using Xunit;
using Launchbox.Helpers;

namespace Launchbox.Tests;

public class ImageHeaderParserTests
{
    [Fact]
    public void GetPngDimensions_ValidHeader_ReturnsDimensions()
    {
        // 8 bytes signature + 4 bytes length + 4 bytes type + 4 bytes width + 4 bytes height = 24 bytes
        var header = new byte[]
        {
            0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, // Signature
            0x00, 0x00, 0x00, 0x0D,                         // IHDR Length
            0x49, 0x48, 0x44, 0x52,                         // "IHDR"
            0x00, 0x00, 0x01, 0x00,                         // Width: 256 (0x0100)
            0x00, 0x00, 0x02, 0x00                          // Height: 512 (0x0200)
        };

        using var stream = new MemoryStream(header);
        var result = ImageHeaderParser.GetPngDimensions(stream);

        Assert.NotNull(result);
        Assert.Equal(256, result.Value.Width);
        Assert.Equal(512, result.Value.Height);
    }

    [Fact]
    public void GetPngDimensions_InvalidSignature_ReturnsNull()
    {
        var header = new byte[24]; // All zeros
        using var stream = new MemoryStream(header);
        var result = ImageHeaderParser.GetPngDimensions(stream);

        Assert.Null(result);
    }

    [Fact]
    public void GetPngDimensions_ShortStream_ReturnsNull()
    {
        var header = new byte[10];
        using var stream = new MemoryStream(header);
        var result = ImageHeaderParser.GetPngDimensions(stream);

        Assert.Null(result);
    }

    [Fact]
    public void GetMaxIcoDimensions_ValidHeader_ReturnsDimensions()
    {
        // Header: 6 bytes
        // Entry 1: 16 bytes (32x32)
        // Entry 2: 16 bytes (64x64)

        var data = new byte[]
        {
            0, 0, // Reserved
            1, 0, // Type 1 (Icon)
            2, 0, // Count 2

            // Entry 1 (32x32)
            32, // Width
            32, // Height
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, // 14 bytes padding

            // Entry 2 (64x64)
            64, // Width
            64, // Height
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 // 14 bytes padding
        };

        using var stream = new MemoryStream(data);
        var result = ImageHeaderParser.GetMaxIcoDimensions(stream);

        Assert.NotNull(result);
        Assert.Equal(64, result.Value.Width);
        Assert.Equal(64, result.Value.Height);
    }

    [Fact]
    public void GetMaxIcoDimensions_ZeroValues_Returns256()
    {
        // Width/Height 0 means 256
        var data = new byte[]
        {
            0, 0,
            1, 0,
            1, 0, // Count 1

            // Entry 1 (0x0 -> 256x256)
            0, // Width
            0, // Height
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0
        };

        using var stream = new MemoryStream(data);
        var result = ImageHeaderParser.GetMaxIcoDimensions(stream);

        Assert.NotNull(result);
        Assert.Equal(256, result.Value.Width);
        Assert.Equal(256, result.Value.Height);
    }

    [Fact]
    public void GetMaxIcoDimensions_InvalidType_ReturnsNull()
    {
        var data = new byte[]
        {
            0, 0,
            2, 0, // Type 2 (Cursor)
            1, 0
        };

        using var stream = new MemoryStream(data);
        var result = ImageHeaderParser.GetMaxIcoDimensions(stream);

        Assert.Null(result);
    }

    [Fact]
    public void GetMaxIcoDimensions_InvalidReserved_ReturnsNull()
    {
        var data = new byte[]
        {
            1, 0, // Reserved 1
            1, 0,
            1, 0
        };

        using var stream = new MemoryStream(data);
        var result = ImageHeaderParser.GetMaxIcoDimensions(stream);

        Assert.Null(result);
    }

    [Fact]
    public void GetMaxIcoDimensions_ShortStream_ReturnsNull()
    {
        var header = new byte[4];
        using var stream = new MemoryStream(header);
        var result = ImageHeaderParser.GetMaxIcoDimensions(stream);

        Assert.Null(result);
    }
}
