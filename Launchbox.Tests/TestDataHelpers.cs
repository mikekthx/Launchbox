namespace Launchbox.Tests;

/// <summary>Shared test data factory for binary image formats.</summary>
internal static class TestDataHelpers
{
    /// <summary>
    /// Creates a minimal valid PNG byte sequence with the given dimensions.
    /// The buffer is padded to 30 bytes so <see cref="Helpers.ImageHeaderParser"/> can read the full header.
    /// </summary>
    public static byte[] CreatePng(int width, int height)
    {
        var result = new byte[30];
        // Signature
        result[0] = 0x89; result[1] = 0x50; result[2] = 0x4E; result[3] = 0x47;
        result[4] = 0x0D; result[5] = 0x0A; result[6] = 0x1A; result[7] = 0x0A;
        // IHDR length
        result[8] = 0x00; result[9] = 0x00; result[10] = 0x00; result[11] = 0x0D;
        // IHDR type
        result[12] = 0x49; result[13] = 0x48; result[14] = 0x44; result[15] = 0x52;
        // Width (big-endian)
        result[16] = (byte)((width >> 24) & 0xFF);
        result[17] = (byte)((width >> 16) & 0xFF);
        result[18] = (byte)((width >> 8) & 0xFF);
        result[19] = (byte)(width & 0xFF);
        // Height (big-endian)
        result[20] = (byte)((height >> 24) & 0xFF);
        result[21] = (byte)((height >> 16) & 0xFF);
        result[22] = (byte)((height >> 8) & 0xFF);
        result[23] = (byte)(height & 0xFF);
        return result;
    }

    /// <summary>
    /// Creates a minimal valid ICO byte sequence with a single entry of the given dimensions.
    /// </summary>
    public static byte[] CreateIco(int width, int height)
    {
        byte[] data =
        [
            0, 0,                                                                        // Reserved
            1, 0,                                                                        // Type = Icon
            1, 0,                                                                        // Count = 1
            (byte)width, (byte)height, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, // Entry (16 bytes)
        ];
        return data;
    }
}
