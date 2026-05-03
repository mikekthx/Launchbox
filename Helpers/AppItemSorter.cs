using Launchbox.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Launchbox.Helpers;

internal static class AppItemSorter
{
    public static List<AppItem> OrderAppItems(List<AppItem> items, IReadOnlyDictionary<string, IReadOnlyList<string>> customOrders)
    {
        return items
            .GroupBy(a => a.FolderPath)
            .SelectMany(g =>
            {
                if (!customOrders.TryGetValue(g.Key, out var customOrder) || customOrder.Count == 0)
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
