using Launchbox.Helpers;
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace Launchbox.Services;

/// <summary>
/// Resolves Windows shortcut files (.lnk and .url) to extract their target path, arguments, and working directory.
/// </summary>
public class WindowsShortcutResolver : IShortcutResolver
{
    private const int MAX_PATH = 4096;
    private const int MAX_ARGS_LENGTH = 8192;

    private readonly IFileSystem _fileSystem;

    public WindowsShortcutResolver(IFileSystem fileSystem)
    {
        _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
    }

    /// <summary>
    /// Parses the provided shortcut file and extracts its underlying metadata.
    /// Returns null if the path is invalid or if COM resolution fails.
    /// </summary>
    /// <param name="shortcutPath">The full path to the .lnk or .url file.</param>
    /// <returns>A <see cref="ShortcutMetadata"/> object containing the resolved path, arguments, and working directory.</returns>
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

            var pathSb = new StringBuilder(MAX_PATH);
            link.GetPath(pathSb, pathSb.Capacity, IntPtr.Zero, 0);

            var argsSb = new StringBuilder(MAX_ARGS_LENGTH);
            link.GetArguments(argsSb, argsSb.Capacity);

            var dirSb = new StringBuilder(MAX_PATH);
            link.GetWorkingDirectory(dirSb, dirSb.Capacity);

            // Security: detect buffer truncation. If any field fills the entire buffer,
            // the value was likely truncated — return null to fail closed rather than
            // validating incomplete data.
            if (pathSb.Length >= MAX_PATH - 1 ||
                argsSb.Length >= MAX_ARGS_LENGTH - 1 ||
                dirSb.Length >= MAX_PATH - 1)
            {
                System.Diagnostics.Trace.WriteLine($"Blocked shortcut with truncated metadata: {PathSecurity.RedactPath(path)}");
                return null;
            }

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

        if (url.Contains('%'))
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
