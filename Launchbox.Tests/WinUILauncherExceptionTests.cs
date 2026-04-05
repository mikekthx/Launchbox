using Launchbox.Services;
using System;
using System.Diagnostics;
using System.IO;
using Xunit;

namespace Launchbox.Tests;

[Trait("Category", "Reliability")]
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
    public void Launch_CatchesAndLogsException()
    {
        var processStarter = new ExceptionThrowingProcessStarter();
        var launcher = new WinUILauncher(_shortcutResolver, processStarter, _fileSystem);

        _fileSystem.AddFile(@"C:\safe\shortcut.lnk");

        using var stringWriter = new StringWriter();
        using var listener = new TextWriterTraceListener(stringWriter);
        Trace.Listeners.Add(listener);

        try
        {
            var exception = Record.Exception(() => launcher.Launch(@"C:\safe\shortcut.lnk"));

            Assert.Null(exception);

            Trace.Flush();
            string traceOutput = stringWriter.ToString();
            Assert.Contains("Failed to launch ...\\shortcut.lnk: [InvalidOperationException]", traceOutput);
        }
        finally
        {
            Trace.Listeners.Remove(listener);
        }
    }

    [Fact]
    public void OpenFolder_CatchesAndLogsException()
    {
        var processStarter = new ExceptionThrowingProcessStarter();
        var launcher = new WinUILauncher(_shortcutResolver, processStarter, _fileSystem);

        _fileSystem.AddDirectory(@"C:\safe\folder");

        using var stringWriter = new StringWriter();
        using var listener = new TextWriterTraceListener(stringWriter);
        Trace.Listeners.Add(listener);

        try
        {
            var exception = Record.Exception(() => launcher.OpenFolder(@"C:\safe\folder"));

            Assert.Null(exception);

            Trace.Flush();
            string traceOutput = stringWriter.ToString();
            Assert.Contains("Failed to open folder ...\\folder: [InvalidOperationException]", traceOutput);
        }
        finally
        {
            Trace.Listeners.Remove(listener);
        }
    }
}
