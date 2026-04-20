using System;
using Launchbox.Services;
using Xunit;

namespace Launchbox.Tests;

public class IconCacheEntryTests
{
    [Fact]
    public void Constructor_AssignsPropertiesCorrectly()
    {
        // Arrange
        var content = new byte[] { 1, 2, 3 };
        var shortcutTime = new DateTime(2023, 1, 1);
        var pngTime = new DateTime(2023, 1, 2);
        var icoTime = new DateTime(2023, 1, 3);

        // Act
        var entry = new IconCacheEntry(content, shortcutTime, pngTime, icoTime);

        // Assert
        Assert.Same(content, entry.Content);
        Assert.Equal(shortcutTime, entry.ShortcutTime);
        Assert.Equal(pngTime, entry.PngTime);
        Assert.Equal(icoTime, entry.IcoTime);
    }

    [Fact]
    public void Constructor_AllowsNullContent()
    {
        // Arrange
        var shortcutTime = new DateTime(2023, 1, 1);
        var pngTime = new DateTime(2023, 1, 2);
        var icoTime = new DateTime(2023, 1, 3);

        // Act
        var entry = new IconCacheEntry(null, shortcutTime, pngTime, icoTime);

        // Assert
        Assert.Null(entry.Content);
        Assert.Equal(shortcutTime, entry.ShortcutTime);
        Assert.Equal(pngTime, entry.PngTime);
        Assert.Equal(icoTime, entry.IcoTime);
    }

    [Fact]
    public void Equality_RecordsWithSameValuesAreEqual()
    {
        // Arrange
        var content = new byte[] { 1, 2, 3 };
        var shortcutTime = new DateTime(2023, 1, 1);
        var pngTime = new DateTime(2023, 1, 2);
        var icoTime = new DateTime(2023, 1, 3);

        var entry1 = new IconCacheEntry(content, shortcutTime, pngTime, icoTime);
        var entry2 = new IconCacheEntry(content, shortcutTime, pngTime, icoTime);

        // Act & Assert
        Assert.Equal(entry1, entry2);
        Assert.True(entry1 == entry2);
    }

    [Fact]
    public void Inequality_DifferentArrayReference_RecordsAreNotEqual()
    {
        // Arrange
        var content1 = new byte[] { 1, 2, 3 };
        var content2 = new byte[] { 1, 2, 3 }; // Different reference, same contents
        var shortcutTime = new DateTime(2023, 1, 1);
        var pngTime = new DateTime(2023, 1, 2);
        var icoTime = new DateTime(2023, 1, 3);

        var entry1 = new IconCacheEntry(content1, shortcutTime, pngTime, icoTime);
        var entry2 = new IconCacheEntry(content2, shortcutTime, pngTime, icoTime);

        // Act & Assert
        // Record equality uses ReferenceEquals for arrays, so these should be different
        Assert.NotEqual(entry1, entry2);
        Assert.True(entry1 != entry2);
    }

    [Fact]
    public void Inequality_DifferentTime_RecordsAreNotEqual()
    {
        // Arrange
        var content = new byte[] { 1, 2, 3 };
        var shortcutTime = new DateTime(2023, 1, 1);
        var pngTime = new DateTime(2023, 1, 2);
        var icoTime = new DateTime(2023, 1, 3);

        var entry1 = new IconCacheEntry(content, shortcutTime, pngTime, icoTime);
        var entry2 = new IconCacheEntry(content, shortcutTime.AddDays(1), pngTime, icoTime);

        // Act & Assert
        Assert.NotEqual(entry1, entry2);
        Assert.True(entry1 != entry2);
    }
}
