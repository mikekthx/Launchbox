using Launchbox.Helpers;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace Launchbox.Services;

public class WinUILauncher : IAppLauncher
{
    private static readonly string[] ALLOWED_EXTENSIONS = Constants.ALLOWED_EXTENSIONS;
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

            // If we can resolve it, check if the target is safe (Defense-in-depth)
            if (!string.IsNullOrEmpty(targetPath))
            {
                if (PathSecurity.IsUnsafePath(targetPath))
                {
                    Trace.WriteLine($"Blocked execution of shortcut pointing to unsafe target: {PathSecurity.RedactPath(targetPath)}");
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
            Trace.WriteLine($"Failed to launch {PathSecurity.RedactPath(path)}: {ex.Message}");
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
                Trace.WriteLine($"Failed to open folder {PathSecurity.RedactPath(path)}: {ex.Message}");
            }
        }
        else
        {
            Trace.WriteLine($"Folder not found: {PathSecurity.RedactPath(path)}");
        }
    }
}
