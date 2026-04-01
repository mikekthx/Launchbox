using Launchbox.Helpers;
using Microsoft.UI.Xaml;
using Xunit;

namespace Launchbox.Tests;

public class BooleanToVisibilityConverterTests
{
    [Fact]
    public void Convert_True_ReturnsVisible()
    {
        var converter = new BooleanToVisibilityConverter();
        var result = converter.Convert(true, typeof(Visibility), null!, "en-US");
        Assert.Equal(Visibility.Visible, result);
    }

    [Fact]
    public void Convert_False_ReturnsCollapsed()
    {
        var converter = new BooleanToVisibilityConverter();
        var result = converter.Convert(false, typeof(Visibility), null!, "en-US");
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
        var result = converter.Convert(null!, typeof(Visibility), null!, "en-US");
        Assert.Equal(Visibility.Collapsed, result);
    }

    [Fact]
    public void Convert_NonBoolean_ReturnsCollapsed()
    {
        var converter = new BooleanToVisibilityConverter();
        var result = converter.Convert("not a bool", typeof(Visibility), null!, "en-US");
        Assert.Equal(Visibility.Collapsed, result);
    }

    [Fact]
    public void ConvertBack_Visible_ReturnsTrue()
    {
        var converter = new BooleanToVisibilityConverter();
        var result = converter.ConvertBack(Visibility.Visible, typeof(bool), null!, "en-US");
        Assert.Equal(true, result);
    }

    [Theory]
    [InlineData(Visibility.Collapsed)]
    [InlineData(null)]
    [InlineData("random string")]
    public void ConvertBack_NonVisible_ReturnsFalse(object? value)
    {
        var converter = new BooleanToVisibilityConverter();
        var result = converter.ConvertBack(value!, typeof(bool), null!, "en-US");
        Assert.Equal(false, result);
    }

    [Fact]
    public void ConvertBool_True_ReturnsVisible()
    {
        Assert.Equal(Visibility.Visible, BooleanToVisibilityConverter.ConvertBool(true));
    }

    [Fact]
    public void ConvertBool_False_ReturnsCollapsed()
    {
        Assert.Equal(Visibility.Collapsed, BooleanToVisibilityConverter.ConvertBool(false));
    }

    [Fact]
    public void ConvertBool_True_Invert_ReturnsCollapsed()
    {
        Assert.Equal(Visibility.Collapsed, BooleanToVisibilityConverter.ConvertBool(true, invert: true));
    }

    [Fact]
    public void ConvertBool_False_Invert_ReturnsVisible()
    {
        Assert.Equal(Visibility.Visible, BooleanToVisibilityConverter.ConvertBool(false, invert: true));
    }
}
