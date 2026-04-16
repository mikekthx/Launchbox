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
    private readonly ExceptionThrowingProcessStarter _processStarter;

    public WinUILauncherExceptionTests()
    {
        _fileSystem = new MockFileSystem();
        _shortcutResolver = new MockShortcutResolver();
        _processStarter = new ExceptionThrowingProcessStarter();
    }

    private class ExceptionThrowingProcessStarter : IProcessStarter
    {
        public Process? Start(ProcessStartInfo startInfo)
        {
            throw new InvalidOperationException("Simulated launch failure");
        }
    }

    private class ThrowingShortcutResolver : IShortcutResolver
    {
        public bool WasInvoked { get; private set; }

        public ShortcutMetadata? Resolve(string shortcutPath)
        {
            WasInvoked = true;
            throw new InvalidOperationException("Simulated COM failure");
        }
    }

    private class NeverCalledProcessStarter : IProcessStarter
    {
        public bool WasInvoked { get; private set; }

        public Process? Start(ProcessStartInfo startInfo)
        {
            WasInvoked = true;
            return null;
        }
    }

    [Fact]
    public void Constructor_ThrowsArgumentNullException_ForNullShortcutResolver()
    {
        var ex = Assert.Throws<ArgumentNullException>(() => new WinUILauncher(null!, _processStarter, _fileSystem));
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
        var ex = Assert.Throws<ArgumentNullException>(() => new WinUILauncher(_shortcutResolver, _processStarter, null!));
        Assert.Equal("fileSystem", ex.ParamName);
    }

    [Fact]
    public void Launch_CatchesAndLogsException()
    {
        var launcher = new WinUILauncher(_shortcutResolver, _processStarter, _fileSystem);

        _fileSystem.AddFile(@"C:\safe\shortcut.lnk");

        // Should not throw, the exception should be caught and logged
        var exception = Record.Exception(() => launcher.Launch(@"C:\safe\shortcut.lnk"));

        Assert.Null(exception);
    }

    [Fact]
    public void OpenFolder_CatchesAndLogsException()
    {
        var launcher = new WinUILauncher(_shortcutResolver, _processStarter, _fileSystem);

        _fileSystem.AddDirectory(@"C:\safe\folder");

        // Should not throw, the exception should be caught and logged
        var exception = Record.Exception(() => launcher.OpenFolder(@"C:\safe\folder"));

        Assert.Null(exception);
    }

    [Fact]
    public void Launch_ResolverThrows_DoesNotPropagateAndDoesNotStartProcess()
    {
        var throwingResolver = new ThrowingShortcutResolver();
        var trackingStarter = new NeverCalledProcessStarter();
        var launcher = new WinUILauncher(throwingResolver, trackingStarter, _fileSystem);

        _fileSystem.AddFile(@"C:\safe\shortcut.lnk");

        var exception = Record.Exception(() => launcher.Launch(@"C:\safe\shortcut.lnk"));

        Assert.Null(exception);
        Assert.True(throwingResolver.WasInvoked);
        Assert.False(trackingStarter.WasInvoked);
    }
}
