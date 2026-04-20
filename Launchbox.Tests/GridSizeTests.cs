using Launchbox.Helpers;
using System;
using Xunit;

namespace Launchbox.Tests;

public class GridSizeTests
{
    [Fact]
    public void GridSize_HasExpectedValues()
    {
        Assert.True(Enum.IsDefined<GridSize>(GridSize.Small));
        Assert.True(Enum.IsDefined<GridSize>(GridSize.Medium));
        Assert.True(Enum.IsDefined<GridSize>(GridSize.Large));
    }

    [Fact]
    public void GridSize_ContainsExactlyThreeValues()
    {
        var values = Enum.GetValues<GridSize>();
        Assert.Equal(3, values.Length);
    }
}
