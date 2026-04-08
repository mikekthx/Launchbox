using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace BenchmarkRunner;

public class AppItem
{
    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string FolderPath { get; set; } = string.Empty;
    public string FolderLabel { get; set; } = string.Empty;
}

class Program
{
    static void Main(string[] args)
    {
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
            customOrders[folder] = customOrder.OrderBy(x => random.Next()).ToList();
        }

        // Warmup
        for (int i = 0; i < 10; i++)
        {
            OriginalOrderAppItems(items, customOrders);
            OptimizedOrderAppItems(items, customOrders);
            Optimized2OrderAppItems(items, customOrders);
        }

        var sw = Stopwatch.StartNew();
        for (int i = 0; i < 100; i++)
            OriginalOrderAppItems(items, customOrders);
        sw.Stop();
        var originalMs = sw.ElapsedMilliseconds;

        sw.Restart();
        for (int i = 0; i < 100; i++)
            OptimizedOrderAppItems(items, customOrders);
        sw.Stop();
        var optimizedMs = sw.ElapsedMilliseconds;

        sw.Restart();
        for (int i = 0; i < 100; i++)
            Optimized2OrderAppItems(items, customOrders);
        sw.Stop();
        var optimized2Ms = sw.ElapsedMilliseconds;

        Console.WriteLine($"Original: {originalMs}ms");
        Console.WriteLine($"Optimized 1: {optimizedMs}ms");
        Console.WriteLine($"Optimized 2: {optimized2Ms}ms");

        var res1 = OriginalOrderAppItems(items, customOrders);
        var res2 = OptimizedOrderAppItems(items, customOrders);
        var res3 = Optimized2OrderAppItems(items, customOrders);

        if (res1.Count != res2.Count || res1.Count != res3.Count) throw new Exception("Count mismatch");
        for (int i = 0; i < res1.Count; i++)
        {
            if (res1[i].Path != res2[i].Path) throw new Exception($"Path mismatch at {i} for Opt1");
            if (res1[i].Path != res3[i].Path) throw new Exception($"Path mismatch at {i} for Opt2");
        }
    }

    static List<AppItem> OriginalOrderAppItems(List<AppItem> items, Dictionary<string, List<string>> orders)
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

    static List<AppItem> OptimizedOrderAppItems(List<AppItem> items, Dictionary<string, List<string>> orders)
    {
        return items
            .GroupBy(a => a.FolderPath)
            .SelectMany(g =>
            {
                var customOrder = orders[g.Key];
                if (customOrder.Count == 0)
                    return (IEnumerable<AppItem>)g.OrderBy(a => a.Name);

                var orderIndex = new Dictionary<string, int>(customOrder.Count, StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < customOrder.Count; i++)
                {
                    orderIndex.TryAdd(customOrder[i], i);
                }

                return g.OrderBy(a => orderIndex.TryGetValue(Path.GetFileName(a.Path), out int idx) ? idx : int.MaxValue)
                        .ThenBy(a => a.Name);
            })
            .ToList();
    }

    static List<AppItem> Optimized2OrderAppItems(List<AppItem> items, Dictionary<string, List<string>> orders)
    {
        return items
            .GroupBy(a => a.FolderPath)
            .SelectMany(g =>
            {
                var customOrder = orders[g.Key];
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
                    return string.Compare(a.Item.Name, b.Item.Name, StringComparison.Ordinal);
                });

                return orderedWithIndex.Select(x => x.Item);
            })
            .ToList();
    }
}
