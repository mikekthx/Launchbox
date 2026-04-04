using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Launchbox.Helpers;
using Launchbox.Models;
using Launchbox.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Launchbox.ViewModels;

public partial class MainViewModel : ObservableObject, IDisposable
{
    private readonly IShortcutService _shortcutService;
    private readonly IIconService _iconService;
    private readonly IImageFactory _imageFactory;
    private readonly IDispatcher _dispatcher;
    private readonly IAppLauncher _appLauncher;
    private readonly IFileSystem _fileSystem;
    private readonly SettingsService _settingsService;
    private readonly IWindowService _windowService;
    private CancellationTokenSource? _loadCts;

    public BulkObservableCollection<AppItem> Apps { get; } = [];

    public BulkObservableCollection<AppItemGroup> GroupedApps { get; } = [];

    public bool CollapsibleGroupsEnabled => _settingsService.CollapsibleGroups;

    public bool IsMergedMode => _settingsService.FolderViewMode == FolderViewMode.Merged;
    public bool IsGroupedMode => _settingsService.FolderViewMode == FolderViewMode.Grouped;
    public bool IsMergedModeVisible => IsMergedMode && !IsEmpty;
    public bool IsGroupedModeVisible => IsGroupedMode && !IsEmpty;

    private bool _isEmpty;
    public bool IsEmpty
    {
        get => _isEmpty;
        private set => SetProperty(ref _isEmpty, value);
    }

    private string _filterText = string.Empty;

    public string FilterText
    {
        get => _filterText;
        set
        {
            if (SetProperty(ref _filterText, value))
            {
                OnPropertyChanged(nameof(IsFilterEmpty));
                RebuildFilteredApps();
                OnPropertyChanged(nameof(HasNoMatches));
                ApplyGroupedFilter();
            }
        }
    }

    private void ApplyGroupedFilter()
    {
        foreach (var group in GroupedApps)
        {
            group.ApplyFilter(_filterText);
        }
    }

    public BulkObservableCollection<AppItem> FilteredApps { get; } = [];

    private void RebuildFilteredApps()
    {
        var source = string.IsNullOrEmpty(_filterText)
            ? (IEnumerable<AppItem>)Apps
            : Apps.Where(a => a.Name.Contains(_filterText, StringComparison.OrdinalIgnoreCase));
        FilteredApps.ReplaceAll(source);
    }

    public bool IsFilterEmpty => string.IsNullOrEmpty(_filterText);

    public bool HasNoMatches =>
        Apps.Count > 0 && !string.IsNullOrEmpty(_filterText) && FilteredApps.Count == 0;

    public int ItemWidth => _settingsService.GridSize switch
    {
        GridSize.Small => 80,
        GridSize.Large => 140,
        _ => 110,
    };

    public int ItemHeight => _settingsService.GridSize switch
    {
        GridSize.Small => 96,
        GridSize.Large => 165,
        _ => 130,
    };

    public int IconSize => _settingsService.GridSize switch
    {
        GridSize.Small => 32,
        GridSize.Large => 72,
        _ => 56,
    };

    public string ToggleWindowText => _windowService.IsVisible
        ? Localization.GetString("TrayMenu_Hide")
        : Localization.GetString("TrayMenu_Show");

    public string SettingsMenuText => Localization.GetString("TrayMenu_Settings");

    public string ExitMenuText => Localization.GetString("TrayMenu_Exit");

    private string? _trayToolTipText;

    public string TrayToolTipText
    {
        get
        {
            if (_trayToolTipText is null)
            {
                int mod = _settingsService.HotkeyModifiers;
                int key = _settingsService.HotkeyKey;

                var sb = new StringBuilder();
                if ((mod & Constants.MOD_CONTROL) != 0)
                {
                    sb.Append(Localization.GetString("Modifier_Ctrl"));
                    sb.Append('+');
                }
                if ((mod & Constants.MOD_ALT) != 0)
                {
                    sb.Append(Localization.GetString("Modifier_Alt"));
                    sb.Append('+');
                }
                if ((mod & Constants.MOD_SHIFT) != 0)
                {
                    sb.Append(Localization.GetString("Modifier_Shift"));
                    sb.Append('+');
                }
                if ((mod & Constants.MOD_WIN) != 0)
                {
                    sb.Append(Localization.GetString("Modifier_Win"));
                    sb.Append('+');
                }

                var vk = (Windows.System.VirtualKey)key;
                string keyName = vk >= Windows.System.VirtualKey.Number0 && vk <= Windows.System.VirtualKey.Number9
                    ? ((char)key).ToString()
                    : vk.ToString();
                sb.Append(keyName);

                _trayToolTipText = string.Format(Localization.GetString("Tray_TooltipFormat"), sb.ToString());
            }

            return _trayToolTipText!;
        }
    }

    public MainViewModel(
        IShortcutService shortcutService,
        IIconService iconService,
        IImageFactory imageFactory,
        IDispatcher dispatcher,
        IAppLauncher appLauncher,
        IFileSystem fileSystem,
        SettingsService settingsService,
        IWindowService windowService)
    {
        _shortcutService = shortcutService ?? throw new ArgumentNullException(nameof(shortcutService));
        _iconService = iconService ?? throw new ArgumentNullException(nameof(iconService));
        _imageFactory = imageFactory ?? throw new ArgumentNullException(nameof(imageFactory));
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _appLauncher = appLauncher ?? throw new ArgumentNullException(nameof(appLauncher));
        _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _windowService = windowService ?? throw new ArgumentNullException(nameof(windowService));

        _settingsService.PropertyChanged += SettingsService_PropertyChanged;
        _windowService.VisibilityChanged += WindowService_VisibilityChanged;

        // FilteredApps and HasNoMatches are computed from Apps. WinUI data binding
        // won't re-evaluate them automatically when Apps changes, so we notify explicitly.
        Apps.CollectionChanged += Apps_CollectionChanged;
    }

    private void Apps_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        RebuildFilteredApps();
        OnPropertyChanged(nameof(HasNoMatches));
    }

    private void SettingsService_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SettingsService.ShortcutsPath))
        {
            // Reload apps when folder path changes
            _ = LoadAppsAsync();
        }
        else if (e.PropertyName == SettingsService.SHORTCUT_FOLDERS_KEY)
        {
            _ = LoadAppsAsync();
        }
        else if (e.PropertyName == nameof(SettingsService.GridSize))
        {
            OnPropertyChanged(nameof(ItemWidth));
            OnPropertyChanged(nameof(ItemHeight));
            OnPropertyChanged(nameof(IconSize));
        }
        else if (e.PropertyName is nameof(SettingsService.HotkeyModifiers) or nameof(SettingsService.HotkeyKey))
        {
            _trayToolTipText = null;
            OnPropertyChanged(nameof(TrayToolTipText));
        }
        else if (e.PropertyName == nameof(SettingsService.CollapsibleGroups))
        {
            OnPropertyChanged(nameof(CollapsibleGroupsEnabled));
        }
        else if (e.PropertyName == nameof(SettingsService.FolderViewMode))
        {
            OnPropertyChanged(nameof(IsMergedMode));
            OnPropertyChanged(nameof(IsGroupedMode));
            OnPropertyChanged(nameof(IsMergedModeVisible));
            OnPropertyChanged(nameof(IsGroupedModeVisible));
        }
    }

    private void WindowService_VisibilityChanged(object? sender, bool e)
    {
        OnPropertyChanged(nameof(ToggleWindowText));
    }

    /// <summary>
    /// Loads shortcuts from the configured folder and concurrently extracts their icons.
    /// Utilizes Parallel.ForEachAsync with bounded parallelism to prevent ThreadPool starvation
    /// during blocking GDI+ extraction operations, while gracefully handling cancellation.
    /// </summary>
    [RelayCommand(AllowConcurrentExecutions = true)]
    public async Task LoadAppsAsync()
    {
        // Debounce: atomically swap the CTS so only the latest call wins.
        // Interlocked.Exchange is safe even if called from multiple threads.
        var cts = new CancellationTokenSource();
        var old = Interlocked.Exchange(ref _loadCts, cts);
        // Cancel but do not Dispose: background tasks hold a captured CancellationToken derived
        // from this source and may still be checking it as they unwind. Disposing the source
        // while token callbacks are still registered can throw ObjectDisposedException.
        // CancellationTokenSource is GC-managed and does not hold unmanaged resources.
        old?.Cancel();
        var ct = cts.Token;

        try
        {
            var folders = _settingsService.GetShortcutFolders();
            var (localAppItems, allFiles) = await LoadAppItemsAsync(folders, ct);

            ct.ThrowIfCancellationRequested();

            _iconService.PruneCache(allFiles);

            var orderedItems = OrderAppItems(localAppItems);
            var groupedData = BuildGroupedData(orderedItems, folders);

            ct.ThrowIfCancellationRequested();

            await UpdateUIAsync(orderedItems, groupedData);

            await ExtractIconsAsync(localAppItems, ct);
        }
        catch (OperationCanceledException)
        {
            // Load was superseded by a newer call -- expected, not an error
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"Unexpected error in LoadAppsAsync: {PathSecurity.GetSafeExceptionMessage(ex)}");
        }
    }

    private async Task<(List<AppItem> Items, List<string> AllFiles)> LoadAppItemsAsync(
        IReadOnlyList<ShortcutFolder> folders,
        CancellationToken ct)
    {
        List<AppItem> localAppItems = [];
        List<string> allFiles = [];

        foreach (var folder in folders.OrderBy(f => f.Order))
        {
            // Avoid blocking the UI thread when the shortcuts folder is on a slow or sleeping drive
            var files = await Task.Run(() =>
                _shortcutService.GetShortcutFiles(
                    folder.ExpandedPath,
                    Constants.ALLOWED_EXTENSIONS, ct), ct);

            if (files != null)
            {
                allFiles.AddRange(files);
                foreach (var file in files)
                {
                    try
                    {
                        var name = Path.GetFileNameWithoutExtension(file);
                        localAppItems.Add(new AppItem
                        {
                            Name = name,
                            Path = file,
                            FolderLabel = folder.Label,
                            FolderPath = folder.Path
                        });
                    }
                    catch (Exception ex)
                    {
                        Trace.WriteLine($"Failed to load app {PathSecurity.RedactPath(file)}: {PathSecurity.GetSafeExceptionMessage(ex)}");
                    }
                }
            }
            else
            {
                Trace.WriteLine($"Shortcut folder not found: {PathSecurity.RedactPath(folder.Path)}");
            }
        }

        return (localAppItems, allFiles);
    }

    private List<AppItem> OrderAppItems(List<AppItem> items)
    {
        // Apply custom item order per folder, falling back to alphabetical for unordered items.
        return items
            .GroupBy(a => a.FolderPath)
            .SelectMany(g =>
            {
                var customOrder = _settingsService.GetItemOrder(g.Key);
                if (customOrder.Count == 0)
                    return (IEnumerable<AppItem>)g.OrderBy(a => a.Name);

                var byName = g.ToDictionary(
                    a => Path.GetFileName(a.Path),
                    StringComparer.OrdinalIgnoreCase);

                // Items with a custom position first, then new/unlisted items alphabetically.
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

    private List<AppItemGroup> BuildGroupedData(List<AppItem> orderedItems, IReadOnlyList<ShortcutFolder> folders)
    {
        // Build grouped data structure — group by FolderPath (unique), display Label
        var folderLookup = folders.ToDictionary(f => f.Path, StringComparer.OrdinalIgnoreCase);
        return orderedItems
            .GroupBy(a => a.FolderPath)
            .Select(g =>
            {
                var found = folderLookup.TryGetValue(g.Key, out var f);
                return (
                    Group: new AppItemGroup(found ? f!.Label : Path.GetFileName(g.Key) ?? g.Key, g.Key, g),
                    Order: found ? f!.Order : int.MaxValue
                );
            })
            .OrderBy(x => x.Order)
            .Select(x => x.Group)
            .ToList();
    }

    private Task UpdateUIAsync(List<AppItem> orderedItems, List<AppItemGroup> groupedData)
    {
        return _dispatcher.EnqueueAsync(() =>
        {
            Apps.ReplaceAll(orderedItems);
            IsEmpty = Apps.Count == 0;

            GroupedApps.ReplaceAll(groupedData);

            OnPropertyChanged(nameof(IsMergedModeVisible));
            OnPropertyChanged(nameof(IsGroupedModeVisible));

            // Reapply active filter to new group instances
            if (!string.IsNullOrEmpty(_filterText))
            {
                ApplyGroupedFilter();
            }

            return Task.CompletedTask;
        });
    }

    private async Task ExtractIconsAsync(List<AppItem> items, CancellationToken ct)
    {
        var parallelOptions = new ParallelOptions
        {
            CancellationToken = ct,
            MaxDegreeOfParallelism = 2 // Bound parallelism to prevent ThreadPool starvation with GDI+ locks
        };

        await Parallel.ForEachAsync(items, parallelOptions, async (item, token) =>
        {
            try
            {
                // Parallel.ForEachAsync already runs on thread pool threads — no need for Task.Run
                var iconBytes = _iconService.ExtractIconBytes(item.Path, token);
                if (iconBytes != null && !token.IsCancellationRequested)
                {
                    await _dispatcher.EnqueueAsync(async () =>
                    {
                        var image = await _imageFactory.CreateImageAsync(iconBytes);
                        item.Icon = image;
                    });
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"Failed to load icon for {PathSecurity.RedactPath(item.Path)}: {PathSecurity.GetSafeExceptionMessage(ex)}");
            }
        });
    }

    [RelayCommand]
    private void LaunchApp(object? parameter)
    {
        if (parameter is AppItem app)
        {
            try
            {
                _appLauncher.Launch(app.Path);
                _windowService.Hide();
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"Failed to launch app {PathSecurity.RedactPath(app.Path)}: {PathSecurity.GetSafeExceptionMessage(ex)}");
            }
        }
    }

    [RelayCommand]
    private void OpenShortcutsFolder()
    {
        try
        {
            var folders = _settingsService.GetShortcutFolders();
            var firstFolder = folders.OrderBy(f => f.Order).FirstOrDefault();
            var shortcutFolder = !string.IsNullOrEmpty(firstFolder?.ExpandedPath)
                ? firstFolder!.ExpandedPath
                : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "Shortcuts");

            if (!_fileSystem.DirectoryExists(shortcutFolder))
            {
                _fileSystem.CreateDirectory(shortcutFolder);
            }
            _appLauncher.OpenFolder(shortcutFolder);
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"Failed to open shortcuts folder: {PathSecurity.GetSafeExceptionMessage(ex)}");
        }
    }

    [RelayCommand]
    private void ToggleWindow() => _windowService.ToggleVisibility();

    [RelayCommand]
    private void Exit() => _windowService.Exit();

    [RelayCommand]
    private void OpenSettings() => _windowService.OpenSettings();

    [RelayCommand]
    private void ToggleGroup(AppItemGroup? group)
    {
        if (CollapsibleGroupsEnabled && group is not null)
        {
            // Match by FolderPath (stable identity) — labels can be duplicated, paths cannot
            var stableGroup = GroupedApps.FirstOrDefault(g => g.FolderPath == group.FolderPath);
            if (stableGroup is not null)
            {
                // IsCollapsed setter mutates the inner ObservableCollection in place —
                // CollectionViewSource observes the change automatically
                stableGroup.IsCollapsed = !stableGroup.IsCollapsed;
            }
        }
    }

    public void ClearFilter() => FilterText = string.Empty;

    /// <summary>
    /// Persists the current order of <see cref="FilteredApps"/> after a drag-and-drop reorder.
    /// Groups items by folder and writes all ordered file names in a single store write.
    /// Also syncs <see cref="Apps"/> to match the new order so filter rebuilds stay consistent.
    /// </summary>
    public void PersistItemOrder()
    {
        var orders = FilteredApps
            .GroupBy(a => a.FolderPath)
            .ToDictionary(g => g.Key, g => g.Select(a => Path.GetFileName(a.Path)).ToList());
        // Merge rather than replace: a folder that is currently unavailable (e.g. slow drive
        // that failed to load) produces no items in FilteredApps and must not lose its saved order.
        _settingsService.MergeItemOrders(orders);

        // Sync Apps without triggering a redundant RebuildFilteredApps — FilteredApps == Apps
        // when no filter is active (CanReorderItems is bound to IsFilterEmpty).
        Apps.CollectionChanged -= Apps_CollectionChanged;
        Apps.ReplaceAll(FilteredApps);
        Apps.CollectionChanged += Apps_CollectionChanged;
    }

    public void Dispose()
    {
        // Cancel only — see LoadAppsAsync for why Dispose is intentionally omitted.
        _loadCts?.Cancel();
        _settingsService.PropertyChanged -= SettingsService_PropertyChanged;
        _windowService.VisibilityChanged -= WindowService_VisibilityChanged;
        Apps.CollectionChanged -= Apps_CollectionChanged;
        GC.SuppressFinalize(this);
    }
}
