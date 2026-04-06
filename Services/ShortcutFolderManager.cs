using Launchbox.Helpers;
using Launchbox.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Launchbox.Services;

public class ShortcutFolderManager
{
    private const string FOLDERS_KEY = "ShortcutFolders";
    private const string LEGACY_KEY = "ShortcutsPath";
    private const int MAX_FOLDERS = 20;
    private const int MAX_JSON_BYTES = 7168; // 7KB safety margin under 8KB LocalSettings limit

    private readonly ISettingsStore _store;
    private readonly object _lock = new();
    // volatile for read visibility from UI thread; lock protects read-modify-write in mutations
    private volatile IReadOnlyList<ShortcutFolder> _cache;

    public ShortcutFolderManager(ISettingsStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        _store = store;
        _cache = LoadFolders();
    }

    public IReadOnlyList<ShortcutFolder> GetFolders() => _cache;

    private IReadOnlyList<ShortcutFolder> LoadFolders()
    {
        // Try reading stored JSON — if the new key exists, legacy is implicitly ignored
        if (_store.TryGetValue(FOLDERS_KEY, out var val) && val is string json && !string.IsNullOrEmpty(json))
        {
            try
            {
                var folders = JsonSerializer.Deserialize<List<ShortcutFolder>>(json);
                if (folders != null && folders.Count > 0)
                {
                    return ValidateAndNormalize(folders);
                }
            }
            catch (JsonException ex)
            {
                System.Diagnostics.Trace.WriteLine($"Corrupt ShortcutFolders JSON, using default: {PathSecurity.GetSafeExceptionMessage(ex)}");
                // Do NOT overwrite — allows manual recovery
            }
        }

        // Migration: check legacy key (new key's existence supersedes legacy — no sentinel needed)
        if (_store.TryGetValue(LEGACY_KEY, out var legacyVal) && legacyVal is string legacyPath
            && !string.IsNullOrEmpty(legacyPath))
        {
            var label = Path.GetFileName(legacyPath) ?? "Shortcuts";
            var migrated = new List<ShortcutFolder>
            {
                new() { Path = legacyPath, Label = label, Order = 0 }
            };
            _store.SetValue(FOLDERS_KEY, JsonSerializer.Serialize(migrated));
            return ValidateAndNormalize(migrated);
        }

        // Default
        var defaultPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "Shortcuts");
        return [new ShortcutFolder { Path = defaultPath, Label = "Shortcuts", Order = 0 }];
    }

    public bool AddFolder(string path, string? label = null)
    {
        if (PathSecurity.IsUnsafePath(path)) return false;

        // Also validate the expanded path to catch env vars that resolve to UNC paths
        var expandedPath = Environment.ExpandEnvironmentVariables(path);
        if (PathSecurity.IsUnsafePath(expandedPath)) return false;

        return MutateAndPersist(folders =>
        {
            if (folders.Count >= MAX_FOLDERS) return false;
            if (folders.Any(f => string.Equals(f.Path, path, StringComparison.OrdinalIgnoreCase))) return false;
            label ??= Path.GetFileName(path) ?? path;
            folders.Add(new ShortcutFolder { Path = path, Label = label, Order = folders.Count });
            return true;
        });
    }

    public bool RemoveFolder(int order)
    {
        return MutateAndPersist(folders =>
        {
            var index = folders.FindIndex(f => f.Order == order);
            if (index < 0) return false;
            folders.RemoveAt(index);
            return true;
        }, renumber: true);
    }

    public bool ReorderFolder(int fromOrder, int toOrder)
    {
        return MutateAndPersist(folders =>
        {
            var fromIndex = folders.FindIndex(f => f.Order == fromOrder);
            var toIndex = folders.FindIndex(f => f.Order == toOrder);
            if (fromIndex < 0 || toIndex < 0) return false;

            var item = folders[fromIndex];
            folders.RemoveAt(fromIndex);
            folders.Insert(toIndex, item);
            return true;
        }, renumber: true);
    }

    public bool RenameFolder(int order, string newLabel)
    {
        if (string.IsNullOrWhiteSpace(newLabel)) return false;

        return MutateAndPersist(folders =>
        {
            var index = folders.FindIndex(f => f.Order == order);
            if (index < 0) return false;
            folders[index] = folders[index] with { Label = newLabel };
            return true;
        });
    }

    /// <summary>
    /// Sets the canonical folder order to match <paramref name="orderedPaths"/>.
    /// Unknown paths are skipped; folders not present in <paramref name="orderedPaths"/>
    /// are appended at the end so they are never silently dropped.
    /// </summary>
    public bool SetFolderSequence(IReadOnlyList<string> orderedPaths)
    {
        return MutateAndPersist(folders =>
        {
            // GroupBy guards against duplicate paths (e.g. from corrupted persisted state);
            // take the first occurrence so the lookup is always safe.
            var lookup = folders
                .GroupBy(f => f.Path, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

            // Deduplicate orderedPaths so a caller passing the same path twice
            // cannot produce duplicate folder entries in the result.
            var distinctOrderedPaths = orderedPaths.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            var represented = new HashSet<string>(distinctOrderedPaths, StringComparer.OrdinalIgnoreCase);

            var ordered = distinctOrderedPaths
                .Where(p => lookup.ContainsKey(p))
                .Select(p => lookup[p])
                .ToList();

            // Append any folders not covered by orderedPaths so they are never silently lost.
            ordered.AddRange(folders.Where(f => !represented.Contains(f.Path)));

            folders.Clear();
            folders.AddRange(ordered);
            return true;
        }, renumber: true);
    }

    /// <summary>
    /// Centralizes the lock → copy → mutate → persist → cache pattern shared by all mutation methods.
    /// The <paramref name="mutate"/> delegate receives a mutable copy of the current folder list
    /// and returns true if the mutation succeeded (false aborts without persisting).
    /// </summary>
    private bool MutateAndPersist(Func<List<ShortcutFolder>, bool> mutate, bool renumber = false)
    {
        lock (_lock)
        {
            var folders = new List<ShortcutFolder>(_cache);
            if (!mutate(folders)) return false;
            return TryPersistAndCache(renumber ? Renumber(folders) : folders);
        }
    }

    private bool TryPersistAndCache(List<ShortcutFolder> folders)
    {
        // Serialize once — reuse for both size check and persistence
        var json = JsonSerializer.Serialize(folders);
        if (System.Text.Encoding.UTF8.GetByteCount(json) > MAX_JSON_BYTES)
        {
            Trace.WriteLine($"Failed to persist {FOLDERS_KEY}: serialized size exceeds {MAX_JSON_BYTES} bytes");
            return false;
        }
        if (!_store.SetValue(FOLDERS_KEY, json))
        {
            Trace.WriteLine($"Failed to persist {FOLDERS_KEY}: store write returned false");
            return false;
        }
        _cache = ValidateAndNormalize(folders);
        return true;
    }

    private static IReadOnlyList<ShortcutFolder> ValidateAndNormalize(List<ShortcutFolder> folders)
    {
        var valid = folders
            .Where(f => !string.IsNullOrEmpty(f.Path))
            // Expand and validate each path. We re-expand here directly for security checks.
            // Callers that need the expanded path later will use the lazy-cached f.ExpandedPath.
            .Where(f => !PathSecurity.IsUnsafePath(Environment.ExpandEnvironmentVariables(f.Path)))
            // Deduplicate by path so corrupted persisted state never leaks into _cache.
            .GroupBy(f => f.Path, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .ToList();

        return Renumber(valid).AsReadOnly();
    }

    private static List<ShortcutFolder> Renumber(List<ShortcutFolder> folders)
    {
        return folders.Select((f, i) => f with { Order = i }).ToList();
    }
}
