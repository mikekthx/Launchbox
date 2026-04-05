using Launchbox.Services;
using System;
using System.Diagnostics;
using Xunit;

namespace Launchbox.Tests;

[Trait("Category", "Reliability")]
[Collection("TraceListeners")]
public class WinUILauncherExceptionTests
{
    private readonly MockFileSystem _fileSystem;
    private readonly MockShortcutResolver _shortcutResolver;

    public WinUILauncherExceptionTests()
    {
        _fileSystem = new MockFileSystem();
        _shortcutResolver = new MockShortcutResolver();
    }

    private class ExceptionThrowingProcessStarter : IProcessStarter
    {
        public Process? Start(ProcessStartInfo startInfo)
        {
            throw new InvalidOperationException("Simulated launch failure");
        }
    }

    [Fact]
    public void Constructor_ThrowsArgumentNullException_ForNullShortcutResolver()
    {
        var processStarter = new ExceptionThrowingProcessStarter();
        var ex = Assert.Throws<ArgumentNullException>(() => new WinUILauncher(null!, processStarter, _fileSystem));
        Assert.Equal("shortcutResolver", ex.ParamName);
    }

    [Fact]
    public void Constructor_ThrowsArgumentNullException_ForNullProcessStarter()
    {
        var ex = Assert.Throws<ArgumentNullException>(() => new WinUILauncher(_shortcutResolver, null!, _fileSystem));
        Assert.Equal("processStarter", ex.ParamName);
    }

    [Fact]
    public void Constructor_ThrowsArgumentNullException_ForNullFileSystem()
    {
        var processStarter = new ExceptionThrowingProcessStarter();
        var ex = Assert.Throws<ArgumentNullException>(() => new WinUILauncher(_shortcutResolver, processStarter, null!));
        Assert.Equal("fileSystem", ex.ParamName);
    }

    [Fact]
    public void Launch_CatchesAndLogsException()
    {
        var processStarter = new ExceptionThrowingProcessStarter();
        var launcher = new WinUILauncher(_shortcutResolver, processStarter, _fileSystem);

        _fileSystem.AddFile(@"C:\safe\shortcut.lnk");

        // Should not throw, the exception should be caught and logged
        var exception = Record.Exception(() => launcher.Launch(@"C:\safe\shortcut.lnk"));

        Assert.Null(exception);
    }

    [Fact]
    public void OpenFolder_CatchesAndLogsException()
    {
        var processStarter = new ExceptionThrowingProcessStarter();
        var launcher = new WinUILauncher(_shortcutResolver, processStarter, _fileSystem);

        _fileSystem.AddDirectory(@"C:\safe\folder");

        // Should not throw, the exception should be caught and logged
        var exception = Record.Exception(() => launcher.OpenFolder(@"C:\safe\folder"));

        Assert.Null(exception);
    }
}
