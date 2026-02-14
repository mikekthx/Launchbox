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
        var sb = new StringBuilder(255);
        NativeMethods.GetPrivateProfileString(section, key, "", sb, 255, path);
        return sb.ToString();
    }

    public byte[] ReadAllBytes(string path) => File.ReadAllBytes(path);
    public Stream OpenRead(string path) => File.OpenRead(path);
    public DateTime GetLastWriteTime(string path) => File.GetLastWriteTime(path);
}
