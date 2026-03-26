using CommunityToolkit.Mvvm.ComponentModel;
using Launchbox.Helpers;
using Launchbox.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
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
                var expandedPath = Environment.ExpandEnvironmentVariables(first.Path);
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

    public bool AddShortcutFolder(string path, string? label = null)
    {
        if (_folderManager.AddFolder(path, label))
        {
            OnPropertyChanged("ShortcutFolders");
            return true;
        }
        return false;
    }

    public bool RemoveShortcutFolder(int order)
    {
        if (_folderManager.RemoveFolder(order))
        {
            OnPropertyChanged("ShortcutFolders");
            return true;
        }
        return false;
    }

    public bool ReorderShortcutFolder(int fromOrder, int toOrder)
    {
        if (_folderManager.ReorderFolder(fromOrder, toOrder))
        {
            OnPropertyChanged("ShortcutFolders");
            return true;
        }
        return false;
    }

    public bool RenameShortcutFolder(int order, string newLabel)
    {
        if (_folderManager.RenameFolder(order, newLabel))
        {
            OnPropertyChanged("ShortcutFolders");
            return true;
        }
        return false;
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
            if (FolderViewMode != value && _store.SetValue(nameof(FolderViewMode), value.ToString()))
            {
                OnPropertyChanged();
            }
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
            if (CollapsibleGroups != value && _store.SetValue(nameof(CollapsibleGroups), value))
            {
                OnPropertyChanged();
            }
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
            if (HotkeyModifiers != value && _store.SetValue(nameof(HotkeyModifiers), value))
            {
                OnPropertyChanged();
            }
        }
    }

    public int HotkeyKey
    {
        get
        {
            // Virtual key codes range from 0x01 to 0xFE
            if (_store.TryGetValue(nameof(HotkeyKey), out var val) && val is int key
                && key >= Constants.MIN_VIRTUAL_KEY && key <= Constants.MAX_VIRTUAL_KEY)
            {
                return key;
            }
            return Constants.VK_S;
        }
        set
        {
            if (HotkeyKey != value && _store.SetValue(nameof(HotkeyKey), value))
            {
                OnPropertyChanged();
            }
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
            if (GridSize != value && _store.SetValue(nameof(GridSize), value.ToString()))
            {
                OnPropertyChanged();
            }
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
            if (KeepCentered != value && _store.SetValue(nameof(KeepCentered), value))
            {
                OnPropertyChanged();
            }
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
            await _startupService.DisableStartupAsync();
            IsRunAtStartup = false;
        }
    }

}
