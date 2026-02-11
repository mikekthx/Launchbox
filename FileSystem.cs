using System.IO;

namespace Launchbox;

public class FileSystem : IFileSystem
{
    public bool DirectoryExists(string path) => Directory.Exists(path);
    public string[] GetFiles(string path) => Directory.GetFiles(path);
}
