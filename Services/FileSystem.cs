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
}
