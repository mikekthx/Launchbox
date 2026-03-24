using System;
using System.Diagnostics;
using System.IO;

namespace Launchbox.Helpers;

public static class ImageHeaderParser
{
    // Minimal parser to extract dimensions from PNG and ICO headers
    // without loading the entire file or using System.Drawing.

    private const int PNG_MIN_HEADER_SIZE = 24;
    private const int ICO_MIN_HEADER_SIZE = 6;
    private const int ICO_ENTRY_SIZE = 16;
    private const int ICO_MAX_IMAGE_COUNT = 256;
    private const int ICO_ZERO_DIMENSION_MEANS = 256;

    public static (int Width, int Height)? GetPngDimensions(Stream stream)
    {
        try
        {
            if (stream.Length < PNG_MIN_HEADER_SIZE) return null;

            stream.Position = 0;
            var header = new byte[PNG_MIN_HEADER_SIZE];
            stream.ReadExactly(header, 0, PNG_MIN_HEADER_SIZE);

            // PNG Signature: 89 50 4E 47 0D 0A 1A 0A
            if (header[0] != 0x89 || header[1] != 0x50 || header[2] != 0x4E || header[3] != 0x47 ||
                header[4] != 0x0D || header[5] != 0x0A || header[6] != 0x1A || header[7] != 0x0A)
            {
                return null;
            }

            // IHDR Chunk starts at byte 8: Length (4 bytes) + Type (4 bytes) + Data
            // Length must be exactly 13 (0x0000000D), Type must be "IHDR" (0x49484452)
            if (header[8] != 0x00 || header[9] != 0x00 || header[10] != 0x00 || header[11] != 0x0D)
                return null;
            if (header[12] != 0x49 || header[13] != 0x48 || header[14] != 0x44 || header[15] != 0x52)
                return null;

            // Width is at byte 16 (4 bytes, Big Endian)
            // Height is at byte 20 (4 bytes, Big Endian)

            int width = (header[16] << 24) | (header[17] << 16) | (header[18] << 8) | header[19];
            int height = (header[20] << 24) | (header[21] << 16) | (header[22] << 8) | header[23];

            return (width, height);
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"Failed to parse PNG dimensions: {PathSecurity.GetSafeExceptionMessage(ex)}");
            return null;
        }
    }

    public static (int Width, int Height)? GetMaxIcoDimensions(Stream stream)
    {
        try
        {
            if (stream.Length < ICO_MIN_HEADER_SIZE) return null;

            stream.Position = 0;
            var header = new byte[ICO_MIN_HEADER_SIZE];
            stream.ReadExactly(header, 0, ICO_MIN_HEADER_SIZE);

            // Reserved (2 bytes) must be 0
            if (header[0] != 0 || header[1] != 0) return null;

            // Type (2 bytes) must be 1 for Icon (2 for Cursor)
            if (header[2] != 1 || header[3] != 0) return null;

            // Count (2 bytes) — cap at 256 to prevent reading unbounded data from a malformed file
            int count = header[4] | (header[5] << 8);
            if (count == 0) return null;
            if (count > ICO_MAX_IMAGE_COUNT) count = ICO_MAX_IMAGE_COUNT;

            int maxWidth = 0;
            int maxHeight = 0;

            // Each directory entry is 16 bytes
            var entry = new byte[ICO_ENTRY_SIZE];
            for (int i = 0; i < count; i++)
            {
                try
                {
                    stream.ReadExactly(entry, 0, ICO_ENTRY_SIZE);
                }
                catch (EndOfStreamException)
                {
                    break;
                }

                // Width (1 byte): 0 means 256
                int w = entry[0];
                if (w == 0) w = 256;

                // Height (1 byte): 0 means 256
                int h = entry[1];
                if (h == 0) h = 256;

                if (w * h > maxWidth * maxHeight)
                {
                    maxWidth = w;
                    maxHeight = h;
                }
            }

            return (maxWidth, maxHeight);
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"Failed to parse ICO dimensions: {PathSecurity.GetSafeExceptionMessage(ex)}");
            return null;
        }
    }
}
