using Launchbox.Helpers;
using System;
using System.IO;
using Xunit;

namespace Launchbox.Tests;

public class PathSecurityExceptionTests
{
    [Fact]
    public void GetSafeExceptionMessage_ReturnsTypeName()
    {
        var ex = new FileNotFoundException("Could not find file 'C:\\Secret\\file.txt'");
        var msg = PathSecurity.GetSafeExceptionMessage(ex);
        Assert.Equal("[FileNotFoundException]", msg);
        Assert.DoesNotContain("Secret", msg);
    }

    [Fact]
    public void GetSafeExceptionMessage_ReturnsUnknownError_WhenNull()
    {
        var msg = PathSecurity.GetSafeExceptionMessage(null!);
        Assert.Equal("[Unknown Error]", msg);
    }

    [Fact]
    public void GetSafeExceptionMessage_HandlesGenericException()
    {
        var ex = new Exception("Something bad happened at C:\\Users\\Admin");
        var msg = PathSecurity.GetSafeExceptionMessage(ex);
        Assert.Equal("[Exception]", msg);
        Assert.DoesNotContain("Admin", msg);
    }
}
