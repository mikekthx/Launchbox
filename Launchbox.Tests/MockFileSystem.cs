using Launchbox.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Launchbox.Tests;

public class MockFileSystem : IFileSystem
{
    private readonly HashSet<string> _directories = new();
    private readonly Dictionary<string, List<string>> _files = new();
    private readonly Dictionary<string, string> _iniValues = new();
    private readonly Dictionary<string, byte[]> _fileContents = new();
    private readonly Dictionary<string, long> _fileSizes = new();
    private readonly Dictionary<string, DateTime> _fileTimes = new();

    public void AddDirectory(string path)
    {
        _directories.Add(path);
    }

    public void AddFile(string directory, string filename, long size = 0, byte[]? content = null, DateTime? lastWriteTime = null)
    {
        string fullPath = Path.Combine(directory, filename);
        AddFile(fullPath, size, content, lastWriteTime);
    }

    public void AddFile(string fullPath, long size = 0, byte[]? content = null, DateTime? lastWriteTime = null)
    {
        string? directory = Path.GetDirectoryName(fullPath);

        // On non-Windows, Path.GetDirectoryName might fail for Windows paths using backslashes
        if (string.IsNullOrEmpty(directory) && fullPath.Contains('\\'))
        {
             int lastSeparator = fullPath.LastIndexOf('\\');
             if (lastSeparator > 0)
             {
                 directory = fullPath.Substring(0, lastSeparator);
             }
        }

        if (string.IsNullOrEmpty(directory)) return;

        if (!_files.ContainsKey(directory))
        {
            _files[directory] = new List<string>();
        }
        if (!_files[directory].Contains(fullPath))
        {
            _files[directory].Add(fullPath);
        }

        if (content != null)
        {
            _fileContents[fullPath] = content;
            _fileSizes[fullPath] = content.Length;
        }
        else
        {
            _fileSizes[fullPath] = size;
        }

        _fileTimes[fullPath] = lastWriteTime ?? DateTime.Now;
    }

    public void SetIniValue(string path, string section, string key, string value)
    {
        _iniValues[$"{path}|{section}|{key}"] = value;
    }

    public bool DirectoryExists(string path)
    {
        return _directories.Contains(path);
    }

    public bool FileExists(string path)
    {
        return _files.Values.Any(list => list.Contains(path));
    }

    public string[] GetFiles(string path)
    {
        if (_files.TryGetValue(path, out var files))
        {
            return files.ToArray();
        }
        return Array.Empty<string>();
    }

    public string GetIniValue(string path, string section, string key)
    {
        if (_iniValues.TryGetValue($"{path}|{section}|{key}", out var val))
            return val;
        return "";
    }

    public byte[] ReadAllBytes(string path)
    {
        if (_fileContents.TryGetValue(path, out var content))
            return content;
        return Array.Empty<byte>();
    }

    public Stream OpenRead(string path)
    {
        if (_fileContents.TryGetValue(path, out var content))
            return new MemoryStream(content);
        throw new FileNotFoundException(path);
    }

    public DateTime GetLastWriteTime(string path)
    {
        if (_fileTimes.TryGetValue(path, out var time))
            return time;
        return DateTime.FromFileTime(0); // 1601-01-01
    }
}
