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
        string extension = Path.GetExtension(path).ToLowerInvariant();
        if (!ALLOWED_EXTENSIONS.Contains(extension))
        {
            Trace.WriteLine($"Blocked execution of unauthorized file: {path}");
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"Failed to launch {path}: {ex.Message}");
        }
    }

    public void OpenFolder(string path)
    {
        if (Directory.Exists(path))
        {
            try
            {
                Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"Failed to open folder {path}: {ex.Message}");
            }
        }
        else
        {
            Trace.WriteLine($"Folder not found: {path}");
        }
    }
}
