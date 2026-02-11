using Xunit;
using Launchbox;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System;

namespace Launchbox.Tests;

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
