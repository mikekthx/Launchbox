using System;
using System.IO;

namespace Launchbox.Helpers;

public static class ImageHeaderParser
{
    // Minimal parser to extract dimensions from PNG and ICO headers
    // without loading the entire file or using System.Drawing.

    public static (int Width, int Height)? GetPngDimensions(Stream stream)
    {
        try
        {
            if (stream.Length < 24)
            {
                return null;
            }

            stream.Position = 0;
            var header = new byte[24];
            if (stream.Read(header, 0, 24) != 24)
            {
                return null;
            }

            // PNG Signature: 89 50 4E 47 0D 0A 1A 0A
            if (header[0] != 0x89 || header[1] != 0x50 || header[2] != 0x4E || header[3] != 0x47 ||
                header[4] != 0x0D || header[5] != 0x0A || header[6] != 0x1A || header[7] != 0x0A)
            {
                return null;
            }

            // IHDR Chunk starts at byte 12 (Length 4 bytes, Type 4 bytes, then Data)
            // Length should be 13 (0x0000000D)
            // Type should be "IHDR" (0x49484452)

            // Width is at byte 16 (4 bytes, Big Endian)
            // Height is at byte 20 (4 bytes, Big Endian)

            int width = (header[16] << 24) | (header[17] << 16) | (header[18] << 8) | header[19];
            int height = (header[20] << 24) | (header[21] << 16) | (header[22] << 8) | header[23];

            return (width, height);
        }
        catch
        {
            return null;
        }
    }

    public static (int Width, int Height)? GetMaxIcoDimensions(Stream stream)
    {
        try
        {
            if (stream.Length < 6)
            {
                return null;
            }

            stream.Position = 0;
            var header = new byte[6];
            if (stream.Read(header, 0, 6) != 6)
            {
                return null;
            }

            // Reserved (2 bytes) must be 0
            if (header[0] != 0 || header[1] != 0)
            {
                return null;
            }

            // Type (2 bytes) must be 1 for Icon (2 for Cursor)
            if (header[2] != 1 || header[3] != 0)
            {
                return null;
            }

            // Count (2 bytes)
            int count = header[4] | (header[5] << 8);
            if (count == 0)
            {
                return null;
            }

            int maxWidth = 0;
            int maxHeight = 0;

            // Each directory entry is 16 bytes
            var entry = new byte[16];
            for (int i = 0; i < count; i++)
            {
                if (stream.Read(entry, 0, 16) != 16)
                {
                    break;
                }

                // Width (1 byte): 0 means 256
                int w = entry[0];
                if (w == 0)
                {
                    w = 256;
                }

                // Height (1 byte): 0 means 256
                int h = entry[1];
                if (h == 0)
                {
                    h = 256;
                }

                if (w * h > maxWidth * maxHeight)
                {
                    maxWidth = w;
                    maxHeight = h;
                }
            }

            return (maxWidth, maxHeight);
        }
        catch
        {
            return null;
        }
    }
}
