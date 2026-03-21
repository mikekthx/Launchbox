using Launchbox.Helpers;
using Launchbox.Models;
using Launchbox.Services;
using Launchbox.ViewModels;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;

namespace Launchbox.Tests;

[Collection("Localization")]
public class MainViewModelTests
{
    private readonly MockFileSystem _fileSystem;
    private readonly ShortcutService _shortcutService;
    private readonly IconService _iconService;
    private readonly MockImageFactory _imageFactory;
    private readonly MockDispatcher _dispatcher;
    private readonly MockAppLauncher _appLauncher;
    private readonly SettingsService _settingsService;
    private readonly MockWindowService _windowService;
    private readonly MockSettingsStore _settingsStore;
    private readonly string _shortcutFolder = Path.Combine("C:", "Shortcuts");

    public MainViewModelTests()
    {
        _fileSystem = new MockFileSystem();
        _shortcutService = new ShortcutService(_fileSystem);
        _iconService = new IconService(_fileSystem);
        _imageFactory = new MockImageFactory();
        _dispatcher = new MockDispatcher();
        _appLauncher = new MockAppLauncher();
        _windowService = new MockWindowService();

        // Pre-populate the store with the default folder JSON so ShortcutFolderManager
        // picks it up at construction time (migration reads ShortcutsPath key).
        _settingsStore = new MockSettingsStore();
        _settingsStore.SetValue("ShortcutsPath", _shortcutFolder);
        var startupService = new MockStartupService();
        _settingsService = new SettingsService(_settingsStore, startupService, new ShortcutFolderManager(_settingsStore));

        _fileSystem.CreateDirectory(_shortcutFolder);

        Localization.SetProvider(new MockStringProvider(new()
        {
            { "TrayMenu_Hide", "Hide" },
            { "TrayMenu_Show", "Show" },
            { "TrayMenu_Settings", "Settings" },
            { "TrayMenu_Exit", "Exit" },
            { "Tray_TooltipFormat", "Launchbox ({0})" },
            { "Tray_AutomationName", "Launchbox System Tray Icon" },
            { "Error_NotificationTitle", "Launchbox Error" },
            { "Modifier_Alt", "Alt" },
            { "Modifier_Ctrl", "Ctrl" },
            { "Modifier_Shift", "Shift" },
            { "Modifier_Win", "Win" },
        }));
    }

    private MainViewModel CreateViewModel()
    {
        return new MainViewModel(
            _shortcutService,
            _iconService,
            _imageFactory,
            _dispatcher,
            _appLauncher,
            _fileSystem,
            _settingsService,
            _windowService);
    }

    [Fact]
    public void Constructor_InitializesAppsCollection()
    {
        var viewModel = CreateViewModel();
        Assert.NotNull(viewModel.Apps);
        Assert.Empty(viewModel.Apps);
    }

    [Fact]
    public async Task LoadAppsCommand_LoadsApps()
    {
        // Arrange
        // Add file to the shortcut folder defined in settings
        string appPath = Path.Combine(_shortcutFolder, "MyApp.lnk");
        _fileSystem.AddFile(appPath);

        var viewModel = CreateViewModel();

        // Act
        await viewModel.LoadAppsAsync();

        // Assert
        Assert.Single(viewModel.Apps);
        Assert.Equal("MyApp", viewModel.Apps[0].Name);
    }

    [Fact]
    public void LaunchAppCommand_LaunchesApp()
    {
        var viewModel = CreateViewModel();
        var appItem = new AppItem { Name = "Test", Path = "C:\\Test.lnk" };

        viewModel.LaunchAppCommand.Execute(appItem);

        Assert.Equal("C:\\Test.lnk", _appLauncher.LastLaunchedPath);
    }

    [Fact]
    public void LaunchAppCommand_HidesWindow()
    {
        var viewModel = CreateViewModel();
        var appItem = new AppItem { Name = "Test", Path = "C:\\Test.lnk" };

        viewModel.LaunchAppCommand.Execute(appItem);

        Assert.True(_windowService.HideCalled);
    }

    [Fact]
    public void ToggleWindowCommand_TogglesVisibility()
    {
        var viewModel = CreateViewModel();

        viewModel.ToggleWindowCommand.Execute(null);

        Assert.True(_windowService.ToggleVisibilityCalled);
    }

    [Fact]
    public void OpenSettingsCommand_DelegatesToWindowService()
    {
        var viewModel = CreateViewModel();

        viewModel.OpenSettingsCommand.Execute(null);

        Assert.True(_windowService.OpenSettingsCalled);
    }

    [Fact]
    public void ToggleWindowText_UpdatesWhenVisibilityChanges()
    {
        var viewModel = CreateViewModel();

        _windowService.RaiseVisibilityChanged(false);
        Assert.Equal("Show", viewModel.ToggleWindowText);

        _windowService.RaiseVisibilityChanged(true);
        Assert.Equal("Hide", viewModel.ToggleWindowText);
    }

    [Fact]
    public async Task LoadAppsAsync_SetsIsEmptyToTrue_WhenNoAppsFound()
    {
        var viewModel = CreateViewModel();

        await viewModel.LoadAppsAsync();

        Assert.True(viewModel.IsEmpty);
    }

    [Fact]
    public async Task LoadAppsAsync_SetsIsEmptyToFalse_WhenAppsFound()
    {
        string appPath = Path.Combine(_shortcutFolder, "MyApp.lnk");
        _fileSystem.AddFile(appPath);
        var viewModel = CreateViewModel();

        await viewModel.LoadAppsAsync();

        Assert.False(viewModel.IsEmpty);
    }

    [Fact]
    public void OpenShortcutsFolderCommand_OpensShortcutFolder()
    {
        var viewModel = CreateViewModel();

        viewModel.OpenShortcutsFolderCommand.Execute(null);

        Assert.Equal(_shortcutFolder, _appLauncher.LastOpenedFolder);
    }

    [Fact]
    public void OpenShortcutsFolderCommand_CreatesFolder_IfMissing()
    {
        // Add a new folder that doesn't exist yet as the first folder (order 0 replaces existing)
        string newPath = Path.Combine("C:", "NewShortcuts");
        // Reset the store to have only the new path so it becomes the first folder
        var newStore = new MockSettingsStore();
        newStore.SetValue("ShortcutsPath", newPath);
        var newSettingsService = new SettingsService(newStore, new MockStartupService(), new ShortcutFolderManager(newStore));

        var viewModel = new MainViewModel(
            _shortcutService,
            _iconService,
            _imageFactory,
            _dispatcher,
            _appLauncher,
            _fileSystem,
            newSettingsService,
            _windowService);

        Assert.False(_fileSystem.DirectoryExists(newPath));

        viewModel.OpenShortcutsFolderCommand.Execute(null);

        Assert.True(_fileSystem.DirectoryExists(newPath));
        Assert.Equal(newPath, _appLauncher.LastOpenedFolder);
    }

    [Fact]
    public void OpenShortcutsFolderCommand_CatchesException_WhenDirectoryCreationFails()
    {
        // Arrange: configure a path that doesn't exist in the throwing file system,
        // so CreateDirectory is called and throws
        string newPath = Path.Combine("C:", "NewShortcuts");
        var newStore = new MockSettingsStore();
        newStore.SetValue("ShortcutsPath", newPath);
        var newSettingsService = new SettingsService(newStore, new MockStartupService(), new ShortcutFolderManager(newStore));

        var throwingFileSystem = new ThrowingFileSystem();
        var viewModel = new MainViewModel(
            _shortcutService,
            _iconService,
            _imageFactory,
            _dispatcher,
            _appLauncher,
            throwingFileSystem,
            newSettingsService,
            _windowService);

        // Act & Assert
        // This should not throw
        var exception = Record.Exception(() => viewModel.OpenShortcutsFolderCommand.Execute(null));

        Assert.Null(exception);
    }

    [Fact]
    public void ItemWidth_Default_Is110()
    {
        var vm = CreateViewModel();
        Assert.Equal(110, vm.ItemWidth);
    }

    [Fact]
    public void ItemWidth_WhenGridSizeSmall_Is80()
    {
        _settingsService.GridSize = GridSize.Small;
        var vm = CreateViewModel();
        Assert.Equal(80, vm.ItemWidth);
    }

    [Fact]
    public void ItemHeight_WhenGridSizeLarge_Is165()
    {
        _settingsService.GridSize = GridSize.Large;
        var vm = CreateViewModel();
        Assert.Equal(165, vm.ItemHeight);
    }

    [Fact]
    public void IconSize_WhenGridSizeSmall_Is32()
    {
        _settingsService.GridSize = GridSize.Small;
        var vm = CreateViewModel();
        Assert.Equal(32, vm.IconSize);
    }

    [Fact]
    public void ItemWidth_WhenGridSizeChanges_RaisesPropertyChanged()
    {
        var vm = CreateViewModel();
        var changed = new List<string?>();
        vm.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        _settingsService.GridSize = GridSize.Large;

        Assert.Contains(nameof(MainViewModel.ItemWidth), changed);
        Assert.Contains(nameof(MainViewModel.ItemHeight), changed);
        Assert.Contains(nameof(MainViewModel.IconSize), changed);
    }

    [Fact]
    public async Task FilterText_WhenSet_FiltersAppsByName()
    {
        _fileSystem.AddFile(Path.Combine(_shortcutFolder, "Alpha.lnk"));
        _fileSystem.AddFile(Path.Combine(_shortcutFolder, "Beta.lnk"));
        _fileSystem.AddFile(Path.Combine(_shortcutFolder, "Gamma.lnk"));
        var vm = CreateViewModel();
        await vm.LoadAppsAsync();

        vm.FilterText = "alp";

        Assert.Single(vm.FilteredApps);
        Assert.Equal("Alpha", vm.FilteredApps.First().Name);
    }

    [Fact]
    public async Task FilterText_WhenEmpty_ReturnsAllApps()
    {
        _fileSystem.AddFile(Path.Combine(_shortcutFolder, "Alpha.lnk"));
        _fileSystem.AddFile(Path.Combine(_shortcutFolder, "Beta.lnk"));
        var vm = CreateViewModel();
        await vm.LoadAppsAsync();

        vm.FilterText = "";

        Assert.Equal(2, vm.FilteredApps.Count());
    }

    [Fact]
    public async Task FilterText_CaseInsensitive_MatchesRegardlessOfCase()
    {
        _fileSystem.AddFile(Path.Combine(_shortcutFolder, "Alpha.lnk"));
        var vm = CreateViewModel();
        await vm.LoadAppsAsync();

        vm.FilterText = "ALPHA";

        Assert.Single(vm.FilteredApps);
    }

    [Fact]
    public async Task FilterText_WithNoMatch_SetsHasNoMatches()
    {
        _fileSystem.AddFile(Path.Combine(_shortcutFolder, "Alpha.lnk"));
        var vm = CreateViewModel();
        await vm.LoadAppsAsync();

        vm.FilterText = "zzz";

        Assert.True(vm.HasNoMatches);
    }

    [Fact]
    public void HasNoMatches_WhenFilterTextEmpty_IsFalse()
    {
        var vm = CreateViewModel();
        vm.FilterText = "";
        Assert.False(vm.HasNoMatches);
    }

    [Fact]
    public void HasNoMatches_WhenAppsEmptyAndFilterActive_IsFalse()
    {
        // No files added — Apps is empty
        var vm = CreateViewModel();
        vm.FilterText = "anything";
        Assert.False(vm.HasNoMatches);
    }

    [Fact]
    public async Task FilteredApps_AfterAppsReload_ReflectsNewCollection()
    {
        _fileSystem.AddFile(Path.Combine(_shortcutFolder, "Alpha.lnk"));
        var vm = CreateViewModel();
        await vm.LoadAppsAsync();
        vm.FilterText = "alp";
        Assert.Single(vm.FilteredApps);

        _fileSystem.AddFile(Path.Combine(_shortcutFolder, "AlphaTwo.lnk"));
        await vm.LoadAppsAsync();

        Assert.Equal(2, vm.FilteredApps.Count());
    }

    [Fact]
    public void ToggleWindowText_ReturnsLocalizedHide_WhenVisible()
    {
        var vm = CreateViewModel();
        _windowService.RaiseVisibilityChanged(true);

        Assert.Equal("Hide", vm.ToggleWindowText);
    }

    [Fact]
    public void ToggleWindowText_ReturnsLocalizedShow_WhenHidden()
    {
        var vm = CreateViewModel();
        _windowService.RaiseVisibilityChanged(false);

        Assert.Equal("Show", vm.ToggleWindowText);
    }

    [Fact]
    public void SettingsMenuText_ReturnsLocalizedString()
    {
        var vm = CreateViewModel();
        Assert.Equal("Settings", vm.SettingsMenuText);
    }

    [Fact]
    public void ExitMenuText_ReturnsLocalizedString()
    {
        var vm = CreateViewModel();
        Assert.Equal("Exit", vm.ExitMenuText);
    }

    [Fact]
    public void TrayToolTipText_UsesLocalizedFormat()
    {
        var vm = CreateViewModel();
        // Default hotkey is Alt+S
        Assert.Equal("Launchbox (Alt+S)", vm.TrayToolTipText);
    }

    private class ThrowingFileSystem : MockFileSystem
    {
        public override void CreateDirectory(string path)
        {
            throw new UnauthorizedAccessException("Access denied");
        }
    }

    [Fact]
    public async Task Dispose_DisposesCancellationTokenSource()
    {
        // Arrange
        var viewModel = CreateViewModel();

        // Act
        // Call LoadAppsAsync to ensure the _loadCts is initialized
        await viewModel.LoadAppsAsync();

        // At this point, _loadCts is initialized but not disposed, let's grab the actual Token to inspect
        // The token is not publicly exposed, so we invoke Dispose() to test its effect
        viewModel.Dispose();

        // Assert
        // 1. Verify CancellationTokenSource is disposed.
        var loadCtsField = typeof(MainViewModel).GetField("_loadCts", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var cts = (System.Threading.CancellationTokenSource?)loadCtsField?.GetValue(viewModel);

        Assert.NotNull(cts);
        Assert.Throws<ObjectDisposedException>(() =>
        {
            // Accessing WaitHandle on a disposed CancellationTokenSource throws ObjectDisposedException
            var handle = cts.Token.WaitHandle;
        });

        // 2. Verify SettingsService event is unsubscribed.
        // After disposal, modifying the ShortcutsPath should not trigger LoadAppsAsync().
        // We can observe this by clearing the Apps collection, triggering the change,
        // and waiting to see if it populates.
        viewModel.Apps.Clear();
        _settingsService.ShortcutsPath = "C:\\NewPath";

        // Dispatcher operations enqueue async tasks. We can await EnqueueAsync in our MockDispatcher.
        // But since MockDispatcher runs synchronously, the change would have happened immediately if subscribed.
        Assert.Empty(viewModel.Apps);
    }

    // --- MULTI-FOLDER TESTS ---

    private SettingsService CreateSettingsServiceWithFolders(params (string path, string label, int order)[] folderDefs)
    {
        var store = new MockSettingsStore();
        var folders = folderDefs.Select(f => new { Path = f.path, Label = f.label, Order = f.order }).ToList();
        store.SetValue("ShortcutFolders", JsonSerializer.Serialize(folders));
        return new SettingsService(store, new MockStartupService(), new ShortcutFolderManager(store));
    }

    [Fact]
    public async Task LoadAppsAsync_LoadsFromMultipleFolders()
    {
        // Arrange: two folders, each with one file
        string folder2 = Path.Combine("C:", "Games");
        _fileSystem.CreateDirectory(folder2);
        _fileSystem.AddFile(Path.Combine(_shortcutFolder, "App1.lnk"));
        _fileSystem.AddFile(Path.Combine(folder2, "Game1.lnk"));

        var settingsService = CreateSettingsServiceWithFolders(
            (_shortcutFolder, "Shortcuts", 0),
            (folder2, "Games", 1));

        var vm = new MainViewModel(_shortcutService, _iconService, _imageFactory, _dispatcher,
            _appLauncher, _fileSystem, settingsService, _windowService);

        // Act
        await vm.LoadAppsAsync();

        // Assert: both apps appear, each with its folder label
        Assert.Equal(2, vm.Apps.Count);
        var app1 = vm.Apps.FirstOrDefault(a => a.Name == "App1");
        var game1 = vm.Apps.FirstOrDefault(a => a.Name == "Game1");
        Assert.NotNull(app1);
        Assert.NotNull(game1);
        Assert.Equal("Shortcuts", app1.FolderLabel);
        Assert.Equal("Games", game1.FolderLabel);
    }

    [Fact]
    public async Task LoadAppsAsync_MergedMode_SortsAlphabetically()
    {
        // Arrange: two files in same folder, added in reverse alphabetical order to verify sorting
        _fileSystem.AddFile(Path.Combine(_shortcutFolder, "Zephyr.lnk"));
        _fileSystem.AddFile(Path.Combine(_shortcutFolder, "Alpha.lnk"));

        var vm = CreateViewModel();

        // Act
        await vm.LoadAppsAsync();

        // Assert: Apps are sorted alphabetically
        Assert.Equal(2, vm.Apps.Count);
        Assert.Equal("Alpha", vm.Apps[0].Name);
        Assert.Equal("Zephyr", vm.Apps[1].Name);
    }

    [Fact]
    public async Task LoadAppsAsync_GroupedMode_BuildsGroupedApps()
    {
        // Arrange: two folders
        string folder2 = Path.Combine("C:", "Games");
        _fileSystem.CreateDirectory(folder2);
        _fileSystem.AddFile(Path.Combine(_shortcutFolder, "App1.lnk"));
        _fileSystem.AddFile(Path.Combine(folder2, "Game1.lnk"));

        var settingsService = CreateSettingsServiceWithFolders(
            (_shortcutFolder, "Shortcuts", 0),
            (folder2, "Games", 1));

        var vm = new MainViewModel(_shortcutService, _iconService, _imageFactory, _dispatcher,
            _appLauncher, _fileSystem, settingsService, _windowService);

        // Act
        await vm.LoadAppsAsync();

        // Assert: GroupedApps has two groups with correct labels
        Assert.Equal(2, vm.GroupedApps.Count);
        Assert.Contains(vm.GroupedApps, g => g.Label == "Shortcuts");
        Assert.Contains(vm.GroupedApps, g => g.Label == "Games");
    }

    [Fact]
    public async Task LoadAppsAsync_GroupedMode_GroupsOrderedByFolderOrder()
    {
        // Arrange: two folders, Games has lower order (should appear first)
        string folder2 = Path.Combine("C:", "Games");
        _fileSystem.CreateDirectory(folder2);
        _fileSystem.AddFile(Path.Combine(_shortcutFolder, "App1.lnk"));
        _fileSystem.AddFile(Path.Combine(folder2, "Game1.lnk"));

        var settingsService = CreateSettingsServiceWithFolders(
            (folder2, "Games", 0),
            (_shortcutFolder, "Shortcuts", 1));

        var vm = new MainViewModel(_shortcutService, _iconService, _imageFactory, _dispatcher,
            _appLauncher, _fileSystem, settingsService, _windowService);

        // Act
        await vm.LoadAppsAsync();

        // Assert: first group is Games (order 0), second is Shortcuts (order 1)
        Assert.Equal(2, vm.GroupedApps.Count);
        Assert.Equal("Games", vm.GroupedApps[0].Label);
        Assert.Equal("Shortcuts", vm.GroupedApps[1].Label);
    }

    [Fact]
    public async Task ApplyFilter_HidesNonMatchingItemsInGroups()
    {
        // Arrange: two apps in same folder
        _fileSystem.AddFile(Path.Combine(_shortcutFolder, "Alpha.lnk"));
        _fileSystem.AddFile(Path.Combine(_shortcutFolder, "Beta.lnk"));
        var vm = CreateViewModel();
        await vm.LoadAppsAsync();

        // Act
        vm.FilterText = "alp";

        // Assert: the group only shows matching items
        Assert.Single(vm.GroupedApps);
        Assert.Single(vm.GroupedApps[0]);
        Assert.Equal("Alpha", vm.GroupedApps[0][0].Name);
    }

    [Fact]
    public async Task FilteredApps_FilterAcrossMultipleFolders()
    {
        // Arrange: two folders, one matching item in each
        string folder2 = Path.Combine("C:", "Games");
        _fileSystem.CreateDirectory(folder2);
        _fileSystem.AddFile(Path.Combine(_shortcutFolder, "Alpha.lnk"));
        _fileSystem.AddFile(Path.Combine(folder2, "AlphaGame.lnk"));
        _fileSystem.AddFile(Path.Combine(folder2, "Beta.lnk"));

        var settingsService = CreateSettingsServiceWithFolders(
            (_shortcutFolder, "Shortcuts", 0),
            (folder2, "Games", 1));

        var vm = new MainViewModel(_shortcutService, _iconService, _imageFactory, _dispatcher,
            _appLauncher, _fileSystem, settingsService, _windowService);

        await vm.LoadAppsAsync();

        // Act
        vm.FilterText = "alp";

        // Assert: FilteredApps includes matches from both folders
        Assert.Equal(2, vm.FilteredApps.Count);
        Assert.Contains(vm.FilteredApps, a => a.Name == "Alpha");
        Assert.Contains(vm.FilteredApps, a => a.Name == "AlphaGame");
    }

    [Fact]
    public async Task LoadAppsAsync_DuplicateNames_BothAppear()
    {
        // Arrange: same name in two different folders
        string folder2 = Path.Combine("C:", "Games");
        _fileSystem.CreateDirectory(folder2);
        _fileSystem.AddFile(Path.Combine(_shortcutFolder, "App.lnk"));
        _fileSystem.AddFile(Path.Combine(folder2, "App.lnk"));

        var settingsService = CreateSettingsServiceWithFolders(
            (_shortcutFolder, "Shortcuts", 0),
            (folder2, "Games", 1));

        var vm = new MainViewModel(_shortcutService, _iconService, _imageFactory, _dispatcher,
            _appLauncher, _fileSystem, settingsService, _windowService);

        // Act
        await vm.LoadAppsAsync();

        // Assert: both "App" items appear, from different folders
        Assert.Equal(2, vm.Apps.Count);
        Assert.Equal(2, vm.Apps.Count(a => a.Name == "App"));
        Assert.Contains(vm.Apps, a => a.FolderLabel == "Shortcuts");
        Assert.Contains(vm.Apps, a => a.FolderLabel == "Games");
    }

    [Fact]
    public void OpenShortcutsFolder_MultipleFolders_OpensFirst()
    {
        // Arrange: two folders, second has lower order to verify first-by-order wins
        string folder2 = Path.Combine("C:", "Games");
        _fileSystem.CreateDirectory(folder2);

        var settingsService = CreateSettingsServiceWithFolders(
            (_shortcutFolder, "Shortcuts", 0),
            (folder2, "Games", 1));

        var vm = new MainViewModel(_shortcutService, _iconService, _imageFactory, _dispatcher,
            _appLauncher, _fileSystem, settingsService, _windowService);

        // Act
        vm.OpenShortcutsFolderCommand.Execute(null);

        // Assert: opens the first folder by order (Shortcuts, order 0)
        Assert.Equal(_shortcutFolder, _appLauncher.LastOpenedFolder);
    }
}
