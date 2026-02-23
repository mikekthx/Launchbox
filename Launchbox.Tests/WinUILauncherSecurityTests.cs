using Launchbox.Services;
using Launchbox.Tests;
using Xunit;

namespace Launchbox.Tests;

public class WinUILauncherSecurityTests
{
    private readonly MockFileSystem _fileSystem;
    private readonly MockProcessStarter _processStarter;

    public WinUILauncherSecurityTests()
    {
        _fileSystem = new MockFileSystem();
        _processStarter = new MockProcessStarter();
    }

    [Fact]
    public void Launch_Blocks_UnsafePath()
    {
        var shortcutResolver = new MockShortcutResolver();
        var launcher = new WinUILauncher(shortcutResolver, _processStarter, _fileSystem);

        launcher.Launch(@"\\attacker\share\malware.exe");

        Assert.False(_processStarter.WasStarted);
    }

    [Fact]
    public void Launch_Blocks_NonExistentFile()
    {
        var shortcutResolver = new MockShortcutResolver();
        var launcher = new WinUILauncher(shortcutResolver, _processStarter, _fileSystem);

        launcher.Launch(@"C:\safe\file.exe"); // Does not exist in mock FS

        Assert.False(_processStarter.WasStarted);
    }

    [Fact]
    public void Launch_Blocks_UnauthorizedExtension()
    {
        var shortcutResolver = new MockShortcutResolver();
        var launcher = new WinUILauncher(shortcutResolver, _processStarter, _fileSystem);

        _fileSystem.AddFile(@"C:\safe\script.bat"); // .bat is not allowed (only .lnk and .url)

        launcher.Launch(@"C:\safe\script.bat");

        Assert.False(_processStarter.WasStarted);
    }

    [Fact]
    public void Launch_Allows_AllowedExtension()
    {
        var shortcutResolver = new MockShortcutResolver();
        var launcher = new WinUILauncher(shortcutResolver, _processStarter, _fileSystem);

        _fileSystem.AddFile(@"C:\safe\shortcut.lnk");

        launcher.Launch(@"C:\safe\shortcut.lnk");

        Assert.True(_processStarter.WasStarted);
        Assert.Equal(@"C:\safe\shortcut.lnk", _processStarter.LastStartInfo?.FileName);
    }

    [Fact]
    public void Launch_Blocks_Shortcut_To_UnsafePath()
    {
        // Setup: shortcut points to UNC path
        var shortcutResolver = new MockShortcutResolver(@"\\attacker\share\malware.exe");
        var launcher = new WinUILauncher(shortcutResolver, _processStarter, _fileSystem);

        _fileSystem.AddFile(@"C:\safe\shortcut.lnk");

        launcher.Launch(@"C:\safe\shortcut.lnk");

        Assert.False(_processStarter.WasStarted);
    }

    [Fact]
    public void Launch_Allows_Shortcut_To_SafePath()
    {
        // Setup: shortcut points to safe path
        var shortcutResolver = new MockShortcutResolver(@"C:\Program Files\App.exe");
        var launcher = new WinUILauncher(shortcutResolver, _processStarter, _fileSystem);

        _fileSystem.AddFile(@"C:\safe\shortcut.lnk");

        launcher.Launch(@"C:\safe\shortcut.lnk");

        Assert.True(_processStarter.WasStarted);
    }
}
