using Launchbox.Helpers;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace Launchbox.Services;

public class WinUILauncher : IAppLauncher
{
    private static readonly string[] ALLOWED_EXTENSIONS = Constants.ALLOWED_EXTENSIONS;

    public void Launch(string path)
    {
        if (PathSecurity.IsUnsafePath(path))
        {
            Trace.WriteLine($"Blocked execution of unsafe file: {PathSecurity.RedactPath(path)}");
            return;
        }

        if (!File.Exists(path))
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

        try
        {
            using var process = Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
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

        if (Directory.Exists(path))
        {
            try
            {
                using var process = Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
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
