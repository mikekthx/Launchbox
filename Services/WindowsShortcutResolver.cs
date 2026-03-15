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
            if (shortcutPath.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase))
            {
                return ResolveLnk(shortcutPath);
            }
            else if (shortcutPath.EndsWith(".url", StringComparison.OrdinalIgnoreCase))
            {
                return ResolveUrl(shortcutPath);
            }

            // Not a shortcut
            return shortcutPath;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine($"Failed to resolve shortcut {PathSecurity.RedactPath(shortcutPath)}: {PathSecurity.GetSafeExceptionMessage(ex)}");
            return null;
        }
    }

    public string? ResolveArguments(string shortcutPath)
    {
        if (string.IsNullOrWhiteSpace(shortcutPath) || !shortcutPath.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase))
            return null;

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return null;

        IShellLinkW? link = null;
        try
        {
            link = (IShellLinkW)new ShellLink();
            ((IPersistFile)link).Load(shortcutPath, 0);
            // Arguments can be longer than MAX_PATH (260); use a larger buffer.
            var sb = new StringBuilder(1024);
            link.GetArguments(sb, sb.Capacity);
            return sb.ToString();
        }
        // COM resolution can fail for shortcuts pointing to nonexistent or inaccessible targets.
        // Swallow and return null — a single bad .lnk should not break the entire loading pipeline.
        catch (COMException) { return null; }
        finally { if (link != null) Marshal.ReleaseComObject(link); }
    }

    public string? ResolveWorkingDirectory(string shortcutPath)
    {
        if (string.IsNullOrWhiteSpace(shortcutPath) || !shortcutPath.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase))
            return null;

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return null;

        IShellLinkW? link = null;
        try
        {
            link = (IShellLinkW)new ShellLink();
            ((IPersistFile)link).Load(shortcutPath, 0);
            var sb = new StringBuilder(260); // MAX_PATH
            link.GetWorkingDirectory(sb, sb.Capacity);
            return sb.ToString();
        }
        catch (COMException) { return null; }
        finally { if (link != null) Marshal.ReleaseComObject(link); }
    }

    private string? ResolveLnk(string path)
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
            var sb = new StringBuilder(260); // MAX_PATH
            link.GetPath(sb, sb.Capacity, IntPtr.Zero, 0);
            return sb.ToString();
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
