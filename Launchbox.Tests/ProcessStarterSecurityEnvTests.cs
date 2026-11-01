using Launchbox.Services;
using System;
using System.Diagnostics;
using Xunit;

namespace Launchbox.Tests;

[Trait("Category", "Security")]
public class ProcessStarterSecurityEnvTests
{
    private readonly ProcessStarter _processStarter = new();

    [Fact]
    public void Start_ThrowsUnauthorizedAccessException_ForUnsafeArgumentsHiddenInEnvVar()
    {
        Environment.SetEnvironmentVariable("TEST_ENV_VAR", @"\\server\share");
        try
        {
            var startInfo = new ProcessStartInfo("cmd.exe")
            {
                Arguments = @"--target %TEST_ENV_VAR%\payload",
                UseShellExecute = false
            };
            var exception = Assert.Throws<UnauthorizedAccessException>(() => _processStarter.Start(startInfo));
            Assert.Contains("Execution with unsafe arguments", exception.Message);
        }
        finally
        {
            Environment.SetEnvironmentVariable("TEST_ENV_VAR", null);
        }
    }
}
