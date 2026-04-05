using Launchbox.Helpers;
using Launchbox.Services;
using System;
using System.Diagnostics;
using System.IO;
using Xunit;

namespace Launchbox.Tests;

[Trait("Category", "Logging")]
[Collection("TraceListeners")]
public class WinUILauncherLoggingTests : IDisposable
{
    private readonly MockFileSystem _fileSystem;
    private readonly MockShortcutResolver _shortcutResolver;
    private readonly MockProcessStarter _processStarter;
    private readonly ThreadSafeStringWriter _stringWriter;
    private readonly TextWriterTraceListener _traceListener;

    public WinUILauncherLoggingTests()
    {
        _fileSystem = new MockFileSystem();
        _shortcutResolver = new MockShortcutResolver();
        _processStarter = new MockProcessStarter();

        _stringWriter = new ThreadSafeStringWriter();
        _traceListener = new TextWriterTraceListener(_stringWriter);
        Trace.Listeners.Add(_traceListener);
    }

    public void Dispose()
    {
        Trace.Listeners.Remove(_traceListener);
        _traceListener.Dispose();
    }

    [Fact]
    public void Launch_Logs_BlockedUnsafePath()
    {
        var launcher = new WinUILauncher(_shortcutResolver, _processStarter, _fileSystem);
        string unsafePath = @"\\attacker\share\malware.exe";

        launcher.Launch(unsafePath);
        _traceListener.Flush();

        string output = _stringWriter.ToString();
        Assert.Contains($"Blocked execution of unsafe file: {PathSecurity.RedactPath(unsafePath)}", output);
    }

    [Fact]
    public void Launch_Logs_BlockedNonExistentFile()
    {
        var launcher = new WinUILauncher(_shortcutResolver, _processStarter, _fileSystem);
        string missingPath = @"C:\safe\missing.exe";
        string missingPath = @"C:\safe\missing.lnk";
        launcher.Launch(missingPath);
        _traceListener.Flush();

        string output = _stringWriter.ToString();
        Assert.Contains($"Blocked execution of non-existent file: {PathSecurity.RedactPath(missingPath)}", output);
    }

    [Fact]
    public void Launch_Logs_BlockedUnauthorizedExtension()
    {
        var launcher = new WinUILauncher(_shortcutResolver, _processStarter, _fileSystem);
        string unauthorizedPath = @"C:\safe\script.bat";
        _fileSystem.AddFile(unauthorizedPath);

        launcher.Launch(unauthorizedPath);
        _traceListener.Flush();

        string output = _stringWriter.ToString();
        Assert.Contains($"Blocked execution of unauthorized file: {PathSecurity.RedactPath(unauthorizedPath)}", output);
    }

    [Fact]
    public void OpenFolder_Logs_BlockedUnsafeFolder()
    {
        var launcher = new WinUILauncher(_shortcutResolver, _processStarter, _fileSystem);
        string unsafeFolder = @"\\attacker\share\folder";

        launcher.OpenFolder(unsafeFolder);
        _traceListener.Flush();

        string output = _stringWriter.ToString();
        Assert.Contains($"Blocked opening of unsafe folder: {PathSecurity.RedactPath(unsafeFolder)}", output);
    }

    [Fact]
    public void OpenFolder_Logs_FolderNotFound()
    {
        var launcher = new WinUILauncher(_shortcutResolver, _processStarter, _fileSystem);
        string missingFolder = @"C:\safe\missing_folder";

        launcher.OpenFolder(missingFolder);
        _traceListener.Flush();

        string output = _stringWriter.ToString();
        Assert.Contains($"Folder not found: {PathSecurity.RedactPath(missingFolder)}", output);
    }
}
