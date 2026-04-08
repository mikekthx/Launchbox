using Launchbox.Helpers;
using Launchbox.Models;
using System.Collections.Generic;
using System.IO;
using Xunit;

namespace Launchbox.Tests;

public class AppItemSorterTests
{
    [Fact]
    public void OrderAppItems_SortsCustomItemsByPositionAndUnlistedItemsAlphabetically()
    {
        List<AppItem> items =
        [
            new() { Name = "Zebra", Path = "C:\\Folder1\\zebra.lnk", FolderPath = "C:\\Folder1", FolderLabel = "Test" },
            new() { Name = "Apple", Path = "C:\\Folder1\\apple.lnk", FolderPath = "C:\\Folder1", FolderLabel = "Test" },
            new() { Name = "Mango", Path = "C:\\Folder1\\mango.lnk", FolderPath = "C:\\Folder1", FolderLabel = "Test" },
            new() { Name = "Banana", Path = "C:\\Folder1\\banana.lnk", FolderPath = "C:\\Folder1", FolderLabel = "Test" },
            new() { Name = "Cherry", Path = "C:\\Folder1\\cherry.lnk", FolderPath = "C:\\Folder1", FolderLabel = "Test" }
        ];

        Dictionary<string, IReadOnlyList<string>> customOrders = new()
        {
            { "C:\\Folder1", ["mango.lnk", "apple.lnk"] }
        };

        var result = AppItemSorter.OrderAppItems(items, path => customOrders.TryGetValue(path, out var list) ? list : []);

        // Expected order:
        // Custom items first: Mango, Apple
        // Unlisted items alphabetically: Banana, Cherry, Zebra
        Assert.Equal(5, result.Count);
        Assert.Equal("Mango", result[0].Name);
        Assert.Equal("Apple", result[1].Name);
        Assert.Equal("Banana", result[2].Name);
        Assert.Equal("Cherry", result[3].Name);
        Assert.Equal("Zebra", result[4].Name);
    }

    [Fact]
    public void OrderAppItems_HandlesDuplicateCustomItemNamesGracefully()
    {
        List<AppItem> items =
        [
            new() { Name = "Zebra", Path = "C:\\Folder1\\zebra.lnk", FolderPath = "C:\\Folder1", FolderLabel = "Test" },
            new() { Name = "Apple", Path = "C:\\Folder1\\apple.lnk", FolderPath = "C:\\Folder1", FolderLabel = "Test" },
            new() { Name = "Apple Duplicate", Path = "C:\\Folder1\\apple.lnk", FolderPath = "C:\\Folder1", FolderLabel = "Test" },
        ];

        Dictionary<string, IReadOnlyList<string>> customOrders = new()
        {
            { "C:\\Folder1", ["apple.lnk"] }
        };

        var result = AppItemSorter.OrderAppItems(items, path => customOrders.TryGetValue(path, out var list) ? list : []);

        // Custom orders puts apple.lnk items first. They share index 0. Then Zebra.
        Assert.Equal(3, result.Count);
        Assert.Equal("apple.lnk", Path.GetFileName(result[0].Path));
        Assert.Equal("apple.lnk", Path.GetFileName(result[1].Path));
        Assert.Equal("Zebra", result[2].Name);
    }
}
