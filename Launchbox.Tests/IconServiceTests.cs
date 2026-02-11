using Xunit;
using Launchbox;
using System.IO;

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
}
