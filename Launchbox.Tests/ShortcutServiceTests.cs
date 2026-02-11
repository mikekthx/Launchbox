using Xunit;
using Launchbox;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System;

namespace Launchbox.Tests;

public class MockFileSystem : IFileSystem
{
    private readonly HashSet<string> _directories = new();
    private readonly Dictionary<string, List<string>> _files = new();

    public void AddDirectory(string path)
    {
        _directories.Add(path);
    }

    public void AddFile(string directory, string filename)
    {
        if (!_files.ContainsKey(directory))
        {
            _files[directory] = new List<string>();
        }
        _files[directory].Add(Path.Combine(directory, filename));
    }

    public bool DirectoryExists(string path)
    {
        return _directories.Contains(path);
    }

    public string[] GetFiles(string path)
    {
        if (_files.TryGetValue(path, out var files))
        {
            return files.ToArray();
        }
        return Array.Empty<string>();
    }
}

public class ShortcutServiceTests
{
    private readonly string SHORTCUT_FOLDER = Path.Combine("Shortcuts");
    private readonly string[] ALLOWED_EXTENSIONS = { ".lnk", ".url" };

    [Fact]
    public void GetShortcutFiles_ReturnsNull_WhenDirectoryDoesNotExist()
    {
        var mockFileSystem = new MockFileSystem();
        var service = new ShortcutService(mockFileSystem);

        var result = service.GetShortcutFiles(SHORTCUT_FOLDER, ALLOWED_EXTENSIONS);

        Assert.Null(result);
    }

    [Fact]
    public void GetShortcutFiles_ReturnsFilteredFiles_WhenDirectoryExists()
    {
        var mockFileSystem = new MockFileSystem();
        mockFileSystem.AddDirectory(SHORTCUT_FOLDER);
        mockFileSystem.AddFile(SHORTCUT_FOLDER, "App1.lnk");
        mockFileSystem.AddFile(SHORTCUT_FOLDER, "App2.url");
        mockFileSystem.AddFile(SHORTCUT_FOLDER, "App3.txt"); // Should be filtered out

        var service = new ShortcutService(mockFileSystem);

        var result = service.GetShortcutFiles(SHORTCUT_FOLDER, ALLOWED_EXTENSIONS);

        Assert.NotNull(result);
        Assert.Equal(2, result.Length);
        Assert.Contains(result, f => f.EndsWith("App1.lnk"));
        Assert.Contains(result, f => f.EndsWith("App2.url"));
        Assert.DoesNotContain(result, f => f.EndsWith("App3.txt"));
    }
}
