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

    [Theory]
    [InlineData(false)]
    [InlineData(null)]
    [InlineData("not a bool")]
    public void Convert_NonTrue_ReturnsCollapsedChevron(object? value)
    {
        var converter = new CollapseChevronConverter();
        var result = converter.Convert(value!, typeof(string), null!, "en-US");
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
