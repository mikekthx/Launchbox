using Launchbox.Services;
using Launchbox.Tests;
using System;
using Xunit;

namespace Launchbox.Tests;

[Trait("Category", "Security")]
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

    [Fact]
    public void Launch_Blocks_UrlFile_When_ResolveTarget_Returns_Null()
    {
        // Setup: resolver returns null (unsafe scheme like file://, ms-settings://, etc.)
        var shortcutResolver = new MockShortcutResolver(target: null);
        var launcher = new WinUILauncher(shortcutResolver, _processStarter, _fileSystem);

        _fileSystem.AddFile(@"C:\safe\malicious.url");

        launcher.Launch(@"C:\safe\malicious.url");

        Assert.False(_processStarter.WasStarted);
    }

    [Fact]
    public void Launch_Allows_UrlFile_When_ResolveTarget_Returns_SafeUrl()
    {
        var shortcutResolver = new MockShortcutResolver("https://example.com");
        var launcher = new WinUILauncher(shortcutResolver, _processStarter, _fileSystem);

        _fileSystem.AddFile(@"C:\safe\website.url");

        launcher.Launch(@"C:\safe\website.url");

        Assert.True(_processStarter.WasStarted);
    }

    [Fact]
    public void Launch_Allows_LnkFile_When_ResolveTarget_Returns_Null()
    {
        // .lnk files with null resolution are allowed — COM may fail for valid shortcuts
        // that the shell can still handle
        var shortcutResolver = new MockShortcutResolver(target: null);
        var launcher = new WinUILauncher(shortcutResolver, _processStarter, _fileSystem);

        _fileSystem.AddFile(@"C:\safe\shortcut.lnk");

        launcher.Launch(@"C:\safe\shortcut.lnk");

        Assert.True(_processStarter.WasStarted);
    }

    [Theory]
    [InlineData(@"\\server\share\file")]
    [InlineData("//server/share/file")]
    [InlineData(@"\/server/share/file")]
    [InlineData(@"/\server/share/file")]
    public void Launch_Blocks_Lnk_With_UnsafeArguments(string unsafeArgs)
    {
        var shortcutResolver = new MockShortcutResolver(
            target: @"C:\Program Files\App.exe",
            arguments: unsafeArgs);
        var launcher = new WinUILauncher(shortcutResolver, _processStarter, _fileSystem);

        _fileSystem.AddFile(@"C:\safe\shortcut.lnk");

        launcher.Launch(@"C:\safe\shortcut.lnk");

        Assert.False(_processStarter.WasStarted);
    }

    [Fact]
    public void Launch_Blocks_Lnk_With_UnsafeWorkingDirectory()
    {
        var shortcutResolver = new MockShortcutResolver(
            target: @"C:\Program Files\App.exe",
            workingDirectory: @"\\attacker\share");
        var launcher = new WinUILauncher(shortcutResolver, _processStarter, _fileSystem);

        _fileSystem.AddFile(@"C:\safe\shortcut.lnk");

        launcher.Launch(@"C:\safe\shortcut.lnk");

        Assert.False(_processStarter.WasStarted);
    }

    [Fact]
    public void Launch_Allows_Lnk_With_SafeArgumentsAndWorkingDirectory()
    {
        var shortcutResolver = new MockShortcutResolver(
            target: @"C:\Program Files\App.exe",
            arguments: @"--verbose --output C:\temp",
            workingDirectory: @"C:\Program Files");
        var launcher = new WinUILauncher(shortcutResolver, _processStarter, _fileSystem);

        _fileSystem.AddFile(@"C:\safe\shortcut.lnk");

        launcher.Launch(@"C:\safe\shortcut.lnk");

        Assert.True(_processStarter.WasStarted);
    }

    [Theory]
    [InlineData("https://example.com")]
    [InlineData("http://example.com")]
    [InlineData("--url=https://example.com/path")]
    public void Launch_Allows_Lnk_With_UrlArguments(string urlArgs)
    {
        var shortcutResolver = new MockShortcutResolver(
            target: @"C:\Program Files\App.exe",
            arguments: urlArgs);
        var launcher = new WinUILauncher(shortcutResolver, _processStarter, _fileSystem);

        _fileSystem.AddFile(@"C:\safe\shortcut.lnk");

        launcher.Launch(@"C:\safe\shortcut.lnk");

        Assert.True(_processStarter.WasStarted);
    }

    [Fact]
    public void Launch_Blocks_Lnk_With_NullTarget_But_UnsafeArguments()
    {
        var shortcutResolver = new MockShortcutResolver(
            target: null,
            arguments: @"\\attacker\share\payload");
        var launcher = new WinUILauncher(shortcutResolver, _processStarter, _fileSystem);

        _fileSystem.AddFile(@"C:\safe\shortcut.lnk");

        launcher.Launch(@"C:\safe\shortcut.lnk");

        Assert.False(_processStarter.WasStarted);
    }

    [Fact]
    public void OpenFolder_Catches_ProcessStart_Exception()
    {
        var shortcutResolver = new MockShortcutResolver();
        var launcher = new WinUILauncher(shortcutResolver, _processStarter, _fileSystem);

        string folderPath = @"C:\safe\folder";
        _fileSystem.AddDirectory(folderPath);

        _processStarter.ExceptionToThrow = new Exception("Test exception for OpenFolder");

        // Should not throw
        launcher.OpenFolder(folderPath);
    }
}
