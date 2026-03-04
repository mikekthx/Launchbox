using Launchbox.Services;
using Microsoft.UI.Xaml;
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

    // Note: Testing the `true` branch of `if (parent is DependencyObject)` is intentionally omitted.
    // In a standard xUnit headless environment, instantiating any WinUI 3 `DependencyObject`
    // (such as `Grid`, `Button`, or even a mock deriving from `DependencyObject`) attempts WinRT COM activation,
    // which throws `REGDB_E_CLASSNOTREG` because the WinAppSDK is not bootstrapped in the test runner.
    // The `WinUIVisualTreeService` has been correctly refactored to use `IVisualTreeHelperWrapper`,
    // making it testable in theory, but the framework limitation prevents passing a valid parameter.
}
