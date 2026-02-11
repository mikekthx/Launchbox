namespace Launchbox;

public interface IFileSystem
{
    bool DirectoryExists(string path);
    string[] GetFiles(string path);
}
