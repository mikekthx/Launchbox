using Launchbox.Helpers;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace Launchbox.Services;

public class WinUILauncher : IAppLauncher
{
    private static readonly System.Collections.Generic.IReadOnlyList<string> ALLOWED_EXTENSIONS = Constants.ALLOWED_EXTENSIONS;
    private readonly IShortcutResolver _shortcutResolver;
    private readonly IProcessStarter _processStarter;
    private readonly IFileSystem _fileSystem;

    public WinUILauncher(IShortcutResolver shortcutResolver, IProcessStarter processStarter, IFileSystem fileSystem)
    {
        _shortcutResolver = shortcutResolver;
        _processStarter = processStarter;
        _fileSystem = fileSystem;
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

        string extension = Path.GetExtension(path).ToLowerInvariant();
        if (!ALLOWED_EXTENSIONS.Contains(extension))
        {
            Trace.WriteLine($"Blocked execution of unauthorized file: {PathSecurity.RedactPath(path)}");
            return;
        }

        // Validate shortcut target (Defense-in-depth)
        if (extension == ".lnk" || extension == ".url")
        {
            string? targetPath = _shortcutResolver.ResolveTarget(path);

            // .url files MUST resolve to a safe scheme (http/https). A null result means
            // the resolver rejected the scheme, so block execution to prevent shell-executing
            // arbitrary URI schemes (file://, ms-settings://, custom handlers, etc.).
            // .lnk files may return null from COM resolution for valid shortcuts the shell
            // can still handle, so only .url null-resolution is treated as a security block.
            if (string.IsNullOrEmpty(targetPath))
            {
                if (extension == ".url")
                {
                    Trace.WriteLine($"Blocked execution of .url with unresolvable or unsafe scheme: {PathSecurity.RedactPath(path)}");
                    return;
                }
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

            if (extension == ".lnk")
            {
                string? args = _shortcutResolver.ResolveArguments(path);
                if (!string.IsNullOrEmpty(args) && (args.Contains(@"\\") || args.Contains("//") || args.Contains(@"\/") || args.Contains(@"/\")))
                {
                    Trace.WriteLine($"Blocked execution of shortcut with unsafe arguments: {PathSecurity.RedactPath(path)}");
                    return;
                }

                string? workingDir = _shortcutResolver.ResolveWorkingDirectory(path);
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
