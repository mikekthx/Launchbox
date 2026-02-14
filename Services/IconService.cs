using Launchbox.Helpers;
using System;
using System.Diagnostics;
using System.Drawing.Imaging;
using System.Collections.Concurrent;
using System.IO;
using System.Runtime.InteropServices;
using WinIcon = System.Drawing.Icon;

namespace Launchbox.Services;

public class IconService
{
    private readonly IFileSystem _fileSystem;
    private readonly ConcurrentDictionary<string, IconCacheEntry> _iconCache = new();

    public IconService(IFileSystem fileSystem)
    {
        _fileSystem = fileSystem;
    }

    public string ResolveIconPath(string path)
    {
        if (path.EndsWith(".url", StringComparison.OrdinalIgnoreCase))
        {
            string iconFile = _fileSystem.GetIniValue(path, "InternetShortcut", "IconFile");

            if (IsUnsafePath(iconFile))
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

    private bool IsUnsafePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;

        // Check for NT object path prefix (\??\) which can bypass UNC checks
        if (path.StartsWith(@"\??\", StringComparison.OrdinalIgnoreCase)) return true;

        // Check for specific UNC patterns
        if (path.StartsWith(@"\\?\UNC", StringComparison.OrdinalIgnoreCase)) return true;

        // Allow local long paths ONLY if they are standard drive paths (e.g. \\?\C:\...)
        if (path.StartsWith(@"\\?\", StringComparison.OrdinalIgnoreCase))
        {
            // Must be at least 7 chars: \\?\C:\
            if (path.Length >= 7 &&
                char.IsLetter(path[4]) &&
                path[5] == ':' &&
                path[6] == '\\')
            {
                return false;
            }
            return true; // Block anything else starting with \\?\ (e.g. GLOBALROOT, Volume, etc.)
        }

        // Check for standard UNC paths
        if (path.StartsWith(@"\\") || path.StartsWith("//")) return true;

        // Check using Uri as a backup
        try
        {
            if (new Uri(path).IsUnc) return true;
        }
        catch { }

        return false;
    }

    public byte[]? ExtractIconBytes(string path)
    {
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
             string iconsDir = Path.Combine(directory, ".icons");

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
        bool pngExists = pngTime.Year > 1900;
        bool icoExists = icoTime.Year > 1900;

        if (!pngExists && !icoExists) return null;

        string? chosenPath = null;

        if (pngExists && icoExists)
        {
            int pngArea = 0;
            int icoArea = 0;

            try
            {
                using (var stream = _fileSystem.OpenRead(pngPath))
                {
                    var dims = ImageHeaderParser.GetPngDimensions(stream);
                    if (dims != null) pngArea = dims.Value.Width * dims.Value.Height;
                }
            }
            catch { }

            try
            {
                using (var stream = _fileSystem.OpenRead(icoPath))
                {
                    var dims = ImageHeaderParser.GetMaxIcoDimensions(stream);
                    if (dims != null) icoArea = dims.Value.Width * dims.Value.Height;
                }
            }
            catch { }

            // Prefer larger resolution
            // If areas are equal (or both failed to parse), prefer PNG (modern/compatible)
            if (icoArea > pngArea) chosenPath = icoPath;
            else chosenPath = pngPath;
        }
        else if (pngExists)
        {
            chosenPath = pngPath;
        }
        else if (icoExists)
        {
            chosenPath = icoPath;
        }

        if (chosenPath != null)
        {
            try
            {
                return _fileSystem.ReadAllBytes(chosenPath);
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"Failed to read custom icon {chosenPath}: {ex.Message}");
                return null;
            }
        }

        return null;
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
            NativeMethods.PrivateExtractIcons(resolvedPath, 0, 96, 96, ref hIcon, IntPtr.Zero, 1, 0);
            if (hIcon == IntPtr.Zero) return null;

            using var icon = WinIcon.FromHandle(hIcon);
            using var bmp = icon.ToBitmap();
            using var ms = new MemoryStream();
            bmp.Save(ms, ImageFormat.Png);
            return ms.ToArray();
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
