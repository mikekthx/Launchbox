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

    public string[]? GetShortcutFiles(string folderPath, string[] allowedExtensions)
    {
        if (allowedExtensions == null || !_fileSystem.DirectoryExists(folderPath))
        {
            return null;
        }

        try
        {
            return _fileSystem.GetFiles(folderPath)
                .Where(f => allowedExtensions.Contains(Path.GetExtension(f) ?? string.Empty, StringComparer.OrdinalIgnoreCase))
                .OrderBy(f => Path.GetFileName(f))
                .ToArray();
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException || ex is IOException)
        {
            System.Diagnostics.Trace.WriteLine($"Failed to access shortcut folder {Launchbox.Helpers.PathSecurity.RedactPath(folderPath)}: {Launchbox.Helpers.PathSecurity.GetSafeExceptionMessage(ex)}");
            return null;
        }
    }
}
