using Launchbox.Helpers;
using System.IO;
using Xunit;

namespace Launchbox.Tests;

[Trait("Category", "Security")]
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

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t")]
    [Trait("Category", "Security")]
    public void IsUnsafePath_ReturnsTrueForNullOrWhitespace(string? path)
    {
        Assert.True(PathSecurity.IsUnsafePath(path));
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

    [Theory]
    [InlineData("file://server/share")]
    [InlineData("--path=file://server/share/payload.exe")]
    [InlineData("smb://attacker/share")]
    [InlineData("dav://attacker/share")]
    [Trait("Category", "Security")]
    public void ContainsUncPath_BlocksNetworkFileSchemes(string args)
    {
        Assert.True(PathSecurity.ContainsUncPath(args), $"Should detect UNC in: {args}");
    }

    [Theory]
    [InlineData("https://example.com")]
    [InlineData("--url=https://example.com")]
    [InlineData("http://example.com/path")]
    [InlineData("ftp://mirror.example.com/file.zip")]
    public void ContainsUncPath_AllowsSafeWebSchemes(string args)
    {
        Assert.False(PathSecurity.ContainsUncPath(args), $"Should allow safe scheme: {args}");
    }

    [Theory]
    [InlineData("HTTPS://example.com")]
    [InlineData("--url=HTTP://example.com")]
    [InlineData("FTP://mirror.example.com/file.zip")]
    [InlineData("Https://example.com/path")]
    public void ContainsUncPath_AllowsMixedCaseSafeWebSchemes(string args)
    {
        // Mixed-case schemes are still valid URLs and must not be flagged as UNC paths
        Assert.False(PathSecurity.ContainsUncPath(args), $"Should allow mixed-case scheme: {args}");
    }

    [Theory]
    [InlineData(@"\\server\share")]
    [InlineData(@"//server/share")]
    [InlineData(@"\/server")]
    [InlineData(@"/\server")]
    public void ContainsUncPath_BlocksStandardUncPatterns(string args)
    {
        Assert.True(PathSecurity.ContainsUncPath(args), $"Should detect UNC: {args}");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("simple argument")]
    public void ContainsUncPath_AllowsSafeArguments(string? args)
    {
        Assert.False(PathSecurity.ContainsUncPath(args));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void RedactPath_ReturnsEmpty_WhenNullOrWhitespace(string? path)
    {
        Assert.Equal("[Empty]", PathSecurity.RedactPath(path));
    }

    [Fact]
    public void RedactPath_RedactsStandardPath()
    {
        // Check platform behavior
        string path;
        string expected;

        if (Path.DirectorySeparatorChar == '/')
        {
            path = "/home/user/test.txt";
            expected = @"...\test.txt";
        }
        else
        {
            path = @"C:\Users\User\Desktop\test.txt";
            expected = @"...\test.txt";
        }

        Assert.Equal(expected, PathSecurity.RedactPath(path));
    }

    [Fact]
    public void RedactPath_RedactsUNCPath_IfParsable()
    {
        if (Path.DirectorySeparatorChar == '/')
        {
            var path = @"\\server\share\file.txt";
            var expected = @"...\file.txt";
            Assert.Equal(expected, PathSecurity.RedactPath(path));
        }
        else
        {
            var path = @"\\server\share\file.txt";
            var expected = @"...\file.txt";
            Assert.Equal(expected, PathSecurity.RedactPath(path));
        }
    }

    [Fact]
    public void RedactPath_ReturnsRedacted_WhenRoot()
    {
        string path;
        if (Path.DirectorySeparatorChar == '/')
        {
            path = "/";
        }
        else
        {
            path = @"C:\";
        }

        Assert.Equal("[Redacted]", PathSecurity.RedactPath(path));
    }
}
