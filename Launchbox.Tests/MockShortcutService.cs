using Launchbox.Services;
using System.Collections.Generic;
using System.Threading;

namespace Launchbox.Tests;

public class MockShortcutService : IShortcutService
{
    private string[]? _files;

    public void SetFiles(string[]? files)
    {
        _files = files;
    }

    public string[]? GetShortcutFiles(string folderPath, IReadOnlyList<string> allowedExtensions, CancellationToken ct = default)
    {
        return _files;
    }
}
