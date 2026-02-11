namespace Launchbox;

public interface IFileSystem
{
    bool DirectoryExists(string path);
    bool FileExists(string path);
    string[] GetFiles(string path);
    string GetIniValue(string path, string section, string key);
}
