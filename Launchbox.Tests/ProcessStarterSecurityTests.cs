using System;
using System.Diagnostics;
using Launchbox.Services;
using Xunit;

namespace Launchbox.Tests;

public class ProcessStarterSecurityTests
{
    [Fact]
    public void Start_WithUnsafePath_ThrowsUnauthorizedAccessException()
    {
        var starter = new ProcessStarter();
        var startInfo = new ProcessStartInfo(@"\\malicious\share\file.exe");

        var ex = Assert.Throws<UnauthorizedAccessException>(() => starter.Start(startInfo));
        Assert.Contains("Execution of unsafe path blocked", ex.Message);
    }
}
