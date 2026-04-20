using System;
using Launchbox.Helpers;
using Xunit;

namespace Launchbox.Tests;

public class GridSizeTests
{
    [Fact]
    public void GridSize_HasExpectedValues()
    {
        Assert.True(Enum.IsDefined(typeof(GridSize), "Small"));
        Assert.True(Enum.IsDefined(typeof(GridSize), "Medium"));
        Assert.True(Enum.IsDefined(typeof(GridSize), "Large"));
    }

    [Fact]
    public void GridSize_ContainsExactlyThreeValues()
    {
        var values = Enum.GetValues(typeof(GridSize));
        Assert.Equal(3, values.Length);
    }
}
