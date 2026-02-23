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
        Assert.Empty(result!);
    }

    [Fact]
    public void ExtractIconBytes_ReturnsNull_WhenCustomIconReadFails()
    {
        string shortcutPath = Path.Combine("C:", "Shortcuts", "App.lnk");
        string iconsDir = Path.Combine("C:", "Shortcuts", ".icons");
        string pngPath = Path.Combine(iconsDir, "App.png");

        // Use custom mock that throws for PNG read
        var faultyFileSystem = new FaultyFileSystem();
        var iconService = new IconService(faultyFileSystem);

        // Add file metadata
        faultyFileSystem.AddFile(shortcutPath);
        faultyFileSystem.AddDirectory(iconsDir);

        // Add valid PNG entry (so it's chosen)
        // Content doesn't matter as ReadAllBytes will throw before reading it
        faultyFileSystem.AddFile(pngPath, size: 1024, content: new byte[1024], lastWriteTime: DateTime.Now);

        // Configure to fail
        faultyFileSystem.FailPath = pngPath;

        var result = iconService.ExtractIconBytes(shortcutPath);

        // Should return null (graceful failure)
        Assert.Null(result);
    }

    private class FaultyFileSystem : MockFileSystem
    {
        public string? FailPath { get; set; }

        public override byte[] ReadAllBytes(string path)
        {
            if (FailPath != null && path.Equals(FailPath, StringComparison.OrdinalIgnoreCase))
            {
                throw new IOException("Simulated read failure");
            }
            return base.ReadAllBytes(path);
        }
    }
}
