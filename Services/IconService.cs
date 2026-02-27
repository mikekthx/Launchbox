using Launchbox.Helpers;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using WinIcon = System.Drawing.Icon;

namespace Launchbox.Services;

public class IconService(IFileSystem fileSystem) : IIconService
{
    private readonly IFileSystem _fileSystem = fileSystem;
    private readonly ConcurrentDictionary<string, IconCacheEntry> _iconCache = [];
    private readonly ConcurrentDictionary<string, Lazy<(bool Exists, HashSet<string>? Files, DateTime Timestamp)>> _directoryCache = [];
    private readonly ConcurrentDictionary<string, Lazy<(DateTime Timestamp, DateTime CacheTime)>> _fileTimestampCache = [];
    private static readonly TimeSpan CACHE_DURATION = TimeSpan.FromSeconds(2);
    // Lock object to serialize GDI+ operations which are not thread-safe even on separate instances
    private static readonly object GDI_LOCK = new();

    public int PruneCache(IEnumerable<string> activePaths)
    {
        // Clear directory cache to prevent memory leaks as it's only needed during bursts
        _directoryCache.Clear();
        _fileTimestampCache.Clear();

        var activeSet = new HashSet<string>(activePaths, StringComparer.OrdinalIgnoreCase);
        int removedCount = 0;

        foreach (var key in _iconCache.Keys)
        {
            if (!activeSet.Contains(key))
            {
                if (_iconCache.TryRemove(key, out _))
                {
                    removedCount++;
                }
            }
        }
        return removedCount;
    }

    internal string ResolveIconPath(string path)
    {
        if (PathSecurity.IsUnsafePath(path))
        {
            Trace.WriteLine($"Blocked resolution for unsafe path: {PathSecurity.RedactPath(path)}");
            return path;
        }

        if (path.EndsWith(".url", StringComparison.OrdinalIgnoreCase))
        {
            string iconFile = _fileSystem.GetIniValue(path, Constants.INTERNET_SHORTCUT_SECTION, Constants.ICON_FILE_KEY);

            if (string.IsNullOrWhiteSpace(iconFile))
            {
                return path;
            }

            // Expand environment variables to support system paths (e.g., %SystemRoot%)
            // and ensure path security checks are performed on the actual target path.
            // PERF: Only call ExpandEnvironmentVariables if needed to avoid overhead
            if (iconFile.Contains('%'))
            {
                iconFile = Environment.ExpandEnvironmentVariables(iconFile);
            }

            if (PathSecurity.IsUnsafePath(iconFile))
            {
                Trace.WriteLine($"Blocked potential unsafe icon path: {PathSecurity.RedactPath(iconFile)}");
                return path;
            }

            if (_fileSystem.FileExists(iconFile))
            {
                return iconFile;
            }
        }
        return path;
    }

    /// <summary>
    /// Extracts icon bytes for a given file path, utilizing an in-memory cache and supporting custom icons.
    /// </summary>
    /// <param name="path">The full path to the shortcut or file.</param>
    /// <returns>A byte array containing the PNG-encoded icon, or null if extraction fails.</returns>
    /// <remarks>
    /// This method first checks for custom icons (PNG or ICO) in a strictly defined `.icons` subdirectory.
    /// If no custom icon is found, it falls back to the system's default icon extraction.
    /// Results are cached based on file modification timestamps to minimize I/O operations.
    /// </remarks>
    public byte[]? ExtractIconBytes(string path)
    {
        if (PathSecurity.IsUnsafePath(path))
        {
            Trace.WriteLine($"Blocked icon extraction for unsafe path: {PathSecurity.RedactPath(path)}");
            return null;
        }

        // Expand environment variables (e.g., %APPDATA%) to ensure correct cache key
        // and that GetLastWriteTime works on the actual file.
        // This also aligns cache keys with PruneCache which typically receives absolute paths.
        string expandedPath = Environment.ExpandEnvironmentVariables(path);

        // 1. Gather current state (timestamps)
        // We check these every time to support live updates, but avoid expensive operations if unchanged.

        var shortcutTime = GetCachedLastWriteTime(expandedPath);

        string? directory = Path.GetDirectoryName(expandedPath);
        string name = Path.GetFileNameWithoutExtension(expandedPath);

        DateTime pngTime = DateTime.MinValue;
        DateTime icoTime = DateTime.MinValue;
        string? pngPath = null;
        string? icoPath = null;

        if (!string.IsNullOrEmpty(directory))
        {
            string iconsDir = Path.Combine(directory, Constants.ICONS_DIR);
            var (dirExists, dirFiles) = GetCachedDirectoryInfo(iconsDir);

            if (dirExists)
            {
                pngPath = Path.Combine(iconsDir, name + ".png");
                icoPath = Path.Combine(iconsDir, name + ".ico");

                bool pngExists = dirFiles == null || dirFiles.Contains(pngPath);
                bool icoExists = dirFiles == null || dirFiles.Contains(icoPath);

                if (pngExists) pngTime = GetCachedLastWriteTime(pngPath);
                if (icoExists) icoTime = GetCachedLastWriteTime(icoPath);
            }
        }

        // 2. Check Cache
        if (_iconCache.TryGetValue(expandedPath, out var entry))
        {
            if (entry.ShortcutTime == shortcutTime &&
                entry.PngTime == pngTime &&
                entry.IcoTime == icoTime)
            {
                return entry.Content;
            }
        }

        // 3. Cache Miss - Compute
        byte[]? iconBytes = null;

        // 3a. Try Custom Icon
        if (pngPath != null && icoPath != null)
        {
            iconBytes = GetCustomIconBytes(pngPath, icoPath, pngTime, icoTime);
        }

        // 3b. If no custom icon, Try System Icon
        if (iconBytes == null)
        {
            // Use expandedPath so ExtractSystemIcon->ResolveIconPath gets the correct path
            iconBytes = ExtractSystemIcon(expandedPath);
        }

        // 4. Update Cache
        _iconCache[expandedPath] = new IconCacheEntry(iconBytes, shortcutTime, pngTime, icoTime);

        return iconBytes;
    }

    /// <summary>
    /// Retrieves cached directory information (existence and file list) with a short expiration.
    /// </summary>
    /// <param name="directoryPath">The directory path to query.</param>
    /// <returns>A tuple containing a boolean indicating existence and a HashSet of file paths if successful.</returns>
    private (bool Exists, HashSet<string>? Files) GetCachedDirectoryInfo(string directoryPath)
    {
        // Loop to handle potential race conditions during cache expiration
        while (true)
        {
            var lazyEntry = _directoryCache.GetOrAdd(directoryPath, dir => new Lazy<(bool Exists, HashSet<string>? Files, DateTime Timestamp)>(() =>
            {
                bool exists = _fileSystem.DirectoryExists(dir);
                HashSet<string>? files = null;
                if (exists)
                {
                    try
                    {
                        var fileList = _fileSystem.GetFiles(dir);
                        files = new HashSet<string>(fileList, StringComparer.OrdinalIgnoreCase);
                    }
                    catch
                    {
                        files = null;
                    }
                }
                return (exists, files, DateTime.UtcNow);
            }));

            var dirEntry = lazyEntry.Value;

            if ((DateTime.UtcNow - dirEntry.Timestamp) < CACHE_DURATION)
            {
                return (dirEntry.Exists, dirEntry.Files);
            }
            else
            {
                // Cache expired, try to remove and retry
                _directoryCache.TryRemove(directoryPath, out _);
            }
        }
    }

    private DateTime GetCachedLastWriteTime(string path)
    {
        while (true)
        {
            var lazyEntry = _fileTimestampCache.GetOrAdd(path, p => new Lazy<(DateTime, DateTime)>(() =>
            {
                return (_fileSystem.GetLastWriteTime(p), DateTime.UtcNow);
            }));

            try
            {
                var entry = lazyEntry.Value;

                if ((DateTime.UtcNow - entry.CacheTime) < CACHE_DURATION)
                {
                    return entry.Timestamp;
                }
                else
                {
                    // Cache expired, try to remove and retry
                    _fileTimestampCache.TryRemove(path, out _);
                }
            }
            catch
            {
                // If the lazy value factory threw an exception (e.g., GetLastWriteTime failed),
                // remove the faulty entry so subsequent calls can retry instead of getting the cached exception.
                _fileTimestampCache.TryRemove(path, out _);

                // Return a safe default to prevent crashing the caller.
                // Using MinValue signals "very old" or "invalid", which is generally safe for timestamp checks.
                return DateTime.MinValue;
            }
        }
    }

    /// <summary>
    /// Attempts to load a custom icon (PNG or ICO) from the .icons directory.
    /// Prefers the file with the largest valid image area.
    /// </summary>
    private byte[]? GetCustomIconBytes(string pngPath, string icoPath, DateTime pngTime, DateTime icoTime)
    {
        bool pngValid = IsValidTimestamp(pngTime);
        bool icoValid = IsValidTimestamp(icoTime);

        if (!pngValid && !icoValid) return null;

        string chosenPath;
        if (pngValid && icoValid)
        {
            // Both exist, so we must decide which one is better.
            int pngArea = GetImageArea(pngPath, ImageHeaderParser.GetPngDimensions);
            int icoArea = GetImageArea(icoPath, ImageHeaderParser.GetMaxIcoDimensions);

            // Prefer larger resolution. If equal (or both invalid), prefer PNG for modern compatibility.
            chosenPath = (icoArea > pngArea) ? icoPath : pngPath;
        }
        else
        {
            // Only one exists
            chosenPath = pngValid ? pngPath : icoPath;
        }

        try
        {
            // Security: Limit file size to prevent DoS via large files
            if (_fileSystem.GetFileSize(chosenPath) > Constants.MAX_ICON_FILE_SIZE_BYTES)
            {
                Trace.WriteLine($"Blocked loading of large icon file: {PathSecurity.RedactPath(chosenPath)}");
                return null;
            }

            return _fileSystem.ReadAllBytes(chosenPath);
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"Failed to read custom icon {PathSecurity.RedactPath(chosenPath)}: {PathSecurity.GetSafeExceptionMessage(ex)}");
            return null;
        }
    }

    private static bool IsValidTimestamp(DateTime timestamp)
    {
        // GetLastWriteTime returns ~1601 for missing files, so we check > 1900
        return timestamp.Year > Constants.MIN_VALID_YEAR;
    }

    private int GetImageArea(string path, Func<Stream, (int Width, int Height)?> parser)
    {
        try
        {
            using var stream = _fileSystem.OpenRead(path);
            var dims = parser(stream);
            return dims.HasValue ? dims.Value.Width * dims.Value.Height : 0;
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"Failed to get image area for {PathSecurity.RedactPath(path)}: {PathSecurity.GetSafeExceptionMessage(ex)}");
            return 0;
        }
    }

    private byte[]? ExtractSystemIcon(string path)
    {
        IntPtr hIcon = IntPtr.Zero;
        string resolvedPath = path;

        try
        {
            resolvedPath = ResolveIconPath(path);

            // Optimized size: 96x96 is sufficient for UI (56x56) at up to ~170% DPI scaling,
            // saving ~43% processing time compared to 128x128.
            NativeMethods.PrivateExtractIcons(resolvedPath, 0, Constants.ICON_SIZE, Constants.ICON_SIZE, ref hIcon, IntPtr.Zero, 1, 0);
            if (hIcon == IntPtr.Zero) return null;

            lock (GDI_LOCK)
            {
                using var icon = WinIcon.FromHandle(hIcon);
                using var bmp = icon.ToBitmap();
                using var ms = new MemoryStream();
                bmp.Save(ms, ImageFormat.Png);
                return ms.ToArray();
            }
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"Failed to extract icon for {PathSecurity.RedactPath(path)} (resolved: {PathSecurity.RedactPath(resolvedPath)}): {PathSecurity.GetSafeExceptionMessage(ex)}");
            return null;
        }
        finally
        {
            if (hIcon != IntPtr.Zero)
                NativeMethods.DestroyIcon(hIcon);
        }
    }
}
