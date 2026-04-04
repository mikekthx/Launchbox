using Launchbox.Helpers;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace Launchbox.Services;

public class FileSystem : IFileSystem
{
    public void CreateDirectory(string path)
    {
        if (PathSecurity.IsUnsafePath(path)) throw new UnauthorizedAccessException($"Access to path '{PathSecurity.RedactPath(path)}' is denied.");
        Directory.CreateDirectory(path);
    }

    public bool DirectoryExists(string path)
    {
        if (PathSecurity.IsUnsafePath(path)) return false;
        return Directory.Exists(path);
    }

    public bool FileExists(string path)
    {
        if (PathSecurity.IsUnsafePath(path)) return false;
        return File.Exists(path);
    }

    public IEnumerable<string> EnumerateFiles(string path)
    {
        if (PathSecurity.IsUnsafePath(path)) return [];
        return Directory.EnumerateFiles(path);
    }

    public string GetIniValue(string path, string section, string key)
    {
        if (PathSecurity.IsUnsafePath(path)) return string.Empty;

        // Security: Prevent symlink redirection attacks on INI files (like .url)
        try
        {
            if (File.Exists(path))
            {
                var attr = File.GetAttributes(path);
                if ((attr & FileAttributes.ReparsePoint) == FileAttributes.ReparsePoint)
                {
                    Trace.WriteLine($"Blocked INI read on reparse point: {PathSecurity.RedactPath(path)}");
                    return string.Empty;
                }
            }
        }
        catch (Exception ex)
        {
            // Fail closed on any file access error (e.g. Access Denied)
            Trace.WriteLine($"Failed to validate INI file {PathSecurity.RedactPath(path)}: {PathSecurity.GetSafeExceptionMessage(ex)}");
            return string.Empty;
        }

        // Fast-path: allocate buffer on the stack for typical small INI values to avoid ArrayPool rent/return overhead
        Span<char> stackBuffer = stackalloc char[512];
        int ret = NativeMethods.GetPrivateProfileString(section, key, string.Empty, ref MemoryMarshal.GetReference(stackBuffer), stackBuffer.Length, path);

        if (ret < stackBuffer.Length - 2)
        {
            if (ret == 0) return string.Empty;
            return new string(stackBuffer.Slice(0, ret));
        }

        // Fallback: value exceeds stack buffer size, allocate larger buffer from ArrayPool
        int capacity = stackBuffer.Length * 2;
        char[] buffer = ArrayPool<char>.Shared.Rent(capacity);
        try
        {
            while (true)
            {
                int size = buffer.Length;
                ret = NativeMethods.GetPrivateProfileString(section, key, string.Empty, buffer, size, path);

                // Truncation check: GetPrivateProfileString returns size - 1 or size - 2 when the
                // provided buffer is insufficient for the full INI value.
                if (ret < size - 2)
                {
                    return new string(buffer.AsSpan(0, ret));
                }

                // Truncated. Loop to double the buffer size until it fits.
                int newCapacity = size * 2;
                if (newCapacity > 65536)
                {
                    // Safety limit to prevent infinite allocation.
                    // Accept truncated result if value exceeds 64KB limit to avoid excessive allocation.
                    return new string(buffer.AsSpan(0, ret));
                }

                var newBuffer = ArrayPool<char>.Shared.Rent(newCapacity);
                ArrayPool<char>.Shared.Return(buffer);
                buffer = newBuffer;
            }
        }
        finally
        {
            ArrayPool<char>.Shared.Return(buffer);
        }
    }

    public byte[] ReadAllBytes(string path)
    {
        if (PathSecurity.IsUnsafePath(path)) throw new UnauthorizedAccessException($"Access to path '{PathSecurity.RedactPath(path)}' is denied.");
        return File.ReadAllBytes(path);
    }

    public Stream OpenRead(string path)
    {
        if (PathSecurity.IsUnsafePath(path)) throw new UnauthorizedAccessException($"Access to path '{PathSecurity.RedactPath(path)}' is denied.");
        return File.OpenRead(path);
    }

    public DateTime GetLastWriteTime(string path)
    {
        if (PathSecurity.IsUnsafePath(path)) throw new UnauthorizedAccessException($"Access to path '{PathSecurity.RedactPath(path)}' is denied.");
        return File.GetLastWriteTime(path);
    }

    public long GetFileSize(string path)
    {
        if (PathSecurity.IsUnsafePath(path)) throw new UnauthorizedAccessException($"Access to path '{PathSecurity.RedactPath(path)}' is denied.");
        return new FileInfo(path).Length;
    }

    public IDisposable WatchDirectory(string path, Action callback)
    {
        if (PathSecurity.IsUnsafePath(path) || !Directory.Exists(path))
            return NullDisposable.INSTANCE;

        try
        {
            var watcher = new FileSystemWatcher(path)
            {
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite,
                IncludeSubdirectories = false,
            };

            FileSystemEventHandler handler = (_, _) => callback();
            RenamedEventHandler renamedHandler = (_, _) => callback();
            // Log buffer overflow and force a reload so missed events are recovered.
            // InternalBufferOverflowException is raised when the OS-level event buffer fills
            // up (default 8 KB), which can happen on busy folders.
            ErrorEventHandler errorHandler = (_, e) =>
            {
                Trace.WriteLine($"FileSystemWatcher buffer overflow for {PathSecurity.RedactPath(path)}: {PathSecurity.GetSafeExceptionMessage(e.GetException())}");
                callback();
            };

            watcher.Created += handler;
            watcher.Deleted += handler;
            watcher.Changed += handler;
            watcher.Renamed += renamedHandler;
            watcher.Error += errorHandler;

            // Subscribe all handlers before enabling events to eliminate the small window
            // where events could fire before handlers are attached.
            watcher.EnableRaisingEvents = true;

            return new WatcherHandle(watcher, handler, renamedHandler, errorHandler);
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"Failed to watch directory {PathSecurity.RedactPath(path)}: {PathSecurity.GetSafeExceptionMessage(ex)}");
            return NullDisposable.INSTANCE;
        }
    }

    private sealed class NullDisposable : IDisposable
    {
        internal static readonly NullDisposable INSTANCE = new();
        public void Dispose() { }
    }

    private sealed class WatcherHandle : IDisposable
    {
        private readonly FileSystemWatcher _watcher;
        private readonly FileSystemEventHandler _handler;
        private readonly RenamedEventHandler _renamedHandler;
        private readonly ErrorEventHandler _errorHandler;

        internal WatcherHandle(FileSystemWatcher watcher, FileSystemEventHandler handler, RenamedEventHandler renamedHandler, ErrorEventHandler errorHandler)
        {
            _watcher = watcher;
            _handler = handler;
            _renamedHandler = renamedHandler;
            _errorHandler = errorHandler;
        }

        public void Dispose()
        {
            _watcher.EnableRaisingEvents = false;
            _watcher.Created -= _handler;
            _watcher.Deleted -= _handler;
            _watcher.Changed -= _handler;
            _watcher.Renamed -= _renamedHandler;
            _watcher.Error -= _errorHandler;
            _watcher.Dispose();
        }
    }
}
