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
    private readonly ConcurrentDictionary<string, string?> _customIconCache = new();

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

        // Allow local long paths (e.g. \\?\C:\...)
        if (path.StartsWith(@"\\?\", StringComparison.OrdinalIgnoreCase)) return false;

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
        if (_customIconCache.TryGetValue(shortcutPath, out string? cachedPath))
        {
            if (cachedPath == null) return null;
            if (_fileSystem.FileExists(cachedPath))
            {
                return _fileSystem.ReadAllBytes(cachedPath);
            }
            // If cached file no longer exists, invalidate cache and continue
            _customIconCache.TryRemove(shortcutPath, out _);
        }

        string? directory = Path.GetDirectoryName(shortcutPath);
        if (string.IsNullOrEmpty(directory)) return null;

        string iconsDir = Path.Combine(directory, ".icons");
        if (!_fileSystem.DirectoryExists(iconsDir))
        {
            _customIconCache[shortcutPath] = null;
            return null;
        }

        string name = Path.GetFileNameWithoutExtension(shortcutPath);
        string pngPath = Path.Combine(iconsDir, name + ".png");
        string icoPath = Path.Combine(iconsDir, name + ".ico");

        bool pngExists = _fileSystem.FileExists(pngPath);
        bool icoExists = _fileSystem.FileExists(icoPath);

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

        _customIconCache[shortcutPath] = chosenPath;

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
