using Launchbox.Helpers;
using Launchbox.Services;
using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using Xunit;

namespace Launchbox.Tests;

public class TraceRedactionTests : IDisposable
{
    private readonly StringWriter _traceOutput;
    private readonly TextWriterTraceListener _traceListener;

    public TraceRedactionTests()
    {
        _traceOutput = new StringWriter();
        _traceListener = new TextWriterTraceListener(_traceOutput);
        Trace.Listeners.Add(_traceListener);
    }

    public void Dispose()
    {
        Trace.Listeners.Remove(_traceListener);
        _traceOutput.Dispose();
        _traceListener.Dispose();
    }

    [Fact]
    public void IconService_ResolveIconPath_RedactsPath_OnException()
    {
        // Arrange
        var fileSystem = new FaultyFileSystem();
        var iconService = new IconService(fileSystem);
        string secretPath = @"C:\Users\Admin\Documents\SecretProject\Sensitive.url";

        // Act
        // ResolveIconPath calls GetIniValue for .url files.
        // FaultyFileSystem throws on GetIniValue.
        // IconService catches NO exception in ResolveIconPath?
        // Wait, let's check IconService code again.

        // ResolveIconPath does NOT catch exceptions!
        // It calls _fileSystem.GetIniValue directly.
        // So this test might crash if not careful.

        // But ResolveIconPath calls PathSecurity.IsUnsafePath first.
        // Let's assume we pass a safe path but file system fails.

        // Actually, looking at IconService.cs:
        // internal string ResolveIconPath(string path)
        // {
        //    if (PathSecurity.IsUnsafePath(path)) { ... return path; }
        //    if (path.EndsWith(".url")) {
        //       string iconFile = _fileSystem.GetIniValue(...)
        //       ...
        //    }
        //    return path;
        // }

        // It doesn't catch exceptions! So modifying it to redact trace on exception
        // requires adding a try-catch block?
        // OR does it trace on other conditions?

        // It traces: "Blocked resolution for unsafe path: {path}"
        // Let's test that one first.

        // Act
        string unsafePath = @"\\UnsafeServer\Share\Malicious.url";
        iconService.ResolveIconPath(unsafePath);

        // Flush trace
        Trace.Flush();
        string log = _traceOutput.ToString();

        // Assert
        // Current behavior: logs full path.
        // Desired behavior: logs redacted path.

        // This test asserts the CURRENT behavior (failure expectation)
        // or asserts the DESIRED behavior (and fails now).
        // I will assert the DESIRED behavior.

        Assert.Contains(PathSecurity.RedactPath(unsafePath), log);
        Assert.DoesNotContain(unsafePath, log);
    }

    [Fact]
    public void IconService_ExtractIconBytes_RedactsPath_OnException()
    {
        // Arrange
        var fileSystem = new FaultyFileSystem();
        var iconService = new IconService(fileSystem);
        string secretPath = @"C:\Users\Admin\Documents\SecretProject\App.exe";

        // Act
        // ExtractIconBytes calls GetCachedLastWriteTime which calls GetLastWriteTime.
        // FaultyFileSystem throws on GetLastWriteTime?
        // IconService does NOT catch exceptions in ExtractIconBytes main body?

        // Wait, ExtractIconBytes:
        // try { ... } catch (Exception ex) { Trace... } IS NOT THERE for the whole method.
        // But GetCustomIconBytes has try-catch.
        // ExtractSystemIcon has try-catch.

        // Let's force ExtractSystemIcon to fail.
        // It fails if P/Invoke fails or if ResolveIconPath fails (but ResolveIconPath doesn't throw usually).

        // However, ExtractSystemIcon calls ResolveIconPath.
        // If ResolveIconPath throws (due to FaultyFileSystem), ExtractSystemIcon catches it?
        // ExtractSystemIcon:
        // try {
        //    resolvedPath = ResolveIconPath(path);
        //    ...
        // } catch (Exception ex) {
        //    Trace.WriteLine($"Failed to extract icon for {path} ... {ex.Message}");
        // }

        // So if GetIniValue throws in ResolveIconPath, ExtractSystemIcon catches it and logs.

        // We need a path ending in .url to trigger ResolveIconPath -> GetIniValue
        string secretUrl = @"C:\Users\Admin\Documents\SecretProject\App.url";

        iconService.ExtractIconBytes(secretUrl);

        Trace.Flush();
        string log = _traceOutput.ToString();

        Assert.Contains(PathSecurity.RedactPath(secretUrl), log);
        Assert.DoesNotContain(secretUrl, log);
    }

    [Fact]
    public void WindowsShortcutResolver_ResolveTarget_RedactsPath_OnException()
    {
        // Arrange
        var fileSystem = new FaultyFileSystem();
        var resolver = new WindowsShortcutResolver(fileSystem);
        string secretPath = @"C:\Users\Admin\Documents\SecretProject\Link.url";

        // Act
        resolver.ResolveTarget(secretPath);

        Trace.Flush();
        string log = _traceOutput.ToString();

        Assert.Contains(PathSecurity.RedactPath(secretPath), log);
        Assert.DoesNotContain(secretPath, log);
    }

    private class FaultyFileSystem : IFileSystem
    {
        public void CreateDirectory(string path) { }
        public bool DirectoryExists(string path) => true;
        public bool FileExists(string path) => true;
        public string[] GetFiles(string path) => throw new IOException("Simulated IO Error");
        public long GetFileSize(string path) => 0;

        // Throwing here will trigger exception in ResolveIconPath (called by ExtractSystemIcon)
        // and ResolveUrl (called by ResolveTarget)
        public string GetIniValue(string path, string section, string key) => throw new IOException("Simulated IO Error");

        public DateTime GetLastWriteTime(string path) => DateTime.Now;
        public Stream OpenRead(string path) => throw new IOException("Simulated IO Error");
        public byte[] ReadAllBytes(string path) => throw new IOException("Simulated IO Error");
    }
}
