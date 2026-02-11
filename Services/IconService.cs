using System;
using System.Diagnostics;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using WinIcon = System.Drawing.Icon;

namespace Launchbox.Services;

public class IconService
{
    private readonly IFileSystem _fileSystem;

    public IconService(IFileSystem fileSystem)
    {
        _fileSystem = fileSystem;
    }

    public string ResolveIconPath(string path)
    {
        if (path.EndsWith(".url", StringComparison.OrdinalIgnoreCase))
        {
            string iconFile = _fileSystem.GetIniValue(path, "InternetShortcut", "IconFile");
            if (_fileSystem.FileExists(iconFile))
            {
                return iconFile;
            }
        }
        return path;
    }

    public byte[]? ExtractIconBytes(string path)
    {
        IntPtr hIcon = IntPtr.Zero;
        string resolvedPath = path;

        try
        {
            resolvedPath = ResolveIconPath(path);

            NativeMethods.PrivateExtractIcons(resolvedPath, 0, 128, 128, ref hIcon, IntPtr.Zero, 1, 0);
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
