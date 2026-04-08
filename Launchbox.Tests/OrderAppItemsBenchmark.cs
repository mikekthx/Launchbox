using Launchbox.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Xunit;

namespace Launchbox.Tests;

public class OrderAppItemsBenchmark
{
    private readonly ITestOutputHelper _output;

    public OrderAppItemsBenchmark(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void OrderAppItems_SortsCustomItemsByPositionAndUnlistedItemsAlphabetically()
    {
        // Use a test instance instead of the full view model
        var items = new List<AppItem>
        {
            new AppItem { Name = "Zebra", Path = "C:\\Folder1\\zebra.lnk", FolderPath = "C:\\Folder1", FolderLabel = "Test" },
            new AppItem { Name = "Apple", Path = "C:\\Folder1\\apple.lnk", FolderPath = "C:\\Folder1", FolderLabel = "Test" },
            new AppItem { Name = "Mango", Path = "C:\\Folder1\\mango.lnk", FolderPath = "C:\\Folder1", FolderLabel = "Test" },
            new AppItem { Name = "Banana", Path = "C:\\Folder1\\banana.lnk", FolderPath = "C:\\Folder1", FolderLabel = "Test" },
            new AppItem { Name = "Cherry", Path = "C:\\Folder1\\cherry.lnk", FolderPath = "C:\\Folder1", FolderLabel = "Test" }
        };

        var customOrders = new Dictionary<string, List<string>>
        {
            { "C:\\Folder1", new List<string> { "mango.lnk", "apple.lnk" } }
        };

        var result = OrderAppItems(items, customOrders);

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
        var items = new List<AppItem>
        {
            new AppItem { Name = "Zebra", Path = "C:\\Folder1\\zebra.lnk", FolderPath = "C:\\Folder1", FolderLabel = "Test" },
            new AppItem { Name = "Apple", Path = "C:\\Folder1\\apple.lnk", FolderPath = "C:\\Folder1", FolderLabel = "Test" },
            new AppItem { Name = "Apple Duplicate", Path = "C:\\Folder1\\apple.lnk", FolderPath = "C:\\Folder1", FolderLabel = "Test" },
        };

        var customOrders = new Dictionary<string, List<string>>
        {
            { "C:\\Folder1", new List<string> { "apple.lnk" } }
        };

        var result = OrderAppItems(items, customOrders);

        // Custom orders puts apple.lnk items first. They share index 0. Then Zebra.
        Assert.Equal(3, result.Count);
        Assert.Equal("apple.lnk", Path.GetFileName(result[0].Path));
        Assert.Equal("apple.lnk", Path.GetFileName(result[1].Path));
        Assert.Equal("Zebra", result[2].Name);
    }

    private List<AppItem> OrderAppItems(List<AppItem> items, Dictionary<string, List<string>> orders)
    {
        return items
            .GroupBy(a => a.FolderPath)
            .SelectMany(g =>
            {
                var customOrder = orders.TryGetValue(g.Key, out var order) ? order : new List<string>();
                if (customOrder.Count == 0)
                    return (IEnumerable<AppItem>)g.OrderBy(a => a.Name);

                var orderIndex = new Dictionary<string, int>(customOrder.Count, StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < customOrder.Count; i++)
                {
                    orderIndex.TryAdd(customOrder[i], i);
                }

                var orderedWithIndex = g.Select(a => (Item: a, Index: orderIndex.TryGetValue(Path.GetFileName(a.Path), out int idx) ? idx : int.MaxValue))
                                        .ToList();
                orderedWithIndex.Sort((a, b) =>
                {
                    int cmp = a.Index.CompareTo(b.Index);
                    if (cmp != 0) return cmp;
                    return StringComparer.CurrentCulture.Compare(a.Item.Name, b.Item.Name);
                });

                return orderedWithIndex.Select(x => x.Item);
            })
            .ToList();
    }
}
