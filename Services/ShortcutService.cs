using Launchbox.Helpers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;

namespace Launchbox.Services;

public class ShortcutService : IShortcutService
{
    private readonly IFileSystem _fileSystem;

    public ShortcutService(IFileSystem fileSystem)
    {
        _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
    }

    public string[]? GetShortcutFiles(string folderPath, IReadOnlyList<string> allowedExtensions, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        if (allowedExtensions == null || !_fileSystem.DirectoryExists(folderPath))
        {
            return null; // Folder does not exist (distinct from empty: no shortcuts in folder)
        }

        ct.ThrowIfCancellationRequested();

        try
        {
            var allowedExtensionsSet = new HashSet<string>(allowedExtensions, StringComparer.OrdinalIgnoreCase);
            return _fileSystem.EnumerateFiles(folderPath)
                .Where(f => allowedExtensionsSet.Contains(Path.GetExtension(f) ?? string.Empty))
                .OrderBy(f => Path.GetFileName(f))
                .ToArray();
        }
        catch (UnauthorizedAccessException ex)
        {
            Trace.WriteLine($"Failed to access shortcut folder {PathSecurity.RedactPath(folderPath)}: {PathSecurity.GetSafeExceptionMessage(ex)}");
            return null;
        }
        catch (IOException ex)
        {
            Trace.WriteLine($"Failed to access shortcut folder {PathSecurity.RedactPath(folderPath)}: {PathSecurity.GetSafeExceptionMessage(ex)}");
            return null;
        }
    }
}
