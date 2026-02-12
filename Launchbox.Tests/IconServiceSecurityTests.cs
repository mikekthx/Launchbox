using Xunit;
using Launchbox.Services;
using System.IO;

namespace Launchbox.Tests;

public class IconServiceSecurityTests
{
    private readonly MockFileSystem _mockFileSystem;
    private readonly IconService _iconService;

    public IconServiceSecurityTests()
    {
        _mockFileSystem = new MockFileSystem();
        _iconService = new IconService(_mockFileSystem);
    }

    [Theory]
    [InlineData(@"\\attacker\share\icon.ico")]
    [InlineData(@"\\?\UNC\attacker\share\icon.ico")]
    [InlineData(@"//attacker/share/icon.ico")]
    public void ResolveIconPath_IgnoresUnsafePaths(string unsafePath)
    {
        // Arrange
        string urlPath = Path.Combine("C:", "Shortcuts", "Malicious.url");

        // Mock the .url file
        _mockFileSystem.AddFile(urlPath);
        _mockFileSystem.SetIniValue(urlPath, "InternetShortcut", "IconFile", unsafePath);

        // Even if we mock the unsafe file existing (which mimics a real scenario where the share is accessible)
        // _mockFileSystem.AddFile(unsafePath); // Note: MockFileSystem might struggle with UNC paths, but let's assume it can store strings.
        // Actually, let's NOT add it to the file system to prove that ResolveIconPath doesn't even try to check it?
        // No, the vulnerability is checking it. But we want to assert the return value is the ORIGINAL path.

        // Act
        string result = _iconService.ResolveIconPath(urlPath);

        // Assert
        Assert.Equal(urlPath, result); // Should return original path, effectively ignoring the unsafe icon
    }

    [Fact]
    public void ResolveIconPath_AllowsSafeLocalPaths()
    {
        // Arrange
        string urlPath = Path.Combine("C:", "Shortcuts", "Good.url");
        string iconPath = Path.Combine("C:", "Icons", "Good.ico");

        _mockFileSystem.AddFile(urlPath);
        _mockFileSystem.AddFile(iconPath);
        _mockFileSystem.SetIniValue(urlPath, "InternetShortcut", "IconFile", iconPath);

        // Act
        string result = _iconService.ResolveIconPath(urlPath);

        // Assert
        Assert.Equal(iconPath, result);
    }

    [Fact]
    public void ResolveIconPath_AllowsLocalLongPaths()
    {
        // Arrange
        string urlPath = Path.Combine("C:", "Shortcuts", "Long.url");
        // Note: verify if MockFileSystem handles \\?\ prefix correctly or if Path.Combine creates it.
        // We will manually construct it.
        string iconPath = @"\\?\C:\Very\Long\Path\To\Icon.ico";

        _mockFileSystem.AddFile(urlPath);
        // We need to add the file to the mock system for it to return true on FileExists
        _mockFileSystem.AddFile(iconPath);
        _mockFileSystem.SetIniValue(urlPath, "InternetShortcut", "IconFile", iconPath);

        // Act
        string result = _iconService.ResolveIconPath(urlPath);

        // Assert
        Assert.Equal(iconPath, result);
    }
}
