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

    // Security: explicit allowlist of safe URI schemes for .url files.
    // An allowlist is mandatory here — Windows has hundreds of undocumented built-in URI schemes
    // (several with historical CVEs, e.g. ms-msdt/Follina, ms-appinstaller/Emotet, search-ms)
    // and third-party apps can register arbitrary schemes with unpredictable argument handling.
    // Only schemes whose handlers are well-known, widely deployed, and incapable of direct
    // filesystem access or arbitrary code execution are included.
    private static readonly HashSet<string> ALLOWED_URL_SCHEMES = new(StringComparer.OrdinalIgnoreCase)
    {
        // Web
        "http", "https",

        // Email / telephony
        "mailto", "tel", "callto", "sip",

        // Gaming clients
        "steam",                   // Steam
        "com.epicgames.launcher",  // Epic Games Launcher
        "xbox",                    // Xbox app
        "goggalaxy",               // GOG Galaxy
        "origin", "origin2",       // EA / Origin
        "battlenet",               // Blizzard Battle.net
        "uplay", "ubisoft-connect",// Ubisoft Connect
        "roblox", "roblox-player", // Roblox

        // Communication
        "discord",
        "msteams", "teamscmd",     // Microsoft Teams
        "slack",
        "zoommtg", "zoomus",       // Zoom
        "skype",
        "tg", "telegram",          // Telegram
        "whatsapp",

        // Media
        "spotify",
        "itunes",

        // Development tools
        "vscode", "vscode-insiders",
        "obsidian",
        "jetbrains",               // JetBrains Toolbox
        "github-windows",          // GitHub Desktop
        "postman",
        "figma",

        // Safe Windows built-in utilities
        // Excluded: ms-msdt (Follina/CVE-2022-30190), search-ms/search (SMB relay),
        //           ms-appinstaller (Emotet vector), ms-officecmd (parameter injection),
        //           shell: (arbitrary shell commands), mhtml: (IE rendering bypass)
        "ms-settings",
        "ms-windows-store",
        "ms-calculator", "calculator",
        "ms-clock",
        "ms-photos",
        "ms-paint",
        "ms-screenclip",
    };

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

        // Security: only pass through URLs whose scheme is in the explicit allowlist.
        // See ALLOWED_URL_SCHEMES above for the rationale — blocklist is not viable here.
        if (Uri.TryCreate(url, UriKind.Absolute, out Uri? uri) && ALLOWED_URL_SCHEMES.Contains(uri.Scheme))
        {
            return url;
        }

        return null;
    }
}
