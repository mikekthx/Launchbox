using Launchbox.Helpers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace Launchbox.Services;

public class WinUILauncher : IAppLauncher
{
    private static readonly IReadOnlyList<string> ALLOWED_EXTENSIONS = Constants.ALLOWED_EXTENSIONS;
    private readonly IShortcutResolver _shortcutResolver;
    private readonly IProcessStarter _processStarter;
    private readonly IFileSystem _fileSystem;

    public WinUILauncher(IShortcutResolver shortcutResolver, IProcessStarter processStarter, IFileSystem fileSystem)
    {
        _shortcutResolver = shortcutResolver ?? throw new ArgumentNullException(nameof(shortcutResolver));
        _processStarter = processStarter ?? throw new ArgumentNullException(nameof(processStarter));
        _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
    }

    public void Launch(string path)
    {
        if (PathSecurity.IsUnsafePath(path))
        {
            Trace.WriteLine($"Blocked execution of unsafe file: {PathSecurity.RedactPath(path)}");
            return;
        }

        if (!_fileSystem.FileExists(path))
        {
            Trace.WriteLine($"Blocked execution of non-existent file: {PathSecurity.RedactPath(path)}");
            return;
        }

        bool isLnk = path.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase);
        bool isUrl = path.EndsWith(".url", StringComparison.OrdinalIgnoreCase);

        // Check against allowed extensions without allocating new strings
        if (!ALLOWED_EXTENSIONS.Any(ext => path.EndsWith(ext, StringComparison.OrdinalIgnoreCase)))
        {
            Trace.WriteLine($"Blocked execution of unauthorized file: {PathSecurity.RedactPath(path)}");
            return;
        }

        // Validate shortcut target (Defense-in-depth)
        if (isLnk || isUrl)
        {
            var metadata = _shortcutResolver.Resolve(path);
            string? targetPath = metadata?.Target;

            // .url files MUST resolve to a safe scheme (http/https). A null result means
            // the resolver rejected the scheme, so block execution to prevent shell-executing
            // arbitrary URI schemes (file://, ms-settings://, custom handlers, etc.).
            // .lnk files may return null from COM resolution for valid shortcuts the shell
            // can still handle, so only .url null-resolution is treated as a security block.
            if (string.IsNullOrEmpty(targetPath))
            {
                if (isUrl)
                {
                    Trace.WriteLine($"Blocked execution of .url with unresolvable or unsafe scheme: {PathSecurity.RedactPath(path)}");
                    return;
                }

                // .lnk files may return null from COM resolution for valid shortcuts the shell
                // can still handle. Log for diagnostics but allow launch to proceed.
                Trace.WriteLine($"Launching .lnk with unresolved target (COM resolution failed): {PathSecurity.RedactPath(path)}");
            }
            else
            {
                if (PathSecurity.IsUnsafePath(targetPath))
                {
                    Trace.WriteLine($"Blocked execution of shortcut pointing to unsafe target: {PathSecurity.RedactPath(targetPath)}");
                    return;
                }
                Trace.WriteLine($"Shortcut target validated: {PathSecurity.RedactPath(targetPath)}");
            }

            if (isLnk)
            {
                string? args = metadata?.Arguments;
                if (PathSecurity.ContainsUncPath(args))
                {
                    Trace.WriteLine($"Blocked execution of shortcut with unsafe arguments: {PathSecurity.RedactPath(path)}");
                    return;
                }

                string? workingDir = metadata?.WorkingDirectory;
                if (!string.IsNullOrEmpty(workingDir) && PathSecurity.IsUnsafePath(workingDir))
                {
                    Trace.WriteLine($"Blocked execution of shortcut with unsafe working directory: {PathSecurity.RedactPath(path)}");
                    return;
                }
            }
        }

        try
        {
            using var process = _processStarter.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"Failed to launch {PathSecurity.RedactPath(path)}: {PathSecurity.GetSafeExceptionMessage(ex)}");
        }
    }

    public void OpenFolder(string path)
    {
        if (PathSecurity.IsUnsafePath(path))
        {
            Trace.WriteLine($"Blocked opening of unsafe folder: {PathSecurity.RedactPath(path)}");
            return;
        }

        if (_fileSystem.DirectoryExists(path))
        {
            try
            {
                using var process = _processStarter.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"Failed to open folder {PathSecurity.RedactPath(path)}: {PathSecurity.GetSafeExceptionMessage(ex)}");
            }
        }
        else
        {
            Trace.WriteLine($"Folder not found: {PathSecurity.RedactPath(path)}");
        }
    }
}
