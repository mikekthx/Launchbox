using Launchbox.Services;
using System;
using System.Diagnostics;
using Xunit;

namespace Launchbox.Tests;

public class ProcessStarterTests
{
    private readonly ProcessStarter _processStarter;

    public ProcessStarterTests()
    {
        _processStarter = new ProcessStarter(new MockShortcutResolver());
    }

    [Fact]
    public void Start_WithValidStartInfo_ReturnsProcess()
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = OperatingSystem.IsWindows() ? "cmd.exe" : "echo",
            Arguments = OperatingSystem.IsWindows() ? "/c echo test" : "test",
            UseShellExecute = false,
            CreateNoWindow = true
        };

        var process = _processStarter.Start(startInfo);

        Assert.NotNull(process);
        process.WaitForExit();
        Assert.True(process.HasExited);
    }

    [Fact]
    public void Start_WithNonExistentFile_ThrowsException()
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "this_file_does_not_exist_12345.exe",
            UseShellExecute = false
        };

        Assert.ThrowsAny<Exception>(() => _processStarter.Start(startInfo));
    }
}
