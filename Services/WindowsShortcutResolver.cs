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

    public ShortcutMetadata? Resolve(string shortcutPath)
    {
        if (string.IsNullOrWhiteSpace(shortcutPath)) return null;

        try
        {
            if (shortcutPath.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase))
            {
                return ResolveLnkAll(shortcutPath);
            }
            else if (shortcutPath.EndsWith(".url", StringComparison.OrdinalIgnoreCase))
            {
                return new ShortcutMetadata(ResolveUrl(shortcutPath), null, null);
            }

            // Not a shortcut
            return new ShortcutMetadata(shortcutPath, null, null);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine($"Failed to resolve shortcut {PathSecurity.RedactPath(shortcutPath)}: {PathSecurity.GetSafeExceptionMessage(ex)}");
            return null;
        }
    }

    public string? ResolveTarget(string shortcutPath)
    {
        return Resolve(shortcutPath)?.Target;
    }

    public string? ResolveArguments(string shortcutPath)
    {
        return Resolve(shortcutPath)?.Arguments;
    }

    public string? ResolveWorkingDirectory(string shortcutPath)
    {
        return Resolve(shortcutPath)?.WorkingDirectory;
    }

    private ShortcutMetadata? ResolveLnkAll(string path)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return null;
        }

        IShellLinkW? link = null;
        try
        {
            link = (IShellLinkW)new ShellLink();
            ((IPersistFile)link).Load(path, 0);

            var pathSb = new StringBuilder(260); // MAX_PATH
            link.GetPath(pathSb, pathSb.Capacity, IntPtr.Zero, 0);

            var argsSb = new StringBuilder(1024);
            link.GetArguments(argsSb, argsSb.Capacity);

            var dirSb = new StringBuilder(260); // MAX_PATH
            link.GetWorkingDirectory(dirSb, dirSb.Capacity);

            return new ShortcutMetadata(pathSb.ToString(), argsSb.ToString(), dirSb.ToString());
        }
        // COM resolution can fail for shortcuts pointing to nonexistent or inaccessible targets.
        // Swallow and return null — a single bad .lnk should not break the entire loading pipeline.
        catch (COMException)
        {
            return null;
        }
        finally
        {
            if (link != null)
            {
                Marshal.ReleaseComObject(link);
            }
        }
    }

    private string? ResolveUrl(string path)
    {
        // .url files are INI files
        string url = _fileSystem.GetIniValue(path, Constants.INTERNET_SHORTCUT_SECTION, "URL");
        if (string.IsNullOrWhiteSpace(url)) return null;

        if (url.Contains('%') || url.Contains('$'))
        {
            url = Environment.ExpandEnvironmentVariables(url);
        }

        // Security: Ensure URL scheme is safe (restricted to http/https).
        // This prevents .url files from being used to execute local files (file://) or other unsafe schemes.
        if (Uri.TryCreate(url, UriKind.Absolute, out Uri? uri))
        {
            if (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
            {
                return url;
            }
        }

        return null;
    }
}
