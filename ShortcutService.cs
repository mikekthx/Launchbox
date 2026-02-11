using System;
using System.IO;
using System.Linq;

namespace Launchbox;

public class ShortcutService
{
    private readonly IFileSystem _fileSystem;

    public ShortcutService(IFileSystem fileSystem)
    {
        _fileSystem = fileSystem;
    }

    public string[]? GetShortcutFiles(string folderPath, string[] allowedExtensions)
    {
        if (!_fileSystem.DirectoryExists(folderPath))
        {
            return null;
        }

        return _fileSystem.GetFiles(folderPath)
            .Where(f => allowedExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
            .OrderBy(f => Path.GetFileName(f))
            .ToArray();
    }
}
