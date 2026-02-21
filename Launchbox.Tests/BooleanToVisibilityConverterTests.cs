using Launchbox.Helpers;
using Microsoft.UI.Xaml;
using System;
using Xunit;

namespace Launchbox.Tests;

public class BooleanToVisibilityConverterTests
{
    [Fact]
    public void Convert_True_ReturnsVisible()
    {
        var converter = new BooleanToVisibilityConverter();
        var result = converter.Convert(true, typeof(Visibility), null, "en-US");
        Assert.Equal(Visibility.Visible, result);
    }

    [Fact]
    public void Convert_False_ReturnsCollapsed()
    {
        var converter = new BooleanToVisibilityConverter();
        var result = converter.Convert(false, typeof(Visibility), null, "en-US");
        Assert.Equal(Visibility.Collapsed, result);
    }

    [Fact]
    public void Convert_True_Invert_ReturnsCollapsed()
    {
        var converter = new BooleanToVisibilityConverter();
        var result = converter.Convert(true, typeof(Visibility), "Invert", "en-US");
        Assert.Equal(Visibility.Collapsed, result);
    }

    [Fact]
    public void Convert_False_Invert_ReturnsVisible()
    {
        var converter = new BooleanToVisibilityConverter();
        var result = converter.Convert(false, typeof(Visibility), "Invert", "en-US");
        Assert.Equal(Visibility.Visible, result);
    }

    [Fact]
    public void Convert_Null_ReturnsCollapsed()
    {
        var converter = new BooleanToVisibilityConverter();
        var result = converter.Convert(null, typeof(Visibility), null, "en-US");
        Assert.Equal(Visibility.Collapsed, result);
    }

    [Fact]
    public void Convert_NonBoolean_ReturnsCollapsed()
    {
        var converter = new BooleanToVisibilityConverter();
        var result = converter.Convert("not a bool", typeof(Visibility), null, "en-US");
        Assert.Equal(Visibility.Collapsed, result);
    }

    [Fact]
    public void ConvertBack_ThrowsNotImplementedException()
    {
        var converter = new BooleanToVisibilityConverter();
        Assert.Throws<NotImplementedException>(() =>
            converter.ConvertBack(Visibility.Visible, typeof(bool), null, "en-US"));
    }
}
