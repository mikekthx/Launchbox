using Launchbox.Helpers;
using System.IO;
using Xunit;

namespace Launchbox.Tests;

public class PathSecurityTests
{
    [Theory]
    [InlineData(@"\\attacker\share\file.lnk")]
    [InlineData(@"\\?\UNC\attacker\share\file.lnk")]
    [InlineData(@"//attacker/share/file.lnk")]
    [InlineData(@"\??\UNC\attacker\share\file.lnk")]
    [InlineData(@"/\attacker/share/file.lnk")]
    [InlineData(@"\/attacker/share/file.lnk")]
    public void IsUnsafePath_IdentifiesUnsafePaths(string path)
    {
        Assert.True(PathSecurity.IsUnsafePath(path), $"Path should be unsafe: {path}");
    }

    [Fact]
    public void IsUnsafePath_AllowsSafeLocalPaths()
    {
        Assert.False(PathSecurity.IsUnsafePath(@"C:\Users\User\Desktop\test.lnk"));
        Assert.False(PathSecurity.IsUnsafePath(@"D:\Games\Launchbox.exe"));
    }

    [Fact]
    public void IsUnsafePath_AllowsLocalLongPaths()
    {
        Assert.False(PathSecurity.IsUnsafePath(@"\\?\C:\Users\User\Desktop\test.lnk"));
    }

    [Fact]
    public void IsUnsafePath_BlocksInvalidLongPaths()
    {
        Assert.True(PathSecurity.IsUnsafePath(@"\\?\Volume{GUID}\"));
        Assert.True(PathSecurity.IsUnsafePath(@"\\?\GLOBALROOT\Device\HarddiskVolumeShadowCopy1\"));
    }

    [Fact]
    public void IsUnsafePath_HandlesNullOrEmpty()
    {
        Assert.False(PathSecurity.IsUnsafePath(null));
        Assert.False(PathSecurity.IsUnsafePath(""));
        Assert.False(PathSecurity.IsUnsafePath("   "));
    }
}
