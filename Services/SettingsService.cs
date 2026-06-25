using CommunityToolkit.Mvvm.ComponentModel;
using Launchbox.Helpers;
using Launchbox.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace Launchbox.Services;

public class SettingsService : ObservableObject
{
    private readonly ISettingsStore _store;
    private readonly IStartupService _startupService;
    private readonly ShortcutFolderManager _folderManager;

    public SettingsService(ISettingsStore store, IStartupService startupService, ShortcutFolderManager folderManager)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _startupService = startupService ?? throw new ArgumentNullException(nameof(startupService));
        _folderManager = folderManager ?? throw new ArgumentNullException(nameof(folderManager));
    }

    /// <summary>
    /// Gets the path to the primary shortcut folder.
    /// Delegates to the folder manager, returning the first folder's expanded path or the default Desktop\Shortcuts path.
    /// </summary>
    public string ShortcutsPath
    {
        get
        {
            var folders = _folderManager.GetFolders();
            var first = folders.OrderBy(f => f.Order).FirstOrDefault();
            if (first != null)
            {
                var expandedPath = first.ExpandedPath;
                if (!PathSecurity.IsUnsafePath(expandedPath))
                {
                    return expandedPath;
                }
                Trace.WriteLine($"Ignored unsafe ShortcutsPath from settings: {PathSecurity.RedactPath(expandedPath)}");
            }
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "Shortcuts");
        }
    }

    public IReadOnlyList<ShortcutFolder> GetShortcutFolders() => _folderManager.GetFolders();

    /// <summary>
    /// Adds a new shortcut folder and unconditionally triggers a UI update.
    /// </summary>
    /// <returns><c>true</c> if the folder was added successfully; otherwise, <c>false</c>.</returns>
    public bool AddShortcutFolder(string path, string? label = null)
    {
        bool result = _folderManager.AddFolder(path, label);
        OnPropertyChanged(SHORTCUT_FOLDERS_KEY);
        return result;
    }

    /// <summary>
    /// Removes a shortcut folder by its display order and unconditionally triggers a UI update.
    /// </summary>
    /// <returns><c>true</c> if the folder was removed successfully; otherwise, <c>false</c>.</returns>
    public bool RemoveShortcutFolder(int order)
    {
        bool result = _folderManager.RemoveFolder(order);
        OnPropertyChanged(SHORTCUT_FOLDERS_KEY);
        return result;
    }

    /// <summary>
    /// Moves a shortcut folder to a new position in the display order and unconditionally triggers a UI update.
    /// </summary>
    /// <returns><c>true</c> if the folder was reordered successfully; otherwise, <c>false</c>.</returns>
    public bool ReorderShortcutFolder(int fromOrder, int toOrder)
    {
        bool result = _folderManager.ReorderFolder(fromOrder, toOrder);
        OnPropertyChanged(SHORTCUT_FOLDERS_KEY);
        return result;
    }

    /// <summary>
    /// Renames a shortcut folder and unconditionally triggers a UI update.
    /// </summary>
    /// <returns><c>true</c> if the folder was renamed successfully; otherwise, <c>false</c>.</returns>
    public bool RenameShortcutFolder(int order, string newLabel)
    {
        bool result = _folderManager.RenameFolder(order, newLabel);
        OnPropertyChanged(SHORTCUT_FOLDERS_KEY);
        return result;
    }

    /// <summary>
    /// Sets the exact canonical order of all shortcut folders and unconditionally triggers a UI update.
    /// </summary>
    /// <returns><c>true</c> if the sequence was updated successfully; otherwise, <c>false</c>.</returns>
    public bool SetShortcutFolderSequence(IReadOnlyList<string> orderedPaths)
    {
        bool result = _folderManager.SetFolderSequence(orderedPaths);
        OnPropertyChanged(SHORTCUT_FOLDERS_KEY);
        return result;
    }

    internal const string ITEM_ORDERS_KEY = "ShortcutItemOrders";
    internal const string SHORTCUT_FOLDERS_KEY = "ShortcutFolders";
    // 7KB safety margin under the 8KB LocalSettings per-value limit
    private const int MAX_ITEM_ORDERS_BYTES = 7168;

    /// <summary>
    /// Returns all custom display orders for all folders in a single call.
    /// </summary>
    public IReadOnlyDictionary<string, IReadOnlyList<string>> GetItemOrders()
    {
        var dict = DeserializeItemOrders();
        return dict.ToDictionary(
            kvp => kvp.Key,
            kvp => (IReadOnlyList<string>)kvp.Value,
            StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Returns the custom display order for shortcuts in <paramref name="folderPath"/>
    /// as an ordered list of file names (e.g. "Notepad.lnk").
    /// Returns an empty list if no custom order has been saved.
    /// </summary>
    public IReadOnlyList<string> GetItemOrder(string folderPath)
    {
        var dict = DeserializeItemOrders();
        if (dict.TryGetValue(folderPath, out var order))
        {
            return order;
        }
        return [];
    }

    /// <summary>
    /// Persists a custom display order for shortcuts in <paramref name="folderPath"/>.
    /// Orders for other folders are preserved.
    /// </summary>
    public bool SetItemOrder(string folderPath, IReadOnlyList<string> orderedNames)
    {
        var dict = DeserializeItemOrders();
        dict[folderPath] = [.. orderedNames];
        return PersistItemOrders(dict);
    }

    /// <summary>
    /// Persists custom display orders for all folders in one store write.
    /// Orders for folders not present in <paramref name="orders"/> are discarded.
    /// </summary>
    public bool SetItemOrders(Dictionary<string, List<string>> orders)
    {
        return PersistItemOrders(orders);
    }

    /// <summary>
    /// Merges <paramref name="updates"/> into the existing order store in one write.
    /// Folders not present in <paramref name="updates"/> retain their saved order.
    /// Use this instead of <see cref="SetItemOrders"/> when only a subset of folders
    /// are currently loaded (e.g. some folders are unavailable).
    /// </summary>
    public bool MergeItemOrders(Dictionary<string, List<string>> updates)
    {
        var existing = DeserializeItemOrders();
        foreach (var (key, value) in updates)
            existing[key] = value;
        return PersistItemOrders(existing);
    }

    private Dictionary<string, List<string>> DeserializeItemOrders()
    {
        if (!_store.TryGetValue(ITEM_ORDERS_KEY, out var val) || val is not string json)
            return new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        try
        {
            var deserialized = JsonSerializer.Deserialize<Dictionary<string, List<string>>>(json);
            if (deserialized == null) return new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            // Windows paths are case-insensitive; rebuild with OrdinalIgnoreCase so a casing
            // mismatch between stored keys and lookup keys never silently loses order data.
            return new Dictionary<string, List<string>>(deserialized, StringComparer.OrdinalIgnoreCase);
        }
        catch (JsonException ex)
        {
            Trace.WriteLine($"Corrupt {ITEM_ORDERS_KEY} JSON: {PathSecurity.GetSafeExceptionMessage(ex)}");
            return new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private bool PersistItemOrders(Dictionary<string, List<string>> orders)
    {
        var json = JsonSerializer.Serialize(orders);
        if (System.Text.Encoding.UTF8.GetByteCount(json) > MAX_ITEM_ORDERS_BYTES)
        {
            Trace.WriteLine($"Failed to persist {ITEM_ORDERS_KEY}: serialized size exceeds {MAX_ITEM_ORDERS_BYTES} bytes");
            OnPropertyChanged(ITEM_ORDERS_KEY);
            return false;
        }

        bool result = _store.SetValue(ITEM_ORDERS_KEY, json);
        if (!result)
        {
            Trace.WriteLine($"Failed to persist {ITEM_ORDERS_KEY}: store write returned false");
        }

        OnPropertyChanged(ITEM_ORDERS_KEY);
        return result;
    }

    public FolderViewMode FolderViewMode
    {
        get
        {
            if (_store.TryGetValue(nameof(FolderViewMode), out var val) && val is string s
                && Enum.TryParse<FolderViewMode>(s, out var mode))
            {
                return mode;
            }
            return FolderViewMode.Merged;
        }
        set
        {
            if (FolderViewMode == value) return;
            if (!_store.SetValue(nameof(FolderViewMode), value.ToString()))
                Trace.WriteLine($"Failed to persist {nameof(FolderViewMode)}: store write returned false");
            OnPropertyChanged();
        }
    }

    public bool CollapsibleGroups
    {
        get
        {
            if (_store.TryGetValue(nameof(CollapsibleGroups), out var val) && val is bool b)
            {
                return b;
            }
            return true;
        }
        set
        {
            if (CollapsibleGroups == value) return;
            if (!_store.SetValue(nameof(CollapsibleGroups), value))
                Trace.WriteLine($"Failed to persist {nameof(CollapsibleGroups)}: store write returned false");
            OnPropertyChanged();
        }
    }

    // Valid modifier flags: any combination of MOD_ALT (1), MOD_CONTROL (2), MOD_SHIFT (4), MOD_WIN (8)
    private const int VALID_MODIFIER_MASK = Constants.MOD_ALT | Constants.MOD_CONTROL | Constants.MOD_SHIFT | Constants.MOD_WIN;

    public int HotkeyModifiers
    {
        get
        {
            if (_store.TryGetValue(nameof(HotkeyModifiers), out var val) && val is int mod
                && mod > 0 && (mod & ~VALID_MODIFIER_MASK) == 0)
            {
                return mod;
            }
            return Constants.MOD_ALT;
        }
        set
        {
            if (HotkeyModifiers == value) return;
            if (!_store.SetValue(nameof(HotkeyModifiers), value))
                Trace.WriteLine($"Failed to persist {nameof(HotkeyModifiers)}: store write returned false");
            OnPropertyChanged();
        }
    }

    public int HotkeyKey
    {
        get
        {
            if (_store.TryGetValue(nameof(HotkeyKey), out var val) && val is int key
                && key >= Constants.MIN_VIRTUAL_KEY && key <= Constants.MAX_VIRTUAL_KEY)
            {
                return key;
            }
            return Constants.VK_S;
        }
        set
        {
            if (HotkeyKey == value) return;
            if (!_store.SetValue(nameof(HotkeyKey), value))
                Trace.WriteLine($"Failed to persist {nameof(HotkeyKey)}: store write returned false");
            OnPropertyChanged();
        }
    }

    public GridSize GridSize
    {
        get
        {
            if (_store.TryGetValue(nameof(GridSize), out var val) && val is string s
                && Enum.TryParse<GridSize>(s, ignoreCase: true, out var parsed))
            {
                return parsed;
            }
            return GridSize.Medium;
        }
        set
        {
            if (GridSize == value) return;
            if (!_store.SetValue(nameof(GridSize), value.ToString()))
                Trace.WriteLine($"Failed to persist {nameof(GridSize)}: store write returned false");
            OnPropertyChanged();
        }
    }

    public bool KeepCentered
    {
        get
        {
            if (_store.TryGetValue(nameof(KeepCentered), out var val) && val is bool b)
            {
                return b;
            }
            return false;
        }
        set
        {
            if (KeepCentered == value) return;
            if (!_store.SetValue(nameof(KeepCentered), value))
                Trace.WriteLine($"Failed to persist {nameof(KeepCentered)}: store write returned false");
            OnPropertyChanged();
        }
    }

    private bool _isRunAtStartup;
    public bool IsRunAtStartup
    {
        get => _isRunAtStartup;
        private set => SetProperty(ref _isRunAtStartup, value);
    }

    public async Task InitializeAsync()
    {
        try
        {
            if (_startupService.IsSupported)
            {
                IsRunAtStartup = await _startupService.IsRunAtStartupEnabledAsync();
            }
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"Failed to initialize settings (StartupService): {PathSecurity.GetSafeExceptionMessage(ex)}");
        }
    }

    public async Task SetRunAtStartupAsync(bool enable)
    {
        if (!_startupService.IsSupported) return;

        if (enable)
        {
            bool success = await _startupService.TryEnableStartupAsync();
            if (success)
            {
                IsRunAtStartup = true;
            }
            else
            {
                // Revert: OS denied the startup enable request, so reset the toggle to false.
                IsRunAtStartup = false;
            }
        }
        else
        {
            try
            {
                await _startupService.DisableStartupAsync();
                IsRunAtStartup = false;
            }
            catch (Exception ex)
            {
                // Revert: OS denied the disable request; keep the toggle as enabled.
                IsRunAtStartup = true;
                Trace.WriteLine($"Failed to disable startup: {PathSecurity.GetSafeExceptionMessage(ex)}");
            }
        }
    }

}
