using Launchbox.Helpers;
using System;
using Xunit;

namespace Launchbox.Tests;

public class CollapseChevronConverterTests
{
    [Fact]
    public void Convert_True_ReturnsExpandedChevron()
    {
        var converter = new CollapseChevronConverter();
        var result = converter.Convert(true, typeof(string), null!, "en-US");
        Assert.Equal("\uE76C", result);
    }

    [Fact]
    public void Convert_False_ReturnsCollapsedChevron()
    {
        var converter = new CollapseChevronConverter();
        var result = converter.Convert(false, typeof(string), null!, "en-US");
        Assert.Equal("\uE76E", result);
    }

    [Fact]
    public void Convert_Null_ReturnsCollapsedChevron()
    {
        var converter = new CollapseChevronConverter();
        var result = converter.Convert(null!, typeof(string), null!, "en-US");
        Assert.Equal("\uE76E", result);
    }

    [Fact]
    public void Convert_NonBoolean_ReturnsCollapsedChevron()
    {
        var converter = new CollapseChevronConverter();
        var result = converter.Convert("not a bool", typeof(string), null!, "en-US");
        Assert.Equal("\uE76E", result);
    }

    [Fact]
    public void ConvertBack_ThrowsNotSupportedException()
    {
        var converter = new CollapseChevronConverter();
        Assert.Throws<NotSupportedException>(() =>
            converter.ConvertBack("\uE76C", typeof(bool), null!, "en-US"));
    }
}
