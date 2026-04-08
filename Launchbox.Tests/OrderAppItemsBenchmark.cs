using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Launchbox.Models;
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
    public void BenchmarkOrdering()
    {
        // Generate test data
        var items = new List<AppItem>();
        var folders = new[] { "C:\\Folder1", "C:\\Folder2", "C:\\Folder3", "C:\\Folder4" };
        var random = new Random(42);

        var customOrders = new Dictionary<string, List<string>>();

        foreach (var folder in folders)
        {
            var customOrder = new List<string>();
            for (int i = 0; i < 5000; i++)
            {
                string fileName = $"App_{i}_{random.Next()}.lnk";
                items.Add(new AppItem
                {
                    Name = $"App {i}",
                    Path = Path.Combine(folder, fileName),
                    FolderPath = folder,
                    FolderLabel = "Test"
                });

                if (i % 2 == 0) // Put half in custom order
                {
                    customOrder.Add(fileName);
                }
            }
            // Shuffle custom order
            customOrders[folder] = customOrder.OrderBy(x => random.Next()).ToList();
        }

        // Run Original
        var sw = Stopwatch.StartNew();
        for (int i = 0; i < 100; i++)
        {
            var result = OriginalOrderAppItems(items, customOrders);
        }
        sw.Stop();
        var originalMs = sw.ElapsedMilliseconds;

        // Run Optimized
        sw.Restart();
        for (int i = 0; i < 100; i++)
        {
            var result = OptimizedOrderAppItems(items, customOrders);
        }
        sw.Stop();
        var optimizedMs = sw.ElapsedMilliseconds;

        _output.WriteLine($"Original: {originalMs}ms");
        _output.WriteLine($"Optimized: {optimizedMs}ms");
        _output.WriteLine($"Improvement: {originalMs - optimizedMs}ms ({(double)(originalMs - optimizedMs) / originalMs:P})");

        // Verify correctness
        var res1 = OriginalOrderAppItems(items, customOrders);
        var res2 = OptimizedOrderAppItems(items, customOrders);

        Assert.Equal(res1.Count, res2.Count);
        for(int i=0; i<res1.Count; i++) {
            Assert.Equal(res1[i].Path, res2[i].Path);
        }
    }

    private List<AppItem> OriginalOrderAppItems(List<AppItem> items, Dictionary<string, List<string>> orders)
    {
        return items
            .GroupBy(a => a.FolderPath)
            .SelectMany(g =>
            {
                var customOrder = orders[g.Key];
                if (customOrder.Count == 0)
                    return (IEnumerable<AppItem>)g.OrderBy(a => a.Name);

                var byName = g.ToDictionary(
                    a => Path.GetFileName(a.Path),
                    StringComparer.OrdinalIgnoreCase);

                var ordered = customOrder
                    .Where(name => byName.ContainsKey(name))
                    .Select(name => byName[name])
                    .ToList();
                var listed = new HashSet<string>(customOrder, StringComparer.OrdinalIgnoreCase);
                ordered.AddRange(g
                    .Where(a => !listed.Contains(Path.GetFileName(a.Path)))
                    .OrderBy(a => a.Name));
                return ordered;
            })
            .ToList();
    }

    private List<AppItem> OptimizedOrderAppItems(List<AppItem> items, Dictionary<string, List<string>> orders)
    {
        return items
            .GroupBy(a => a.FolderPath)
            .SelectMany(g =>
            {
                var customOrder = orders[g.Key];
                if (customOrder.Count == 0)
                    return (IEnumerable<AppItem>)g.OrderBy(a => a.Name);

                var orderIndex = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < customOrder.Count; i++)
                {
                    orderIndex.TryAdd(customOrder[i], i);
                }

                return g.OrderBy(a => orderIndex.TryGetValue(Path.GetFileName(a.Path), out int idx) ? idx : int.MaxValue)
                        .ThenBy(a => a.Name);
            })
            .ToList();
    }
}
