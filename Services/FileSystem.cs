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
        int capacity = 512;
        var sb = new StringBuilder(capacity);
        int ret = NativeMethods.GetPrivateProfileString(section, key, string.Empty, sb, capacity, path);

        // Check for truncation. GetPrivateProfileString returns size - 1 (or sometimes size - 2)
        // if the buffer was too small. We loop to double the buffer size until it fits.
        while (ret >= capacity - 2)
        {
            capacity *= 2;
            if (capacity > 65536)
            {
                // Safety limit to prevent infinite allocation.
                // If it's larger than 64KB, we accept truncation.
                break;
            }

            sb = new StringBuilder(capacity);
            ret = NativeMethods.GetPrivateProfileString(section, key, string.Empty, sb, capacity, path);
        }

        return sb.ToString();
    }

    public byte[] ReadAllBytes(string path) => File.ReadAllBytes(path);
    public Stream OpenRead(string path) => File.OpenRead(path);
    public DateTime GetLastWriteTime(string path) => File.GetLastWriteTime(path);
}
