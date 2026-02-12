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

    public void AddDirectory(string path)
    {
        _directories.Add(path);
    }

    public void AddFile(string directory, string filename)
    {
        if (!_files.ContainsKey(directory))
        {
            _files[directory] = new List<string>();
        }
        _files[directory].Add(Path.Combine(directory, filename));
    }

    public void AddFile(string fullPath)
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
        _files[directory].Add(fullPath);
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
}
