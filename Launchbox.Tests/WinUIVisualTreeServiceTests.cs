using Launchbox.Services;
using Microsoft.UI.Xaml;
using System;
using Xunit;

namespace Launchbox.Tests;

public class WinUIVisualTreeServiceTests
{
    private class MockVisualTreeHelper : IVisualTreeHelperWrapper
    {
        public int ChildCountToReturn { get; set; } = 0;
        public DependencyObject? ChildToReturn { get; set; } = null;
        public DependencyObject? LastParentPassed { get; private set; }
        public int LastIndexPassed { get; private set; }

        public int GetChildrenCount(DependencyObject reference)
        {
            LastParentPassed = reference;
            return ChildCountToReturn;
        }

        public DependencyObject GetChild(DependencyObject reference, int childIndex)
        {
            LastParentPassed = reference;
            LastIndexPassed = childIndex;
            return ChildToReturn!;
        }
    }

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

    [Fact]
    public void GetChildrenCount_CallsWrapper_WhenParentIsDependencyObject()
    {
        // Arrange
        var mockHelper = new MockVisualTreeHelper { ChildCountToReturn = 5 };
        var service = new WinUIVisualTreeService(mockHelper);
        var validParent = new Microsoft.UI.Xaml.Controls.Grid();

        // Act
        var result = service.GetChildrenCount(validParent);

        // Assert
        Assert.Equal(5, result);
        Assert.Same(validParent, mockHelper.LastParentPassed);
    }

    [Fact]
    public void GetChild_CallsWrapper_WhenParentIsDependencyObject()
    {
        // Arrange
        var mockHelper = new MockVisualTreeHelper();
        var expectedChild = new Microsoft.UI.Xaml.Controls.Button();
        mockHelper.ChildToReturn = expectedChild;

        var service = new WinUIVisualTreeService(mockHelper);
        var validParent = new Microsoft.UI.Xaml.Controls.Grid();
        var index = 2;

        // Act
        var result = service.GetChild(validParent, index);

        // Assert
        Assert.Same(expectedChild, result);
        Assert.Same(validParent, mockHelper.LastParentPassed);
        Assert.Equal(index, mockHelper.LastIndexPassed);
    }
}
