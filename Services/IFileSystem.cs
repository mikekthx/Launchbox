using System;
using System.Collections.Generic;
using System.IO;

namespace Launchbox.Services;

public interface IFileSystem
{
    void CreateDirectory(string path);
    bool DirectoryExists(string path);
    bool FileExists(string path);
    IEnumerable<string> EnumerateFiles(string path);
    string GetIniValue(string path, string section, string key);
    byte[] ReadAllBytes(string path);
    Stream OpenRead(string path);
    DateTime GetLastWriteTime(string path);
    long GetFileSize(string path);

    /// <summary>
    /// Watches a directory for file additions, deletions, renames, and changes,
    /// invoking <paramref name="callback"/> on any event. Returns a disposable handle
    /// that stops watching when disposed. Returns a no-op disposable if the path does
    /// not exist or is unsafe.
    /// </summary>
    IDisposable WatchDirectory(string path, Action callback);
}
