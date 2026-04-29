using Launchbox.Helpers;
using System;
using Xunit;

namespace Launchbox.Tests;

public class GridSizeTests
{
    [Fact]
    public void GridSize_ContainsExactlyThreeValues()
    {
        var values = Enum.GetValues<GridSize>();
        Assert.Equal(3, values.Length);
    }
}
