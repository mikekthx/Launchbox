using Xunit;
using Launchbox.Helpers;
using System.IO;

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

    [Fact]
    public void IsUnsafePath_ReturnsTrue_OnInvalidPathException()
    {
        // These paths cause Path.GetFullPath (and possibly new Uri) to throw exceptions on Windows.
        // We want IsUnsafePath to return true (fail closed) instead of false.
        Assert.True(PathSecurity.IsUnsafePath("path|with|pipe"));
        Assert.True(PathSecurity.IsUnsafePath("path<with<bracket"));
        Assert.True(PathSecurity.IsUnsafePath("path>with>bracket"));
        Assert.True(PathSecurity.IsUnsafePath("path\"with\"quote"));
    }

    [Fact]
    public void IsUnsafePath_AllowsRelativePaths()
    {
        Assert.False(PathSecurity.IsUnsafePath("config.xml"));
        Assert.False(PathSecurity.IsUnsafePath(@"subfolder\file.txt"));
        Assert.False(PathSecurity.IsUnsafePath("..\\parent.txt"));
    }

    [Fact]
    public void IsUnsafePath_HandlesQuestionMarkCorrectly()
    {
        // ? is strictly invalid in standard paths
        Assert.True(PathSecurity.IsUnsafePath("path?with?question"));
        Assert.True(PathSecurity.IsUnsafePath("C:\\path\\file?.txt"));

        // ? is allowed ONLY as part of the \\?\ prefix
        Assert.False(PathSecurity.IsUnsafePath(@"\\?\C:\Windows\System32\notepad.exe"));

        // ? is NOT allowed elsewhere even if starting with \\?\
        Assert.True(PathSecurity.IsUnsafePath(@"\\?\C:\Windows\System32\note?pad.exe"));

        // \??\ prefix is still considered unsafe by policy (though technically valid NT path)
        Assert.True(PathSecurity.IsUnsafePath(@"\??\C:\Windows\System32\notepad.exe"));
    }
}
