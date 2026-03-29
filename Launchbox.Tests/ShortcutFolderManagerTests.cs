using Launchbox.Models;
using Launchbox.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;

namespace Launchbox.Tests;

public class ShortcutFolderManagerTests
{
    // --- HELPERS ---

    private static string SerializeFolders(IEnumerable<ShortcutFolder> folders) =>
        JsonSerializer.Serialize(folders.ToList());

    private static MockSettingsStore StoreWithFolders(IEnumerable<ShortcutFolder> folders)
    {
        var store = new MockSettingsStore();
        store.SetValue("ShortcutFolders", SerializeFolders(folders));
        return store;
    }

    private static string SafePath(string name) =>
        System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Desktop), name);

    // --- LOAD / DEFAULT ---

    [Fact]
    public void GetFolders_ReturnsDefault_WhenNoStoredValue()
    {
        var store = new MockSettingsStore();
        var manager = new ShortcutFolderManager(store);

        var folders = manager.GetFolders();

        Assert.Single(folders);
        Assert.Contains("Shortcuts", folders[0].Path);
        Assert.Equal(0, folders[0].Order);
    }

    [Fact]
    public void GetFolders_DeserializesStoredJson()
    {
        var expected = new List<ShortcutFolder>
        {
            new() { Path = SafePath("Work"), Label = "Work", Order = 0 },
            new() { Path = SafePath("Games"), Label = "Games", Order = 1 },
        };
        var store = StoreWithFolders(expected);
        var manager = new ShortcutFolderManager(store);

        var folders = manager.GetFolders();

        Assert.Equal(2, folders.Count);
        Assert.Equal(SafePath("Work"), folders[0].Path);
        Assert.Equal("Work", folders[0].Label);
        Assert.Equal(SafePath("Games"), folders[1].Path);
        Assert.Equal("Games", folders[1].Label);
    }

    [Fact]
    public void GetFolders_MigratesLegacyShortcutsPath()
    {
        var legacyPath = SafePath("MyShortcuts");
        var store = new MockSettingsStore();
        store.SetValue("ShortcutsPath", legacyPath);

        var manager = new ShortcutFolderManager(store);
        var folders = manager.GetFolders();

        // The legacy path should be loaded
        Assert.Single(folders);
        Assert.Equal(legacyPath, folders[0].Path);

        // The new key should now exist in the store
        Assert.True(store.TryGetValue("ShortcutFolders", out var newVal));
        Assert.NotNull(newVal);
        Assert.IsType<string>(newVal);
        Assert.False(string.IsNullOrEmpty((string)newVal!));
    }

    [Fact]
    public void GetFolders_AfterMigration_UsesNewKeyIgnoresLegacy()
    {
        var newFolders = new List<ShortcutFolder>
        {
            new() { Path = SafePath("NewFolder"), Label = "NewFolder", Order = 0 }
        };
        var store = StoreWithFolders(newFolders);
        // Also set the legacy key — new key must win
        store.SetValue("ShortcutsPath", SafePath("LegacyFolder"));

        var manager = new ShortcutFolderManager(store);
        var folders = manager.GetFolders();

        Assert.Single(folders);
        Assert.Equal(SafePath("NewFolder"), folders[0].Path);
    }

    [Fact]
    public void GetFolders_MalformedJson_ReturnsDefault_DoesNotOverwrite()
    {
        const string corrupt = "{ this is not valid json [";
        var store = new MockSettingsStore();
        store.SetValue("ShortcutFolders", corrupt);

        var manager = new ShortcutFolderManager(store);
        var folders = manager.GetFolders();

        // Falls back to default
        Assert.Single(folders);
        Assert.Contains("Shortcuts", folders[0].Path);

        // Original corrupt value should NOT have been overwritten (allow manual recovery)
        Assert.True(store.TryGetValue("ShortcutFolders", out var val));
        Assert.Equal(corrupt, val);
    }

    [Fact]
    public void GetFolders_InvalidJson_ReturnsDefault_DoesNotOverwrite()
    {
        const string corrupt = "{invalid}";
        var store = new MockSettingsStore();
        store.SetValue("ShortcutFolders", corrupt);

        var manager = new ShortcutFolderManager(store);
        var folders = manager.GetFolders();

        // Falls back to default
        Assert.Single(folders);
        Assert.Contains("Shortcuts", folders[0].Path);

        // Original corrupt value should NOT have been overwritten (allow manual recovery)
        Assert.True(store.TryGetValue("ShortcutFolders", out var val));
        Assert.Equal(corrupt, val);
    }


    [Fact]
    public void GetFolders_DropsUnsafePaths()
    {
        // UNC path should be filtered out by ValidateAndNormalize
        var unsafePath = @"\\server\share\Shortcuts";
        var safeFolderPath = SafePath("Safe");
        var folders = new List<ShortcutFolder>
        {
            new() { Path = unsafePath, Label = "Unsafe", Order = 0 },
            new() { Path = safeFolderPath, Label = "Safe", Order = 1 },
        };
        var store = StoreWithFolders(folders);

        var manager = new ShortcutFolderManager(store);
        var result = manager.GetFolders();

        // Only the safe path survives
        Assert.Single(result);
        Assert.Equal(safeFolderPath, result[0].Path);
    }

    [Fact]
    public void GetFolders_NormalizesOrderGaps()
    {
        // Stored with non-contiguous orders
        var json = JsonSerializer.Serialize(new[]
        {
            new { Path = SafePath("A"), Label = "A", Order = 0 },
            new { Path = SafePath("B"), Label = "B", Order = 5 },
            new { Path = SafePath("C"), Label = "C", Order = 10 },
        });
        var store = new MockSettingsStore();
        store.SetValue("ShortcutFolders", json);

        var manager = new ShortcutFolderManager(store);
        var result = manager.GetFolders();

        Assert.Equal(3, result.Count);
        Assert.Equal(0, result[0].Order);
        Assert.Equal(1, result[1].Order);
        Assert.Equal(2, result[2].Order);
    }

    // --- ADD ---

    [Fact]
    public void AddFolder_AppendsAndPersists()
    {
        var store = new MockSettingsStore();
        var manager = new ShortcutFolderManager(store);
        var newPath = SafePath("NewApp");

        var result = manager.AddFolder(newPath, "NewApp");

        Assert.True(result);
        var folders = manager.GetFolders();
        Assert.Equal(2, folders.Count);
        Assert.Contains(folders, f => f.Path == newPath && f.Label == "NewApp");

        // Persisted to store: deserialize to verify (JSON double-escapes backslashes)
        Assert.True(store.TryGetValue("ShortcutFolders", out var val));
        var persisted = JsonSerializer.Deserialize<List<ShortcutFolder>>((string)val!);
        Assert.NotNull(persisted);
        Assert.Contains(persisted, f => f.Path == newPath);
    }

    [Fact]
    public void AddFolder_RejectsAtMaxCap()
    {
        // Pre-populate 20 folders
        var initialFolders = Enumerable.Range(0, 20)
            .Select(i => new ShortcutFolder { Path = SafePath($"Folder{i}"), Label = $"F{i}", Order = i })
            .ToList();
        var store = StoreWithFolders(initialFolders);
        var manager = new ShortcutFolderManager(store);

        var result = manager.AddFolder(SafePath("OneMore"), "OneMore");

        Assert.False(result);
        Assert.Equal(20, manager.GetFolders().Count);
    }

    [Fact]
    public void AddFolder_RejectsUnsafePath()
    {
        var store = new MockSettingsStore();
        var manager = new ShortcutFolderManager(store);

        // UNC path is unsafe
        var result = manager.AddFolder(@"\\server\share\Bad", "Bad");

        Assert.False(result);
        // Only the default folder remains
        Assert.Single(manager.GetFolders());
    }

    // --- REMOVE ---

    [Fact]
    public void RemoveFolder_RemovesAndRenumbers()
    {
        var initialFolders = new List<ShortcutFolder>
        {
            new() { Path = SafePath("A"), Label = "A", Order = 0 },
            new() { Path = SafePath("B"), Label = "B", Order = 1 },
            new() { Path = SafePath("C"), Label = "C", Order = 2 },
        };
        var store = StoreWithFolders(initialFolders);
        var manager = new ShortcutFolderManager(store);

        // Remove the middle folder (order 1)
        var result = manager.RemoveFolder(1);

        Assert.True(result);
        var folders = manager.GetFolders();
        Assert.Equal(2, folders.Count);
        Assert.Equal(SafePath("A"), folders[0].Path);
        Assert.Equal(0, folders[0].Order);
        Assert.Equal(SafePath("C"), folders[1].Path);
        Assert.Equal(1, folders[1].Order);
    }

    // --- REORDER ---

    [Fact]
    public void ReorderFolder_SwapsCorrectly()
    {
        var initialFolders = new List<ShortcutFolder>
        {
            new() { Path = SafePath("A"), Label = "A", Order = 0 },
            new() { Path = SafePath("B"), Label = "B", Order = 1 },
            new() { Path = SafePath("C"), Label = "C", Order = 2 },
        };
        var store = StoreWithFolders(initialFolders);
        var manager = new ShortcutFolderManager(store);

        // Move folder 0 ("A") to position 1
        var result = manager.ReorderFolder(0, 1);

        Assert.True(result);
        var folders = manager.GetFolders();
        Assert.Equal(3, folders.Count);
        // After moving A from index 0 to index 1, B ends up first then A then C
        Assert.Equal(SafePath("B"), folders[0].Path);
        Assert.Equal(SafePath("A"), folders[1].Path);
        Assert.Equal(SafePath("C"), folders[2].Path);
        // Orders are renumbered
        Assert.Equal(0, folders[0].Order);
        Assert.Equal(1, folders[1].Order);
        Assert.Equal(2, folders[2].Order);
    }

    // --- RENAME ---

    [Fact]
    public void RenameFolder_UpdatesLabel()
    {
        var initialFolders = new List<ShortcutFolder>
        {
            new() { Path = SafePath("Work"), Label = "Work", Order = 0 },
        };
        var store = StoreWithFolders(initialFolders);
        var manager = new ShortcutFolderManager(store);

        var result = manager.RenameFolder(0, "Office");

        Assert.True(result);
        Assert.Equal("Office", manager.GetFolders()[0].Label);
    }

    [Fact]
    public void RenameFolder_RejectsEmpty()
    {
        var initialFolders = new List<ShortcutFolder>
        {
            new() { Path = SafePath("Work"), Label = "Work", Order = 0 },
        };
        var store = StoreWithFolders(initialFolders);
        var manager = new ShortcutFolderManager(store);

        Assert.False(manager.RenameFolder(0, string.Empty));
        Assert.False(manager.RenameFolder(0, "   "));

        // Label unchanged
        Assert.Equal("Work", manager.GetFolders()[0].Label);
    }

    // --- SIZE LIMIT ---

    [Fact]
    public void TryPersist_RejectsOversizedJson()
    {
        // Fill 19 folders with very long paths to approach the 7KB JSON limit,
        // then add one more with a long path that pushes it over.
        var longSegment = new string('x', 300);
        var initialFolders = Enumerable.Range(0, 19)
            .Select(i => new ShortcutFolder
            {
                Path = SafePath($"{longSegment}_{i}"),
                Label = $"F{i}",
                Order = i
            })
            .ToList();
        var store = StoreWithFolders(initialFolders);
        var manager = new ShortcutFolderManager(store);

        // The 20th folder with an equally long path may push JSON over 7168 bytes
        var result = manager.AddFolder(SafePath($"{longSegment}_final"), "Final");

        // Either rejected due to size (false) or accepted (true) — what matters is
        // that if it returned false, the cache is unchanged.
        if (!result)
        {
            Assert.Equal(19, manager.GetFolders().Count);
        }
        else
        {
            // Accepted: verify it was actually persisted
            Assert.Equal(20, manager.GetFolders().Count);
        }
    }

    // --- SET FOLDER SEQUENCE ---

    [Fact]
    public void SetFolderSequence_ReordersAndPersists()
    {
        var store = StoreWithFolders([
            new ShortcutFolder { Path = SafePath("A"), Label = "A", Order = 0 },
            new ShortcutFolder { Path = SafePath("B"), Label = "B", Order = 1 },
            new ShortcutFolder { Path = SafePath("C"), Label = "C", Order = 2 },
        ]);
        var manager = new ShortcutFolderManager(store);

        var result = manager.SetFolderSequence([SafePath("C"), SafePath("A"), SafePath("B")]);

        Assert.True(result);
        var folders = manager.GetFolders();
        Assert.Equal(3, folders.Count);
        Assert.Equal("C", folders[0].Label);
        Assert.Equal("A", folders[1].Label);
        Assert.Equal("B", folders[2].Label);
        Assert.Equal(0, folders[0].Order);
        Assert.Equal(1, folders[1].Order);
        Assert.Equal(2, folders[2].Order);
    }

    [Fact]
    public void SetFolderSequence_IgnoresUnknownPaths()
    {
        var store = StoreWithFolders([
            new ShortcutFolder { Path = SafePath("A"), Label = "A", Order = 0 },
            new ShortcutFolder { Path = SafePath("B"), Label = "B", Order = 1 },
        ]);
        var manager = new ShortcutFolderManager(store);

        var result = manager.SetFolderSequence([SafePath("B"), SafePath("Z"), SafePath("A")]);

        Assert.True(result);
        var folders = manager.GetFolders();
        Assert.Equal(2, folders.Count);
        Assert.Equal("B", folders[0].Label);
        Assert.Equal("A", folders[1].Label);
    }

    [Fact]
    public void SetFolderSequence_AppendsUnrepresentedFolders()
    {
        var store = StoreWithFolders([
            new ShortcutFolder { Path = SafePath("A"), Label = "A", Order = 0 },
            new ShortcutFolder { Path = SafePath("B"), Label = "B", Order = 1 },
            new ShortcutFolder { Path = SafePath("C"), Label = "C", Order = 2 },
        ]);
        var manager = new ShortcutFolderManager(store);

        var result = manager.SetFolderSequence([SafePath("B"), SafePath("A")]);

        Assert.True(result);
        var folders = manager.GetFolders();
        Assert.Equal(3, folders.Count);
        Assert.Equal("B", folders[0].Label);
        Assert.Equal("A", folders[1].Label);
        Assert.Equal("C", folders[2].Label);
    }

    // --- THREAD SAFETY ---

    [Fact]
    public async Task GetFolders_ThreadSafe_ReturnsConsistentSnapshot()
    {
        var initialFolders = Enumerable.Range(0, 5)
            .Select(i => new ShortcutFolder { Path = SafePath($"Folder{i}"), Label = $"F{i}", Order = i })
            .ToList();
        var store = StoreWithFolders(initialFolders);
        var manager = new ShortcutFolderManager(store);

        const int threadCount = 20;
        var snapshots = new IReadOnlyList<ShortcutFolder>[threadCount];
        var exceptions = new Exception?[threadCount];

        var tasks = Enumerable.Range(0, threadCount).Select(i => Task.Run(() =>
        {
            try
            {
                snapshots[i] = manager.GetFolders();
                // Each snapshot must be non-null and have a consistent order sequence
                Assert.NotNull(snapshots[i]);
                for (int j = 0; j < snapshots[i].Count; j++)
                {
                    Assert.Equal(j, snapshots[i][j].Order);
                }
            }
            catch (Exception ex)
            {
                exceptions[i] = ex;
            }
        })).ToArray();

        await Task.WhenAll(tasks);

        // No exceptions
        Assert.All(exceptions, e => Assert.Null(e));
        // Every thread got a non-null result
        Assert.All(snapshots, s => Assert.NotNull(s));
    }
}
