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
    [InlineData(@"\??\UNC\attacker\share\icon.ico")]
    [InlineData(@"/\attacker/share/icon.ico")]
    [InlineData(@"\/attacker/share/icon.ico")]
    public void ResolveIconPath_IgnoresUnsafePaths(string unsafePath)
    {
        // Arrange
        string urlPath = Path.Combine("C:", "Shortcuts", "Malicious.url");

        // Mock the .url file
        _mockFileSystem.AddFile(urlPath);
        _mockFileSystem.SetIniValue(urlPath, "InternetShortcut", "IconFile", unsafePath);

        // We mock the unsafe file existing to simulate that the attacker's share is accessible.
        // If the security check is bypassed, ResolveIconPath will find this file and return unsafePath.
        // If the security check works, it should ignore this file and return urlPath.
        _mockFileSystem.AddFile(unsafePath);

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
