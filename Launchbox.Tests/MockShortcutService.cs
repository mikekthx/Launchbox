using Launchbox.Services;
using System.Collections.Generic;

namespace Launchbox.Tests;

public class MockShortcutService : IShortcutService
{
    private string[]? _files;

    public void SetFiles(string[]? files)
    {
        _files = files;
    }

    public string[]? GetShortcutFiles(string folderPath, string[] allowedExtensions)
    {
        return _files;
    }
}
