using System;
using System.IO;
using System.Linq;

namespace Launchbox.Services;

public class ShortcutService : IShortcutService
{
    private readonly IFileSystem _fileSystem;

    public ShortcutService(IFileSystem fileSystem)
    {
        _fileSystem = fileSystem;
    }

    public string[]? GetShortcutFiles(string folderPath, System.Collections.Generic.IReadOnlyList<string> allowedExtensions)
    {
        if (allowedExtensions == null || !_fileSystem.DirectoryExists(folderPath))
        {
            return null;
        }

        try
        {
            return _fileSystem.EnumerateFiles(folderPath)
                .Where(f => allowedExtensions.Contains(Path.GetExtension(f) ?? string.Empty, StringComparer.OrdinalIgnoreCase))
                .OrderBy(f => Path.GetFileName(f))
                .ToArray();
        }
        catch (UnauthorizedAccessException ex)
        {
            System.Diagnostics.Trace.WriteLine($"Failed to access shortcut folder {Launchbox.Helpers.PathSecurity.RedactPath(folderPath)}: {Launchbox.Helpers.PathSecurity.GetSafeExceptionMessage(ex)}");
            return null;
        }
        catch (IOException ex)
        {
            System.Diagnostics.Trace.WriteLine($"Failed to access shortcut folder {Launchbox.Helpers.PathSecurity.RedactPath(folderPath)}: {Launchbox.Helpers.PathSecurity.GetSafeExceptionMessage(ex)}");
            return null;
        }
    }
}
