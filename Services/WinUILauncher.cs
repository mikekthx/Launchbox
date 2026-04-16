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

        string extension = Path.GetExtension(path).ToLowerInvariant();
        if (!ALLOWED_EXTENSIONS.Contains(extension))
        {
            Trace.WriteLine($"Blocked execution of unauthorized file: {PathSecurity.RedactPath(path)}");
            return;
        }

        // Validate shortcut target (Defense-in-depth)
        if (extension == ".lnk" || extension == ".url")
        {
            ShortcutMetadata? metadata;
            try
            {
                metadata = _shortcutResolver.Resolve(path);
            }
            catch (Exception ex)
            {
                // Fail closed: a malformed .lnk can cause COM to throw during resolution.
                // Return early so one bad shortcut doesn't crash the launcher.
                // Fail closed: treat any resolver exception (e.g. COM failure on a malformed .lnk)
                // as unresolvable metadata so one bad shortcut doesn't crash the launcher.
                Trace.WriteLine($"Blocked execution: resolver threw for {PathSecurity.RedactPath(path)}: {PathSecurity.GetSafeExceptionMessage(ex)}");
                return;
            }

            // Security: fail closed if the resolver could not produce metadata at all
            // (COM failure, buffer truncation, non-Windows platform, or exception).
            // Proceeding without metadata would silently skip arg/workingDir validation.
            if (metadata == null)
            {
                Trace.WriteLine($"Blocked execution of shortcut with unresolvable metadata: {PathSecurity.RedactPath(path)}");
                return;
            }

            string? targetPath = metadata.Target;

            if (string.IsNullOrEmpty(targetPath))
            {
                if (extension == ".url")
                {
                    // .url MUST have a resolved http/https target; null means unsafe scheme was rejected.
                    Trace.WriteLine($"Blocked execution of .url with unresolvable or unsafe scheme: {PathSecurity.RedactPath(path)}");
                    return;
                }

                // .lnk with an empty target string: COM resolved successfully but returned no path.
                // The shell may still be able to handle it, so proceed with arg/workingDir checks.
                Trace.WriteLine($"Launching .lnk with empty target (COM returned no path): {PathSecurity.RedactPath(path)}");
            }
            else if (extension == ".lnk")
            {
                // Security: validate .lnk target as a filesystem path.
                // .url targets are http/https URLs already validated by the resolver — IsUnsafePath
                // is a filesystem check and would reject query-string characters.
                if (PathSecurity.IsUnsafePath(targetPath))
                {
                    Trace.WriteLine($"Blocked execution of shortcut pointing to unsafe target: {PathSecurity.RedactPath(targetPath)}");
                    return;
                }
                Trace.WriteLine($"Shortcut target validated: {PathSecurity.RedactPath(targetPath)}");
            }

            if (extension == ".lnk")
            {
                string? args = metadata.Arguments;
                if (PathSecurity.ContainsUncPath(args))
                {
                    Trace.WriteLine($"Blocked execution of shortcut with unsafe arguments: {PathSecurity.RedactPath(path)}");
                    return;
                }

                string? workingDir = metadata.WorkingDirectory;
                if (!string.IsNullOrEmpty(workingDir) && PathSecurity.IsUnsafePath(workingDir))
                {
                    Trace.WriteLine($"Blocked execution of shortcut with unsafe working directory: {PathSecurity.RedactPath(path)}");
                    return;
                }
            }
        }

        TryStartProcess(path, "Failed to launch");
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
            TryStartProcess(path, "Failed to open folder");
        }
        else
        {
            Trace.WriteLine($"Folder not found: {PathSecurity.RedactPath(path)}");
        }
    }

    private void TryStartProcess(string path, string failurePrefix)
    {
        try
        {
            using var process = _processStarter.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"{failurePrefix} {PathSecurity.RedactPath(path)}: {PathSecurity.GetSafeExceptionMessage(ex)}");
        }
    }
}
