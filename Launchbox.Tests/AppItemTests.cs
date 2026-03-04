using System.ComponentModel;
using Xunit;
using Launchbox.Models;

namespace Launchbox.Tests;

public class AppItemTests
{
    [Fact]
    public void Constructor_InitializesDefaultValues()
    {
        // Arrange & Act
        var item = new AppItem();

        // Assert
        Assert.Equal(string.Empty, item.Name);
        Assert.Equal(string.Empty, item.Path);
        Assert.Null(item.Icon);
    }

    [Fact]
    public void Name_SetAndGet_WorksCorrectly()
    {
        // Arrange
        var item = new AppItem();
        var expectedName = "Test App";

        // Act
        item.Name = expectedName;

        // Assert
        Assert.Equal(expectedName, item.Name);
    }

    [Fact]
    public void Path_SetAndGet_WorksCorrectly()
    {
        // Arrange
        var item = new AppItem();
        var expectedPath = @"C:\Test\App.exe";

        // Act
        item.Path = expectedPath;

        // Assert
        Assert.Equal(expectedPath, item.Path);
    }

    [Fact]
    public void Icon_SetAndGet_WorksCorrectly()
    {
        // Arrange
        var item = new AppItem();
        var expectedIcon = new object();

        // Act
        item.Icon = expectedIcon;

        // Assert
        Assert.Same(expectedIcon, item.Icon);
    }

    [Fact]
    public void Icon_Set_FiresPropertyChangedEvent()
    {
        // Arrange
        var item = new AppItem();
        var expectedIcon = new object();
        string? propertyName = null;

        item.PropertyChanged += (sender, e) =>
        {
            propertyName = e.PropertyName;
        };

        // Act
        item.Icon = expectedIcon;

        // Assert
        Assert.Equal(nameof(AppItem.Icon), propertyName);
    }

    [Fact]
    public void Icon_SetSameValue_DoesNotFirePropertyChangedEvent()
    {
        // Arrange
        var expectedIcon = new object();
        var item = new AppItem { Icon = expectedIcon };
        bool eventFired = false;

        item.PropertyChanged += (sender, e) =>
        {
            eventFired = true;
        };

        // Act
        item.Icon = expectedIcon;

        // Assert
        Assert.False(eventFired);
    }

    [Fact]
    public void NameAndPath_DoNotFirePropertyChangedEvent()
    {
        // Arrange
        var item = new AppItem();
        bool eventFired = false;

        item.PropertyChanged += (sender, e) =>
        {
            eventFired = true;
        };

        // Act
        item.Name = "New Name";
        item.Path = "New Path";

        // Assert
        Assert.False(eventFired);
    }
}
