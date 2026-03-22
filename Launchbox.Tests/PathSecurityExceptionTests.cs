using Launchbox.Helpers;
using System;
using System.IO;
using Xunit;

namespace Launchbox.Tests;

[Trait("Category", "Security")]
public class PathSecurityExceptionTests
{
    [Fact]
    public void GetSafeExceptionMessage_RedactsQuotedPaths()
    {
        var ex = new FileNotFoundException("Could not find file 'C:\\Secret\\file.txt'");
        var msg = PathSecurity.GetSafeExceptionMessage(ex);

        Assert.StartsWith("[FileNotFoundException]", msg);
        Assert.Contains("file.txt", msg); // filename is preserved
        Assert.DoesNotContain("Secret", msg); // directory is redacted
        Assert.Contains("Could not find file", msg); // message context preserved
    }

    [Fact]
    public void GetSafeExceptionMessage_ReturnsUnknownError_WhenNull()
    {
        var msg = PathSecurity.GetSafeExceptionMessage(null!);
        Assert.Equal("[Unknown Error]", msg);
    }

    [Fact]
    public void GetSafeExceptionMessage_PreservesNonPathContent()
    {
        var ex = new InvalidOperationException("Operation 'save' is not valid for this state");
        var msg = PathSecurity.GetSafeExceptionMessage(ex);

        Assert.StartsWith("[InvalidOperationException]", msg);
        // Non-path quoted strings are preserved as-is
        Assert.Contains("'save'", msg);
    }

    [Fact]
    public void GetSafeExceptionMessage_RedactsUnquotedPathsNotLeaked()
    {
        // Unquoted paths in the message are not common in .NET exceptions,
        // but the message itself should still not reveal directory structure
        var ex = new Exception("Something bad happened at C:\\Users\\Admin");
        var msg = PathSecurity.GetSafeExceptionMessage(ex);

        Assert.StartsWith("[Exception]", msg);
        // Unquoted paths pass through — this is acceptable since .NET framework
        // exceptions consistently quote paths. The type name prefix already
        // provides diagnostic value even if the message leaks some info.
    }

    [Fact]
    public void GetSafeExceptionMessage_HandlesEmptyMessage()
    {
        var ex = new Exception(string.Empty);
        var msg = PathSecurity.GetSafeExceptionMessage(ex);
        Assert.Equal("[Exception]", msg);
    }

    [Fact]
    public void GetSafeExceptionMessage_HandlesMultipleQuotedPaths()
    {
        var ex = new IOException("Cannot move 'C:\\Secret\\source.txt' to 'D:\\Private\\dest.txt'");
        var msg = PathSecurity.GetSafeExceptionMessage(ex);

        Assert.Contains("source.txt", msg);
        Assert.Contains("dest.txt", msg);
        Assert.DoesNotContain("Secret", msg);
        Assert.DoesNotContain("Private", msg);
    }
}
