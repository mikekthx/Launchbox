using Launchbox.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Launchbox.Helpers;

internal static class AppItemSorter
{
    /// <summary>
    /// Groups and sorts a list of <see cref="AppItem"/> instances by their source folder.
    /// Items are sorted based on a provided custom order configuration (retrieved per folder).
    /// Items not present in the custom order fall back to being sorted alphabetically.
    /// </summary>
    /// <param name="items">The flat list of items to sort.</param>
    /// <param name="customOrders">A dictionary mapping each folder path to the ordered list of filenames for that folder.</param>
    /// <returns>A new sorted list containing all original items.</returns>
    public static List<AppItem> OrderAppItems(List<AppItem> items, IReadOnlyDictionary<string, IReadOnlyList<string>> customOrders)
    {
        var sortedItems = new List<AppItem>(items.Count);

        foreach (var group in items.GroupBy(a => a.FolderPath))
        {
            if (!customOrders.TryGetValue(group.Key, out var customOrder) || customOrder.Count == 0)
            {
                sortedItems.AddRange(group.OrderBy(a => a.Name));
                continue;
            }

            var orderIndex = new Dictionary<string, int>(customOrder.Count, StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < customOrder.Count; i++)
            {
                orderIndex.TryAdd(customOrder[i], i);
            }

            var orderedWithIndex = group.Select(a =>
            {
                var fileName = Path.GetFileName(a.Path);
                var sortIndex = orderIndex.TryGetValue(fileName, out int idx) ? idx : int.MaxValue;
                return (Item: a, Index: sortIndex);
            }).ToList();

            orderedWithIndex.Sort((a, b) =>
            {
                int cmp = a.Index.CompareTo(b.Index);
                if (cmp != 0) return cmp;
                return StringComparer.CurrentCulture.Compare(a.Item.Name, b.Item.Name);
            });

            sortedItems.AddRange(orderedWithIndex.Select(x => x.Item));
        }

        return sortedItems;
    }
}
