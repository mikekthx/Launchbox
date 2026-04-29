using Launchbox.Helpers;
using Microsoft.UI.Xaml;
using System;
using Xunit;

namespace Launchbox.Tests;

public class CollapseStateConverterTests
{
    private readonly CollapseStateConverter _converter = new();

    [Fact]
    public void Convert_True_ReturnsLocalizedCollapsedString()
    {
        var result = _converter.Convert(true, typeof(string), null!, "");
        Assert.Equal("CollapseState_Collapsed", result);
    }

    [Fact]
    public void Convert_False_ReturnsLocalizedExpandedString()
    {
        var result = _converter.Convert(false, typeof(string), null!, "");
        Assert.Equal("CollapseState_Expanded", result);
    }

    [Fact]
    public void Convert_Null_ReturnsLocalizedExpandedString()
    {
        var result = _converter.Convert(null!, typeof(string), null!, "");
        Assert.Equal("CollapseState_Expanded", result);
    }

    [Fact]
    public void ConvertBack_ThrowsNotSupportedException()
    {
        Assert.Throws<NotSupportedException>(() => _converter.ConvertBack("Collapsed", typeof(bool), null!, ""));
    }
}
