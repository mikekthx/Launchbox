using System;
using System.Buffers;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace Launchbox.Services;

public class FileSystem : IFileSystem
{
    public void CreateDirectory(string path) => Directory.CreateDirectory(path);
    public bool DirectoryExists(string path) => Directory.Exists(path);
    public bool FileExists(string path) => File.Exists(path);
    public string[] GetFiles(string path) => Directory.GetFiles(path);

    public string GetIniValue(string path, string section, string key)
    {
        // Start with a reasonable buffer size to minimize reallocations
        // 4096 is enough for almost all paths and typical INI values.
        int capacity = 4096;
        char[] buffer = ArrayPool<char>.Shared.Rent(capacity);
        try
        {
            while (true)
            {
                int size = buffer.Length;
                int ret = NativeMethods.GetPrivateProfileString(section, key, string.Empty, buffer, size, path);

                // Check for truncation. GetPrivateProfileString returns size - 1 (or sometimes size - 2)
                // if the buffer was too small.
                if (ret < size - 2)
                {
                    return new string(buffer, 0, ret);
                }

                // Truncated. Loop to double the buffer size until it fits.
                int newCapacity = size * 2;
                if (newCapacity > 65536)
                {
                    // Safety limit to prevent infinite allocation.
                    // If it's larger than 64KB, we accept truncation.
                    return new string(buffer, 0, ret);
                }

                var newBuffer = ArrayPool<char>.Shared.Rent(newCapacity);
                ArrayPool<char>.Shared.Return(buffer);
                buffer = newBuffer;
            }
        }
        finally
        {
            ArrayPool<char>.Shared.Return(buffer);
        }
    }

    public byte[] ReadAllBytes(string path) => File.ReadAllBytes(path);
    public Stream OpenRead(string path) => File.OpenRead(path);
    public DateTime GetLastWriteTime(string path) => File.GetLastWriteTime(path);
    public long GetFileSize(string path) => new FileInfo(path).Length;
}
