using Launchbox.Helpers;
using System.IO;
using Xunit;

namespace Launchbox.Tests;

public class ImageHeaderParserTests
{
    // --- PNG Tests ---

    [Fact]
    public void GetPngDimensions_ReturnsCorrectDimensions_ForValidPng()
    {
        var data = CreatePng(64, 48);
        using var stream = new MemoryStream(data);

        var result = ImageHeaderParser.GetPngDimensions(stream);

        Assert.NotNull(result);
        Assert.Equal(64, result.Value.Width);
        Assert.Equal(48, result.Value.Height);
    }

    [Fact]
    public void GetPngDimensions_ReturnsCorrectDimensions_ForLargeMultiByteDimensions()
    {
        // 1920x1080 — requires all 4 bytes of big-endian encoding
        var data = CreatePngMultiByte(1920, 1080);
        using var stream = new MemoryStream(data);

        var result = ImageHeaderParser.GetPngDimensions(stream);

        Assert.NotNull(result);
        Assert.Equal(1920, result.Value.Width);
        Assert.Equal(1080, result.Value.Height);
    }

    [Fact]
    public void GetPngDimensions_ReturnsNull_WhenStreamTooShort()
    {
        using var stream = new MemoryStream(new byte[10]);

        var result = ImageHeaderParser.GetPngDimensions(stream);

        Assert.Null(result);
    }

    [Fact]
    public void GetPngDimensions_ReturnsNull_ForEmptyStream()
    {
        using var stream = new MemoryStream([]);

        var result = ImageHeaderParser.GetPngDimensions(stream);

        Assert.Null(result);
    }

    [Fact]
    public void GetPngDimensions_ReturnsNull_ForInvalidSignature()
    {
        var data = CreatePng(64, 48);
        data[0] = 0x00; // Corrupt the PNG signature
        using var stream = new MemoryStream(data);

        var result = ImageHeaderParser.GetPngDimensions(stream);

        Assert.Null(result);
    }

    [Fact]
    public void GetPngDimensions_ReturnsNull_ForJpegData()
    {
        // JPEG starts with FF D8 FF — not a PNG
        var data = new byte[30];
        data[0] = 0xFF;
        data[1] = 0xD8;
        data[2] = 0xFF;
        using var stream = new MemoryStream(data);

        var result = ImageHeaderParser.GetPngDimensions(stream);

        Assert.Null(result);
    }

    [Fact]
    public void GetPngDimensions_ReturnsOneDimensions_ForMinimalValidPng()
    {
        var data = CreatePng(1, 1);
        using var stream = new MemoryStream(data);

        var result = ImageHeaderParser.GetPngDimensions(stream);

        Assert.NotNull(result);
        Assert.Equal(1, result.Value.Width);
        Assert.Equal(1, result.Value.Height);
    }

    // --- ICO Tests ---

    [Fact]
    public void GetMaxIcoDimensions_ReturnsCorrectDimensions_ForSingleEntry()
    {
        var data = CreateIco(32, 32);
        using var stream = new MemoryStream(data);

        var result = ImageHeaderParser.GetMaxIcoDimensions(stream);

        Assert.NotNull(result);
        Assert.Equal(32, result.Value.Width);
        Assert.Equal(32, result.Value.Height);
    }

    [Fact]
    public void GetMaxIcoDimensions_ReturnsLargestEntry_WhenMultipleEntries()
    {
        var data = CreateMultiEntryIco((16, 16), (48, 48), (32, 32));
        using var stream = new MemoryStream(data);

        var result = ImageHeaderParser.GetMaxIcoDimensions(stream);

        Assert.NotNull(result);
        Assert.Equal(48, result.Value.Width);
        Assert.Equal(48, result.Value.Height);
    }

    [Fact]
    public void GetMaxIcoDimensions_Returns256_WhenEntryByteIsZero()
    {
        // ICO convention: 0 byte means 256 pixels
        var data = CreateIco(0, 0);
        using var stream = new MemoryStream(data);

        var result = ImageHeaderParser.GetMaxIcoDimensions(stream);

        Assert.NotNull(result);
        Assert.Equal(256, result.Value.Width);
        Assert.Equal(256, result.Value.Height);
    }

    [Fact]
    public void GetMaxIcoDimensions_ReturnsNull_WhenStreamTooShort()
    {
        using var stream = new MemoryStream(new byte[3]);

        var result = ImageHeaderParser.GetMaxIcoDimensions(stream);

        Assert.Null(result);
    }

    [Fact]
    public void GetMaxIcoDimensions_ReturnsNull_ForEmptyStream()
    {
        using var stream = new MemoryStream([]);

        var result = ImageHeaderParser.GetMaxIcoDimensions(stream);

        Assert.Null(result);
    }

    [Fact]
    public void GetMaxIcoDimensions_ReturnsNull_WhenReservedFieldNonZero()
    {
        var data = CreateIco(32, 32);
        data[0] = 1; // Corrupt reserved field
        using var stream = new MemoryStream(data);

        var result = ImageHeaderParser.GetMaxIcoDimensions(stream);

        Assert.Null(result);
    }

    [Fact]
    public void GetMaxIcoDimensions_ReturnsNull_WhenTypeIsCursor()
    {
        var data = CreateIco(32, 32);
        data[2] = 2; // Type 2 = cursor, not icon
        using var stream = new MemoryStream(data);

        var result = ImageHeaderParser.GetMaxIcoDimensions(stream);

        Assert.Null(result);
    }

    [Fact]
    public void GetMaxIcoDimensions_ReturnsNull_WhenEntryCountIsZero()
    {
        byte[] data =
        [
            0, 0,   // Reserved
            1, 0,   // Type = Icon
            0, 0,   // Count = 0
        ];
        using var stream = new MemoryStream(data);

        var result = ImageHeaderParser.GetMaxIcoDimensions(stream);

        Assert.Null(result);
    }

    [Fact]
    public void GetMaxIcoDimensions_HandlesTruncatedEntryData()
    {
        // Header says 2 entries but only include 1 full entry
        byte[] data =
        [
            0, 0,   // Reserved
            1, 0,   // Type = Icon
            2, 0,   // Count = 2
            // Entry 1 (complete 16 bytes)
            64, 64, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            // Entry 2 (truncated — only 4 bytes instead of 16)
            128, 128, 0, 0,
        ];
        using var stream = new MemoryStream(data);

        var result = ImageHeaderParser.GetMaxIcoDimensions(stream);

        // Should return the first entry's dimensions (second entry read fails)
        Assert.NotNull(result);
        Assert.Equal(64, result.Value.Width);
        Assert.Equal(64, result.Value.Height);
    }

    // --- Helper methods ---

    private static byte[] CreatePng(int width, int height)
    {
        byte[] header =
        [
            0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, // PNG signature
            0x00, 0x00, 0x00, 0x0D,                             // IHDR chunk length
            0x49, 0x48, 0x44, 0x52,                             // IHDR chunk type
            0, 0, 0, (byte)width,                               // Width (big-endian)
            0, 0, 0, (byte)height,                              // Height (big-endian)
        ];
        var result = new byte[30];
        Array.Copy(header, result, header.Length);
        return result;
    }

    private static byte[] CreatePngMultiByte(int width, int height)
    {
        byte[] header =
        [
            0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, // PNG signature
            0x00, 0x00, 0x00, 0x0D,                             // IHDR chunk length
            0x49, 0x48, 0x44, 0x52,                             // IHDR chunk type
            (byte)(width >> 24), (byte)(width >> 16), (byte)(width >> 8), (byte)width,
            (byte)(height >> 24), (byte)(height >> 16), (byte)(height >> 8), (byte)height,
        ];
        var result = new byte[30];
        Array.Copy(header, result, header.Length);
        return result;
    }

    private static byte[] CreateIco(int width, int height)
    {
        byte[] data =
        [
            0, 0,                                                   // Reserved
            1, 0,                                                   // Type = Icon
            1, 0,                                                   // Count = 1
            (byte)width, (byte)height, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, // Entry (16 bytes)
        ];
        return data;
    }

    private static byte[] CreateMultiEntryIco(params (int Width, int Height)[] entries)
    {
        var count = entries.Length;
        var data = new byte[6 + (count * 16)];

        // Header
        data[0] = 0; data[1] = 0;           // Reserved
        data[2] = 1; data[3] = 0;           // Type = Icon
        data[4] = (byte)count; data[5] = 0; // Count

        for (int i = 0; i < count; i++)
        {
            var offset = 6 + (i * 16);
            data[offset] = (byte)entries[i].Width;
            data[offset + 1] = (byte)entries[i].Height;
        }

        return data;
    }
}
