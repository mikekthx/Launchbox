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
    private readonly ConcurrentDictionary<string, IconCacheEntry> _customIconCache = new();

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
        // Try to load custom icon first (from Shortcuts\.icons)
        var customIcon = TryGetCustomIconBytes(path);
        if (customIcon != null) return customIcon;

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

    private byte[]? TryGetCustomIconBytes(string shortcutPath)
    {
        string? directory = Path.GetDirectoryName(shortcutPath);
        if (string.IsNullOrEmpty(directory)) return null;

        string iconsDir = Path.Combine(directory, ".icons");
        string name = Path.GetFileNameWithoutExtension(shortcutPath);
        string pngPath = Path.Combine(iconsDir, name + ".png");
        string icoPath = Path.Combine(iconsDir, name + ".ico");

        // Check for file existence first (avoids relying on GetLastWriteTime magic date for missing files)
        bool pngExists = _fileSystem.FileExists(pngPath);
        bool icoExists = _fileSystem.FileExists(icoPath);

        // Get timestamps only if files exist
        DateTime pngTime = pngExists ? _fileSystem.GetLastWriteTime(pngPath) : DateTime.MinValue;
        DateTime icoTime = icoExists ? _fileSystem.GetLastWriteTime(icoPath) : DateTime.MinValue;

        // Check cache
        if (_customIconCache.TryGetValue(shortcutPath, out IconCacheEntry? cachedEntry))
        {
            // Valid if timestamps haven't changed
            if (cachedEntry.PngTime == pngTime && cachedEntry.IcoTime == icoTime)
            {
                // Verify selected file still exists (edge case: timestamp matching MinValue but file gone?
                // actually GetLastWriteTime handles existence check, so if file deleted, time becomes MinValue,
                // which won't match old valid time. So this check is robust).
                if (cachedEntry.SelectedPath == null) return null;

                // Double check existence just in case, though timestamp check covers most cases
                if (_fileSystem.FileExists(cachedEntry.SelectedPath))
                {
                    return _fileSystem.ReadAllBytes(cachedEntry.SelectedPath);
                }
            }
        }

        if (!pngExists && !icoExists)
        {
            _customIconCache[shortcutPath] = new IconCacheEntry(null, pngTime, icoTime);
            return null;
        }

        string? chosenPath = null;

        if (pngExists && icoExists)
        {
            // If both exist, compare dimensions to pick the better quality one.
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

        // Update cache
        _customIconCache[shortcutPath] = new IconCacheEntry(chosenPath, pngTime, icoTime);

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
}
