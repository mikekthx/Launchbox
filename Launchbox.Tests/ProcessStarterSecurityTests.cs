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
}
