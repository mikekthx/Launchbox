using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace Launchbox;

public class FileSystem : IFileSystem
{
    public bool DirectoryExists(string path) => Directory.Exists(path);
    public bool FileExists(string path) => File.Exists(path);
    public string[] GetFiles(string path) => Directory.GetFiles(path);

    public string GetIniValue(string path, string section, string key)
    {
        var sb = new StringBuilder(255);
        GetPrivateProfileString(section, key, "", sb, 255, path);
        return sb.ToString();
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetPrivateProfileString(string s, string k, string d, StringBuilder r, int z, string f);
}
