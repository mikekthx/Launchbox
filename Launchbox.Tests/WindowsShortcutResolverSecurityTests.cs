using Launchbox.Helpers;
using Launchbox.Services;
using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using Xunit;

namespace Launchbox.Tests;

public class WindowsShortcutResolverSecurityTests : IDisposable
{
    private readonly StringBuilder _traceOutput = new();
    private readonly TextWriterTraceListener _listener;

    public WindowsShortcutResolverSecurityTests()
    {
        _listener = new TextWriterTraceListener(new StringWriter(_traceOutput));
        Trace.Listeners.Add(_listener);
    }

    public void Dispose()
    {
        Trace.Listeners.Remove(_listener);
        _listener.Dispose();
    }

    [Fact]
    public void ResolveTarget_RedactsExceptionMessage()
    {
        // Arrange
        var faultyFileSystem = new FaultyFileSystem();
        var resolver = new WindowsShortcutResolver(faultyFileSystem);
        string sensitivePath = @"C:\Users\Admin\Documents\Secret\shortcut.url";
        string sensitiveMessage = $"Could not access {sensitivePath}";

        faultyFileSystem.SetExceptionToThrow(new IOException(sensitiveMessage));

        // Act
        var result = resolver.ResolveTarget(sensitivePath);

        // Assert
        _listener.Flush();
        string logs = _traceOutput.ToString();

        // It should NOT contain the sensitive message
        Assert.DoesNotContain(sensitiveMessage, logs);

        // It should contain the redacted path (via PathSecurity.RedactPath)
        Assert.Contains(PathSecurity.RedactPath(sensitivePath), logs);

        // It should contain the safe exception message ([IOException])
        Assert.Contains("[IOException]", logs);

        Assert.Null(result);
    }

    private class FaultyFileSystem : MockFileSystem
    {
        private Exception? _exceptionToThrow;

        public void SetExceptionToThrow(Exception ex)
        {
            _exceptionToThrow = ex;
        }

        public override string GetIniValue(string path, string section, string key)
        {
            if (_exceptionToThrow != null)
            {
                throw _exceptionToThrow;
            }
            return base.GetIniValue(path, section, key);
        }
    }
}
