using System.IO;
using Xunit;
using Launchbox.Services;

namespace Launchbox.Tests;

public class IconServiceTests
{
    private readonly MockFileSystem _mockFileSystem;
    private readonly IconService _iconService;

    public IconServiceTests()
    {
        _mockFileSystem = new MockFileSystem();
        _iconService = new IconService(_mockFileSystem);
    }

    [Fact]
    public void ResolveIconPath_ReturnsOriginalPath_WhenNotUrlFile()
    {
        string path = Path.Combine("C:", "Shortcuts", "App.lnk");
        string result = _iconService.ResolveIconPath(path);
        Assert.Equal(path, result);
    }

    [Fact]
    public void ResolveIconPath_ReturnsOriginalPath_WhenUrlFileHasNoIconFile()
    {
        string path = Path.Combine("C:", "Shortcuts", "App.url");
        _mockFileSystem.AddFile(path);
        // No INI value set

        string result = _iconService.ResolveIconPath(path);
        Assert.Equal(path, result);
    }

    [Fact]
    public void ResolveIconPath_ReturnsIconPath_WhenUrlFileHasValidIconFile()
    {
        string path = Path.Combine("C:", "Shortcuts", "App.url");
        string iconPath = Path.Combine("C:", "Icons", "App.ico");

        _mockFileSystem.AddFile(path);
        _mockFileSystem.AddFile(iconPath);
        _mockFileSystem.SetIniValue(path, "InternetShortcut", "IconFile", iconPath);

        string result = _iconService.ResolveIconPath(path);
        Assert.Equal(iconPath, result);
    }

    [Fact]
    public void ResolveIconPath_ReturnsOriginalPath_WhenIconFileDoesNotExist()
    {
        string path = Path.Combine("C:", "Shortcuts", "App.url");
        string iconPath = Path.Combine("C:", "Icons", "App.ico");

        _mockFileSystem.AddFile(path);
        // iconPath is NOT added to file system
        _mockFileSystem.SetIniValue(path, "InternetShortcut", "IconFile", iconPath);

        string result = _iconService.ResolveIconPath(path);
        Assert.Equal(path, result);
    }

    [Fact]
    public void ExtractIconBytes_ReturnsCustomPng_WhenPngExists()
    {
        string shortcutPath = Path.Combine("C:", "Shortcuts", "App.lnk");
        string iconsDir = Path.Combine("C:", "Shortcuts", ".icons");
        string pngPath = Path.Combine(iconsDir, "App.png");
        byte[] expectedBytes = { 1, 2, 3 };

        _mockFileSystem.AddFile(shortcutPath);
        _mockFileSystem.AddDirectory(iconsDir);
        _mockFileSystem.AddFile(pngPath, content: expectedBytes);

        var result = _iconService.ExtractIconBytes(shortcutPath);

        Assert.Equal(expectedBytes, result);
    }

    [Fact]
    public void ExtractIconBytes_ReturnsCustomIco_WhenIcoExists()
    {
        string shortcutPath = Path.Combine("C:", "Shortcuts", "App.lnk");
        string iconsDir = Path.Combine("C:", "Shortcuts", ".icons");
        string icoPath = Path.Combine(iconsDir, "App.ico");
        byte[] expectedBytes = { 4, 5, 6 };

        _mockFileSystem.AddFile(shortcutPath);
        _mockFileSystem.AddDirectory(iconsDir);
        _mockFileSystem.AddFile(icoPath, content: expectedBytes);

        var result = _iconService.ExtractIconBytes(shortcutPath);

        Assert.Equal(expectedBytes, result);
    }

    [Fact]
    public void ExtractIconBytes_ReturnsHigherResolution_WhenBothExist()
    {
        string shortcutPath = Path.Combine("C:", "Shortcuts", "App.lnk");
        string iconsDir = Path.Combine("C:", "Shortcuts", ".icons");
        string pngPath = Path.Combine(iconsDir, "App.png");
        string icoPath = Path.Combine(iconsDir, "App.ico");

        // Small PNG (1x1)
        byte[] pngBytes = CreatePng(1, 1);

        // Large ICO (2x2)
        byte[] icoBytes = CreateIco(2, 2);

        _mockFileSystem.AddFile(shortcutPath);
        _mockFileSystem.AddDirectory(iconsDir);
        _mockFileSystem.AddFile(pngPath, content: pngBytes);
        _mockFileSystem.AddFile(icoPath, content: icoBytes);

        var result = _iconService.ExtractIconBytes(shortcutPath);

        Assert.Equal(icoBytes, result);
    }

    [Fact]
    public void ExtractIconBytes_ReturnsPng_WhenResolutionsAreEqual()
    {
        string shortcutPath = Path.Combine("C:", "Shortcuts", "App.lnk");
        string iconsDir = Path.Combine("C:", "Shortcuts", ".icons");
        string pngPath = Path.Combine(iconsDir, "App.png");
        string icoPath = Path.Combine(iconsDir, "App.ico");

        // Equal resolution (2x2)
        byte[] pngBytes = CreatePng(2, 2);
        byte[] icoBytes = CreateIco(2, 2);

        _mockFileSystem.AddFile(shortcutPath);
        _mockFileSystem.AddDirectory(iconsDir);
        _mockFileSystem.AddFile(pngPath, content: pngBytes);
        _mockFileSystem.AddFile(icoPath, content: icoBytes);

        var result = _iconService.ExtractIconBytes(shortcutPath);

        Assert.Equal(pngBytes, result);
    }

    private byte[] CreatePng(int width, int height)
    {
        var header = new byte[]
        {
            0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, // Sig
            0x00, 0x00, 0x00, 0x0D, // IHDR Len
            0x49, 0x48, 0x44, 0x52, // IHDR Type
            0, 0, 0, (byte)width,   // Width (Big Endian - simplified for test)
            0, 0, 0, (byte)height   // Height
        };
        // Pad to > 24 bytes
        var result = new byte[30];
        Array.Copy(header, result, header.Length);
        return result;
    }

    private byte[] CreateIco(int width, int height)
    {
        var header = new byte[]
        {
            0, 0, // Reserved
            1, 0, // Type 1 (Icon)
            1, 0, // Count 1
            (byte)width, (byte)height, // Entry 1: W, H
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 // Rest of entry (14 bytes needed)
        };
        return header;
    }

    [Fact]
    public void ExtractIconBytes_RefreshesCache_WhenFileUpdated()
    {
        string shortcutPath = Path.Combine("C:", "Shortcuts", "App.lnk");
        string iconsDir = Path.Combine("C:", "Shortcuts", ".icons");
        string pngPath = Path.Combine(iconsDir, "App.png");
        byte[] pngBytes = { 1, 2, 3 };

        _mockFileSystem.AddFile(shortcutPath);
        _mockFileSystem.AddDirectory(iconsDir);
        _mockFileSystem.AddFile(pngPath, content: pngBytes, lastWriteTime: DateTime.Now.AddHours(-1));

        // First call: Load initial
        var result1 = _iconService.ExtractIconBytes(shortcutPath);
        Assert.Equal(pngBytes, result1);

        // Update file with newer timestamp
        byte[] newBytes = { 9, 9, 9 };
        _mockFileSystem.AddFile(pngPath, content: newBytes, lastWriteTime: DateTime.Now);

        // Second call: Should detect timestamp change and reload
        var result2 = _iconService.ExtractIconBytes(shortcutPath);
        Assert.Equal(newBytes, result2);
    }

    [Fact]
    public void ExtractIconBytes_UsesCache_WhenTimestampsUnchanged()
    {
        // This test is tricky to prove "cache usage" without spying,
        // but we can verify correctness: it returns the file content.
        // The optimization is internal (skipping header parsing).

        string shortcutPath = Path.Combine("C:", "Shortcuts", "App.lnk");
        string iconsDir = Path.Combine("C:", "Shortcuts", ".icons");
        string pngPath = Path.Combine(iconsDir, "App.png");
        byte[] pngBytes = { 1, 2, 3 };
        var time = DateTime.Now;

        _mockFileSystem.AddFile(shortcutPath);
        _mockFileSystem.AddDirectory(iconsDir);
        _mockFileSystem.AddFile(pngPath, content: pngBytes, lastWriteTime: time);

        // First call
        var result1 = _iconService.ExtractIconBytes(shortcutPath);
        Assert.Equal(pngBytes, result1);

        // Second call (timestamps same)
        var result2 = _iconService.ExtractIconBytes(shortcutPath);
        Assert.Equal(pngBytes, result2);
    }

    [Fact]
    public void ExtractIconBytes_ReturnsNull_WhenCustomIconIsTooLarge()
    {
        string shortcutPath = Path.Combine("C:", "Shortcuts", "App.lnk");
        string iconsDir = Path.Combine("C:", "Shortcuts", ".icons");
        string pngPath = Path.Combine(iconsDir, "App.png");

        // Size > 5MB
        long largeSize = 6 * 1024 * 1024;

        _mockFileSystem.AddFile(shortcutPath);
        _mockFileSystem.AddDirectory(iconsDir);
        // We set size explicitly. Content is null but size check should trigger before read.
        _mockFileSystem.AddFile(pngPath, size: largeSize, content: null);

        var result = _iconService.ExtractIconBytes(shortcutPath);

        Assert.Null(result);
    }

    [Fact]
    public void PruneCache_RemovesUnusedEntries_ReturnsCount()
    {
        string shortcutPath1 = Path.Combine("C:", "Shortcuts", "App1.lnk");
        string shortcutPath2 = Path.Combine("C:", "Shortcuts", "App2.lnk");
        string iconsDir = Path.Combine("C:", "Shortcuts", ".icons");
        string pngPath1 = Path.Combine(iconsDir, "App1.png");
        string pngPath2 = Path.Combine(iconsDir, "App2.png");

        _mockFileSystem.AddFile(shortcutPath1);
        _mockFileSystem.AddFile(shortcutPath2);
        _mockFileSystem.AddDirectory(iconsDir);
        // Mock custom icons to avoid NativeMethods P/Invoke on Linux
        _mockFileSystem.AddFile(pngPath1, content: new byte[] { 1 });
        _mockFileSystem.AddFile(pngPath2, content: new byte[] { 2 });

        // Populate cache
        _iconService.ExtractIconBytes(shortcutPath1);
        _iconService.ExtractIconBytes(shortcutPath2);

        // Prune shortcutPath2 (keep shortcutPath1)
        var activePaths = new[] { shortcutPath1 };
        int removedCount = _iconService.PruneCache(activePaths);

        Assert.Equal(1, removedCount);

        // Verify removing everything
        removedCount = _iconService.PruneCache(System.Array.Empty<string>());
        Assert.Equal(1, removedCount);
    }
}
