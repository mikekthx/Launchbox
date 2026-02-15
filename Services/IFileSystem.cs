using System;
using System.IO;

namespace Launchbox.Services;

public interface IFileSystem
{
    void CreateDirectory(string path);
    bool DirectoryExists(string path);
    bool FileExists(string path);
    string[] GetFiles(string path);
    string GetIniValue(string path, string section, string key);
    byte[] ReadAllBytes(string path);
    Stream OpenRead(string path);
    DateTime GetLastWriteTime(string path);
}
