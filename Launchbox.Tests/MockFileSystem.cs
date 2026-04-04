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
    private readonly Dictionary<string, List<Action>> _watchers = [];
    private readonly Dictionary<string, string> _iniValues = new();
    private readonly Dictionary<string, byte[]> _fileContents = new();
    private readonly Dictionary<string, long> _fileSizes = new();
    private readonly Dictionary<string, DateTime> _fileTimes = new();

    public List<string> OperationsLog { get; } = new();

    /// <summary>When set, <see cref="EnumerateFiles"/> and <see cref="GetFiles"/> throw this exception.</summary>
    public Exception? EnumerateFilesException { get; set; }

    /// <summary>When set, <see cref="ReadAllBytes"/> and <see cref="OpenRead"/> throw this exception.</summary>
    public Exception? ReadException { get; set; }

    /// <summary>When set, <see cref="GetIniValue"/> throws this exception.</summary>
    public Exception? GetIniValueException { get; set; }

    /// <summary>When set, <see cref="CreateDirectory"/> throws this exception.</summary>
    public Exception? CreateDirectoryException { get; set; }

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
            _files[directory] = [];
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

    public virtual void CreateDirectory(string path)
    {
        if (CreateDirectoryException != null) throw CreateDirectoryException;
        _directories.Add(path);
    }

    public bool DirectoryExists(string path)
    {
        return _directories.Contains(path);
    }

    public bool FileExists(string path)
    {
        return _files.Values.Any(list => list.Contains(path));
    }

    public virtual IEnumerable<string> EnumerateFiles(string path)
    {
        if (EnumerateFilesException != null) throw EnumerateFilesException;
        if (_files.TryGetValue(path, out var files))
        {
            return files;
        }
        return [];
    }

    public virtual string GetIniValue(string path, string section, string key)
    {
        if (GetIniValueException != null) throw GetIniValueException;
        OperationsLog.Add($"GetIniValue: {path}");
        if (_iniValues.TryGetValue($"{path}|{section}|{key}", out var val))
            return val;
        return "";
    }

    public virtual byte[] ReadAllBytes(string path)
    {
        if (ReadException != null) throw ReadException;
        if (_fileContents.TryGetValue(path, out var content))
            return content;
        return [];
    }

    public Stream OpenRead(string path)
    {
        if (ReadException != null) throw ReadException;
        if (_fileContents.TryGetValue(path, out var content))
            return new MemoryStream(content);
        throw new FileNotFoundException(path);
    }

    public virtual DateTime GetLastWriteTime(string path)
    {
        if (_fileTimes.TryGetValue(path, out var time))
            return time;
        return DateTime.FromFileTime(0); // 1601-01-01
    }

    public long GetFileSize(string path)
    {
        if (_fileSizes.TryGetValue(path, out var size))
            return size;
        throw new FileNotFoundException(path);
    }

    public IDisposable WatchDirectory(string path, Action callback)
    {
        if (!_watchers.TryGetValue(path, out var list))
        {
            list = [];
            _watchers[path] = list;
        }
        list.Add(callback);
        return new MockWatcherHandle(list, callback);
    }

    /// <summary>Returns the number of active watchers on <paramref name="directoryPath"/>.</summary>
    public int GetWatcherCount(string directoryPath) =>
        _watchers.TryGetValue(directoryPath, out var list) ? list.Count : 0;

    /// <summary>Simulates a file system change in the given directory, firing all registered callbacks.</summary>
    public void SimulateChange(string directoryPath)
    {
        if (_watchers.TryGetValue(directoryPath, out var callbacks))
        {
            foreach (var cb in new List<Action>(callbacks)) cb();
        }
    }

    private sealed class MockWatcherHandle : IDisposable
    {
        private readonly List<Action> _list;
        private readonly Action _callback;

        internal MockWatcherHandle(List<Action> list, Action callback)
        {
            _list = list;
            _callback = callback;
        }

        public void Dispose() => _list.Remove(_callback);
    }
}
