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

public class IconService(IFileSystem fileSystem)
{
    private readonly IFileSystem _fileSystem = fileSystem;
    private readonly ConcurrentDictionary<string, IconCacheEntry> _iconCache = [];
    private readonly object _gdiLock = new();

    public int PruneCache(IEnumerable<string> activePaths)
    {
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

    public string ResolveIconPath(string path)
    {
        if (PathSecurity.IsUnsafePath(path))
        {
            Trace.WriteLine($"Blocked resolution for unsafe path: {path}");
            return path;
        }

        if (path.EndsWith(".url", StringComparison.OrdinalIgnoreCase))
        {
            string iconFile = _fileSystem.GetIniValue(path, Constants.INTERNET_SHORTCUT_SECTION, Constants.ICON_FILE_KEY);
            iconFile = Environment.ExpandEnvironmentVariables(iconFile);

            // Expand environment variables to support system paths (e.g., %SystemRoot%)
            // and ensure path security checks are performed on the actual target path.
            iconFile = Environment.ExpandEnvironmentVariables(iconFile);

            if (PathSecurity.IsUnsafePath(iconFile))
            {
                Trace.WriteLine($"Blocked potential unsafe icon path: {iconFile}");
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
            Trace.WriteLine($"Blocked icon extraction for unsafe path: {path}");
            return null;
        }

        // 1. Gather current state (timestamps)
        // We check these every time to support live updates, but avoid expensive operations if unchanged.

        var shortcutTime = _fileSystem.GetLastWriteTime(path);

        string? directory = Path.GetDirectoryName(path);
        string name = Path.GetFileNameWithoutExtension(path);

        DateTime pngTime = DateTime.MinValue;
        DateTime icoTime = DateTime.MinValue;
        string? pngPath = null;
        string? icoPath = null;

        if (!string.IsNullOrEmpty(directory))
        {
            string iconsDir = Path.Combine(directory, Constants.ICONS_DIR);

            // Optimization: Check if .icons directory exists before checking for individual files
            // This significantly reduces syscalls (checking 1 directory vs 2 potentially missing files)
            if (_fileSystem.DirectoryExists(iconsDir))
            {
                pngPath = Path.Combine(iconsDir, name + ".png");
                icoPath = Path.Combine(iconsDir, name + ".ico");

                // Optimization: GetLastWriteTime returns 1601 date if file missing, avoiding extra FileExists check
                pngTime = _fileSystem.GetLastWriteTime(pngPath);
                icoTime = _fileSystem.GetLastWriteTime(icoPath);
            }
        }

        // 2. Check Cache
        if (_iconCache.TryGetValue(path, out var entry))
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
            iconBytes = ExtractSystemIcon(path);
        }

        // 4. Update Cache
        _iconCache[path] = new IconCacheEntry(iconBytes, shortcutTime, pngTime, icoTime);

        return iconBytes;
    }

    private byte[]? GetCustomIconBytes(string pngPath, string icoPath, DateTime pngTime, DateTime icoTime)
    {
        // Check year > 1900 because GetLastWriteTime returns ~1601 for missing files
        bool pngExists = pngTime.Year > Constants.MIN_VALID_YEAR;
        bool icoExists = icoTime.Year > Constants.MIN_VALID_YEAR;

        if (!pngExists && !icoExists) return null;

        string? chosenPath = null;

        if (pngExists && icoExists)
        {
            int pngArea = GetImageArea(pngPath, ImageHeaderParser.GetPngDimensions);
            int icoArea = GetImageArea(icoPath, ImageHeaderParser.GetMaxIcoDimensions);

            // Prefer larger resolution
            // If areas are equal (or both failed to parse), prefer PNG (modern/compatible)
            chosenPath = (icoArea > pngArea) ? icoPath : pngPath;
        }
        else
        {
            chosenPath = pngExists ? pngPath : icoPath;
        }

        try
        {
            // Security: Limit file size to 5MB to prevent DoS via large files
            if (_fileSystem.GetFileSize(chosenPath) > 5 * 1024 * 1024)
            {
                Trace.WriteLine($"Blocked loading of large icon file: {chosenPath}");
                return null;
            }

            return _fileSystem.ReadAllBytes(chosenPath);
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"Failed to read custom icon {chosenPath}: {ex.Message}");
            return null;
        }
    }

    private int GetImageArea(string path, Func<Stream, (int Width, int Height)?> parser)
    {
        try
        {
            using var stream = _fileSystem.OpenRead(path);
            var dims = parser(stream);
            return dims.HasValue ? dims.Value.Width * dims.Value.Height : 0;
        }
        catch
        {
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

            lock (_gdiLock)
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
            Trace.WriteLine($"Failed to extract icon for {path} (resolved: {resolvedPath}): {ex.Message}");
            return null;
        }
        finally
        {
            if (hIcon != IntPtr.Zero)
                NativeMethods.DestroyIcon(hIcon);
        }
    }
}
