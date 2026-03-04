using Launchbox.Services;
using System;
using Xunit;

namespace Launchbox.Tests;

public class WinUIVisualTreeServiceTests
{
    [Fact]
    public void GetChild_ThrowsArgumentException_WhenParentIsNotDependencyObject()
    {
        // Arrange
        var service = new WinUIVisualTreeService();
        var invalidParent = "Not a DependencyObject";

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => service.GetChild(invalidParent, 0));
        Assert.Equal("Parent must be a DependencyObject", ex.Message);
    }

    [Fact]
    public void GetChildrenCount_ReturnsZero_WhenParentIsNotDependencyObject()
    {
        // Arrange
        var service = new WinUIVisualTreeService();
        var invalidParent = "Not a DependencyObject";

        // Act
        var result = service.GetChildrenCount(invalidParent);

        // Assert
        Assert.Equal(0, result);
    }
}
