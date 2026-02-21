using Launchbox.Services;
using System.IO;
using System.Linq;
using Xunit;

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

        // MockFileSystem handles the \\?\ prefix correctly due to its fallback directory parsing logic.
        // Path.Combine does not automatically add this prefix, so we construct it manually.
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

    [Fact]
    public void ExtractIconBytes_BlocksUnsafePaths_PreventsFileSystemAccess()
    {
        // Arrange
        // We use a UNC path that would trigger NTLM auth if accessed
        string unsafePath = @"\\attacker\share\malicious.lnk";

        // We do NOT add the file to mockFileSystem.
        // If the code tries to access it (GetLastWriteTime), it might succeed (return default) or fail depending on mock.
        // But crucially, ExtractIconBytes returns null immediately due to IsUnsafePath check.

        // Act
        var result = _iconService.ExtractIconBytes(unsafePath);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void ResolveIconPath_BlocksUnsafePath_BeforeProcessing()
    {
        // Arrange
        // We use a UNC path that represents a potential NTLM leak vector
        string unsafePath = @"\\attacker\share\malicious.url";

        // Act
        // We call ResolveIconPath with the unsafe path directly.
        // Before the fix, this will call GetIniValue (and thus GetPrivateProfileString), potentially leaking credentials.
        // After the fix, it should return immediately.
        string result = _iconService.ResolveIconPath(unsafePath);

        // Assert
        // We verify that GetIniValue was NOT called for the unsafe path.
        // The mock logs "GetIniValue: {path}"
        Assert.Empty(_mockFileSystem.OperationsLog.Where(op => op.Contains(unsafePath)));

        // It should return the path itself (as it failed to resolve or was blocked)
        Assert.Equal(unsafePath, result);
    }
}
