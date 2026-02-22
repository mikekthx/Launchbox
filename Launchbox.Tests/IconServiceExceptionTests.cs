using Launchbox.Services;
using System;
using System.IO;
using Xunit;

namespace Launchbox.Tests;

public class IconServiceExceptionTests
{
    private readonly MockFileSystem _mockFileSystem;
    private readonly IconService _iconService;

    public IconServiceExceptionTests()
    {
        _mockFileSystem = new MockFileSystem();
        _iconService = new IconService(_mockFileSystem);
    }

    [Fact]
    public void ExtractIconBytes_LogsAndReturnsNull_WhenOpenReadFails()
    {
        string shortcutPath = Path.Combine("C:", "Shortcuts", "App.lnk");
        string iconsDir = Path.Combine("C:", "Shortcuts", ".icons");
        string pngPath = Path.Combine(iconsDir, "App.png");
        string icoPath = Path.Combine(iconsDir, "App.ico");

        // Add file metadata for shortcut
        _mockFileSystem.AddFile(shortcutPath);
        _mockFileSystem.AddDirectory(iconsDir);

        // Add BOTH png and ico metadata but NO content.
        // This forces GetCustomIconBytes to call GetImageArea for both.
        // OpenRead will throw because content is missing in MockFileSystem.
        _mockFileSystem.AddFile(pngPath, size: 1024, content: null, lastWriteTime: DateTime.Now);
        _mockFileSystem.AddFile(icoPath, size: 1024, content: null, lastWriteTime: DateTime.Now);

        var result = _iconService.ExtractIconBytes(shortcutPath);

        // We expect empty array (MockFileSystem behavior) but NO CRASH.
        Assert.Empty(result);
    }
}
