using Launchbox.Helpers;
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace Launchbox.Services;

public class WindowsShortcutResolver : IShortcutResolver
{
    private readonly IFileSystem _fileSystem;

    public WindowsShortcutResolver(IFileSystem fileSystem)
    {
        _fileSystem = fileSystem;
    }

    public string? ResolveTarget(string shortcutPath)
    {
        if (string.IsNullOrWhiteSpace(shortcutPath)) return null;

        try
        {
            string extension = Path.GetExtension(shortcutPath).ToLowerInvariant();

            if (extension == ".lnk")
            {
                return ResolveLnk(shortcutPath);
            }
            else if (extension == ".url")
            {
                return ResolveUrl(shortcutPath);
            }

            // Not a shortcut
            return shortcutPath;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine($"Failed to resolve shortcut {shortcutPath}: {ex.Message}");
            return null;
        }
    }

    private string? ResolveLnk(string path)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
             return null;
        }

        try
        {
            IShellLinkW link = (IShellLinkW)new ShellLink();
            ((IPersistFile)link).Load(path, 0);
            var sb = new StringBuilder(260); // MAX_PATH
            link.GetPath(sb, sb.Capacity, IntPtr.Zero, 0);
            return sb.ToString();
        }
        catch (COMException)
        {
             return null;
        }
    }

    private string? ResolveUrl(string path)
    {
        // .url files are INI files
        string url = _fileSystem.GetIniValue(path, Constants.INTERNET_SHORTCUT_SECTION, "URL");
        if (!string.IsNullOrWhiteSpace(url))
        {
            return url;
        }
        return null;
    }
}
