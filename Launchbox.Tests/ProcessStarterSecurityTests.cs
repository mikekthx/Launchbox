using Launchbox.Services;
using System;
using System.Diagnostics;
using Xunit;

namespace Launchbox.Tests;

[Trait("Category", "Security")]
public class ProcessStarterSecurityTests
{
    private readonly ProcessStarter _processStarter;

    public ProcessStarterSecurityTests()
    {
        _processStarter = new ProcessStarter();
    }

    [Theory]
    [InlineData(@"\\attacker\share\malware.exe")]
    [InlineData(@"//attacker/share/malware.exe")]
    [InlineData(@"/\attacker/share/malware.exe")]
    [InlineData(@"\/attacker/share/malware.exe")]
    [InlineData(@"\\?\UNC\attacker\share\malware.exe")]
    public void Start_ThrowsUnauthorizedAccessException_ForUnsafePath(string unsafePath)
    {
        var startInfo = new ProcessStartInfo(unsafePath);

        var exception = Assert.Throws<UnauthorizedAccessException>(() => _processStarter.Start(startInfo));
        Assert.Contains("Execution of unsafe path", exception.Message);
    }

    [Fact]
    public void Start_ThrowsArgumentNullException_ForNullStartInfo()
    {
        Assert.Throws<ArgumentNullException>(() => _processStarter.Start(null!));
    }

    [Theory]
    [InlineData("https://example.com")]
    [InlineData("http://example.com/path?q=1")]
    [InlineData("--url=https://example.com")]
    public void Start_AllowsUrlArguments(string urlArgs)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = OperatingSystem.IsWindows() ? "cmd.exe" : "echo",
            Arguments = OperatingSystem.IsWindows() ? $"/c echo {urlArgs}" : urlArgs,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        // Should not throw — URLs are legitimate arguments
        using var process = _processStarter.Start(startInfo);
        process?.Kill();
    }

    [Theory]
    [InlineData(@"\\server\share\payload")]
    [InlineData("//server/share/payload")]
    public void Start_ThrowsUnauthorizedAccessException_ForUnsafeArguments(string unsafeArgs)
    {
        var startInfo = new ProcessStartInfo("cmd.exe")
        {
            Arguments = unsafeArgs,
            UseShellExecute = false
        };

        var exception = Assert.Throws<UnauthorizedAccessException>(() => _processStarter.Start(startInfo));
        Assert.Contains("Execution with unsafe arguments", exception.Message);
    }

    [Theory]
    [InlineData(@"\\server\share\payload")]
    [InlineData("//server/share/payload")]
    public void Start_ThrowsUnauthorizedAccessException_ForUnsafeWorkingDirectory(string unsafeDir)
    {
        var startInfo = new ProcessStartInfo("cmd.exe")
        {
            WorkingDirectory = unsafeDir,
            UseShellExecute = false
        };

        var exception = Assert.Throws<UnauthorizedAccessException>(() => _processStarter.Start(startInfo));
        Assert.Contains("Execution with unsafe working directory", exception.Message);
    }

    [Theory]
    [InlineData(@"%TEST_ENV_VAR%\unsafe.exe")]
    public void Start_ThrowsUnauthorizedAccessException_ForUnsafePathHiddenInEnvVar(string unsafePathWithEnvVar)
    {
        Environment.SetEnvironmentVariable("TEST_ENV_VAR", @"\\server\share");
        try
        {
            var startInfo = new ProcessStartInfo(unsafePathWithEnvVar);
            var exception = Assert.Throws<UnauthorizedAccessException>(() => _processStarter.Start(startInfo));
            Assert.Contains("Execution of unsafe path", exception.Message);
        }
        finally
        {
            Environment.SetEnvironmentVariable("TEST_ENV_VAR", null);
        }
    }

    [Fact]
    public void Start_ThrowsUnauthorizedAccessException_ForUnsafeWorkingDirectoryHiddenInEnvVar()
    {
        Environment.SetEnvironmentVariable("TEST_ENV_VAR", @"\\server\share");
        try
        {
            var startInfo = new ProcessStartInfo("cmd.exe")
            {
                WorkingDirectory = @"%TEST_ENV_VAR%\unsafe",
                UseShellExecute = false
            };
            var exception = Assert.Throws<UnauthorizedAccessException>(() => _processStarter.Start(startInfo));
            Assert.Contains("Execution with unsafe working directory", exception.Message);
        }
        finally
        {
            Environment.SetEnvironmentVariable("TEST_ENV_VAR", null);
        }
    }
}
