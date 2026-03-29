using Launchbox.Helpers;
using Microsoft.UI.Xaml;
using System;
using Xunit;

namespace Launchbox.Tests;

public class EmptyStringToCollapsedConverterTests
{
    [Fact]
    public void Convert_NonEmptyString_ReturnsVisible()
    {
        var converter = new EmptyStringToCollapsedConverter();
        var result = converter.Convert("Some string", typeof(Visibility), null!, "en-US");
        Assert.Equal(Visibility.Visible, result);
    }

    [Fact]
    public void Convert_EmptyString_ReturnsCollapsed()
    {
        var converter = new EmptyStringToCollapsedConverter();
        var result = converter.Convert(string.Empty, typeof(Visibility), null!, "en-US");
        Assert.Equal(Visibility.Collapsed, result);
    }

    [Fact]
    public void Convert_Null_ReturnsCollapsed()
    {
        var converter = new EmptyStringToCollapsedConverter();
        var result = converter.Convert(null!, typeof(Visibility), null!, "en-US");
        Assert.Equal(Visibility.Collapsed, result);
    }

    [Fact]
    public void Convert_NonString_ReturnsCollapsed()
    {
        var converter = new EmptyStringToCollapsedConverter();
        var result = converter.Convert(123, typeof(Visibility), null!, "en-US");
        Assert.Equal(Visibility.Collapsed, result);
    }

    [Fact]
    public void ConvertBack_ThrowsNotSupportedException()
    {
        var converter = new EmptyStringToCollapsedConverter();
        Assert.Throws<NotSupportedException>(() =>
            converter.ConvertBack(Visibility.Visible, typeof(string), null!, "en-US"));
    }
}
