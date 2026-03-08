using Launchbox.Services;
using Launchbox.Tests;
using Xunit;

namespace Launchbox.Tests;

public class WindowsShortcutResolverTests
{
    private readonly MockFileSystem _fileSystem;
    private readonly WindowsShortcutResolver _resolver;

    public WindowsShortcutResolverTests()
    {
        _fileSystem = new MockFileSystem();
        _resolver = new WindowsShortcutResolver(_fileSystem);
    }

    [Fact]
    public void ResolveTarget_Returns_Url_From_UrlFile()
    {
        string shortcutPath = @"C:\shortcuts\google.url";
        string targetUrl = "https://www.google.com";

        _fileSystem.SetIniValue(shortcutPath, "InternetShortcut", "URL", targetUrl);

        string? result = _resolver.ResolveTarget(shortcutPath);

        Assert.Equal(targetUrl, result);
    }

    [Fact]
    public void ResolveTarget_Returns_Null_If_Url_Missing()
    {
        string shortcutPath = @"C:\shortcuts\empty.url";

        string? result = _resolver.ResolveTarget(shortcutPath);

        Assert.Null(result);
    }

    [Fact]
    public void ResolveTarget_Returns_Null_For_Lnk_On_Linux()
    {
        if (!System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows))
        {
            string shortcutPath = @"C:\shortcuts\app.lnk";
            string? result = _resolver.ResolveTarget(shortcutPath);
            Assert.Null(result);
        }
    }

    [Fact]
    public void ResolveTarget_Expands_EnvironmentVariables_From_UrlFile()
    {
        string shortcutPath = @"C:\shortcuts\app.url";
        string envVar = "RESOLVER_TEST_ENV_VAR";
        string envValue = @"C:\ResolvedTarget";

        // Environment.ExpandEnvironmentVariables uses %VAR% syntax on all .NET platforms
        string targetUrl = $@"%{envVar}%\app.exe";
        string expectedUrl = $@"{envValue}\app.exe";

        System.Environment.SetEnvironmentVariable(envVar, envValue);

        try
        {
            _fileSystem.SetIniValue(shortcutPath, "InternetShortcut", "URL", targetUrl);

            string? result = _resolver.ResolveTarget(shortcutPath);

            Assert.Equal(expectedUrl, result);
        }
        finally
        {
            System.Environment.SetEnvironmentVariable(envVar, null);
        }
    }
}
