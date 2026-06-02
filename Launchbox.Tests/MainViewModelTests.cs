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

    [Fact]
    public void SearchBoxKeyDownCommand_EnterWithSelectedItem_LaunchesApp()
    {
        // Arrange
        using var vm = CreateViewModel();
        var app = new AppItem { Path = "C:\\test.lnk", Name = "test.lnk", FolderPath = "C:\\", FolderLabel = "Test App" };
        vm.SelectedItem = app;

        // Act
        vm.SearchBoxKeyDownCommand.Execute(Windows.System.VirtualKey.Enter);

        // Assert
        Assert.Equal("C:\\test.lnk", _appLauncher.LastLaunchedPath);
    }

    [Fact]
    public void SearchBoxKeyDownCommand_EnterWithoutSelectedItem_DoesNothing()
    {
        // Arrange
        using var vm = CreateViewModel();
        vm.SelectedItem = null;

        // Act
        vm.SearchBoxKeyDownCommand.Execute(Windows.System.VirtualKey.Enter);

        // Assert
        Assert.Null(_appLauncher.LastLaunchedPath);
    }

    [Fact]
    public void SearchBoxKeyDownCommand_DownKey_RaisesGridFocusRequestedEvent()
    {
        // Arrange
        using var vm = CreateViewModel();
        var eventRaised = false;
        vm.GridFocusRequested += (s, e) => eventRaised = true;

        // Act
        vm.SearchBoxKeyDownCommand.Execute(Windows.System.VirtualKey.Down);

        // Assert
        Assert.True(eventRaised);
    }

    [Fact]
    public void SearchBoxKeyDownCommand_OtherKey_DoesNothing()
    {
        // Arrange
        using var vm = CreateViewModel();
        var eventRaised = false;
        vm.GridFocusRequested += (s, e) => eventRaised = true;

        // Act
        vm.SearchBoxKeyDownCommand.Execute(Windows.System.VirtualKey.A);

        // Assert
        Assert.False(eventRaised);
        Assert.Null(_appLauncher.LastLaunchedPath);
    }

    {
        // Arrange
        using var vm = CreateViewModel();
        var app = new AppItem { Path = "C:\\test.lnk", Name = "test.lnk", FolderPath = "C:\\", FolderLabel = "Test App" };
        vm.SelectedItem = app;

        // Act
        vm.SearchBoxKeyDownCommand.Execute(Windows.System.VirtualKey.Enter);

        // Assert
        Assert.Equal("C:\\test.lnk", _appLauncher.LastLaunchedPath);
    }

    [Fact]
    public void SearchBoxKeyDownCommand_EnterWithoutSelectedItem_DoesNothing()
    {
        // Arrange
        using var vm = CreateViewModel();
        vm.SelectedItem = null;

        // Act
        vm.SearchBoxKeyDownCommand.Execute(Windows.System.VirtualKey.Enter);

        // Assert
        Assert.Null(_appLauncher.LastLaunchedPath);
    }

    [Fact]
    public void SearchBoxKeyDownCommand_DownKey_RaisesGridFocusRequestedEvent()
    {
        // Arrange
        using var vm = CreateViewModel();
        var eventRaised = false;
        vm.GridFocusRequested += (s, e) => eventRaised = true;

        // Act
        vm.SearchBoxKeyDownCommand.Execute(Windows.System.VirtualKey.Down);

        // Assert
        Assert.True(eventRaised);
    }

    [Fact]
    public void SearchBoxKeyDownCommand_OtherKey_DoesNothing()
    {
        // Arrange
        using var vm = CreateViewModel();
        var eventRaised = false;
        vm.GridFocusRequested += (s, e) => eventRaised = true;

        // Act
        vm.SearchBoxKeyDownCommand.Execute(Windows.System.VirtualKey.A);

        // Assert
        Assert.False(eventRaised);
        Assert.Null(_appLauncher.LastLaunchedPath);
    }

    [Fact]
    public void CharacterReceivedCommand_AppendsCharacterAndRaisesEvent()
    {
        // Arrange
        using var vm = CreateViewModel();
        var eventRaised = false;
        vm.SearchFocusRequested += (sender, args) => eventRaised = true;

        vm.FilterText = "test";

        // Act
        vm.CharacterReceivedCommand.Execute("s");

        // Assert
        Assert.Equal("tests", vm.FilterText);
        Assert.True(eventRaised);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void CharacterReceivedCommand_IgnoresNullOrEmptyString(string? input)
    {
        // Arrange
        using var vm = CreateViewModel();
        var eventRaised = false;
        vm.SearchFocusRequested += (sender, args) => eventRaised = true;

        vm.FilterText = "test";

        // Act
        vm.CharacterReceivedCommand.Execute(input);

        // Assert
        Assert.Equal("test", vm.FilterText);
        Assert.False(eventRaised);
    }

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
    public async Task SelectedItem_WhenFilterActive_IsFirstFilteredResult()
    {
        _fileSystem.AddFile(Path.Combine(_shortcutFolder, "Chrome.lnk"));
        _fileSystem.AddFile(Path.Combine(_shortcutFolder, "Firefox.lnk"));
        var vm = CreateViewModel();
        await vm.LoadAppsAsync();

        vm.FilterText = "ch";

        Assert.NotNull(vm.SelectedItem);
        Assert.Equal("Chrome", vm.SelectedItem!.Name);
    }

    [Fact]
    public async Task SelectedItem_WhenFilterCleared_IsNull()
    {
        _fileSystem.AddFile(Path.Combine(_shortcutFolder, "Chrome.lnk"));
        var vm = CreateViewModel();
        await vm.LoadAppsAsync();
        vm.FilterText = "ch";
        Assert.NotNull(vm.SelectedItem);

        vm.FilterText = string.Empty;

        Assert.Null(vm.SelectedItem);
    }

    [Fact]
    public async Task SelectedItem_WhenFilterHasNoMatches_IsNull()
    {
        _fileSystem.AddFile(Path.Combine(_shortcutFolder, "Chrome.lnk"));
        var vm = CreateViewModel();
        await vm.LoadAppsAsync();

        vm.FilterText = "zzz";

        Assert.Null(vm.SelectedItem);
    }

    [Fact]
    public async Task FilterText_PrefixMatchRanksBeforeSubstringMatch()
    {
        _fileSystem.AddFile(Path.Combine(_shortcutFolder, "VisualStudio.lnk"));
        _fileSystem.AddFile(Path.Combine(_shortcutFolder, "Studio.lnk"));
        _fileSystem.AddFile(Path.Combine(_shortcutFolder, "AnotherStudio.lnk"));
        var vm = CreateViewModel();
        await vm.LoadAppsAsync();

        vm.FilterText = "studio";

        var names = vm.FilteredApps.Select(a => a.Name).ToList();
        Assert.Equal("Studio", names[0]);
        Assert.Equal(3, names.Count);
        Assert.DoesNotContain("Studio", names.Skip(1).Select(a => a));
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
    public async Task Dispose_CancelsCancellationTokenSource()
    {
        // Arrange
        var viewModel = CreateViewModel();

        // Act: initialize _loadCts, then dispose.
        await viewModel.LoadAppsAsync();
        viewModel.Dispose();

        // Assert
        // 1. Verify the CTS is cancelled (but intentionally not disposed — see MainViewModel.Dispose
        //    for why: disposing while background tasks hold the token can throw ObjectDisposedException).
        var loadCtsField = typeof(MainViewModel).GetField("_loadCts", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var cts = (System.Threading.CancellationTokenSource?)loadCtsField?.GetValue(viewModel);

        Assert.NotNull(cts);
        Assert.True(cts.IsCancellationRequested);

        // 2. Verify SettingsService event is unsubscribed.
        // After disposal, modifying the ShortcutsPath should not trigger LoadAppsAsync().
        // We can observe this by clearing the Apps collection, triggering the change,
        // and waiting to see if it populates.
        viewModel.Apps.Clear();
        _settingsService.AddShortcutFolder("C:\\NewPath");

        // Dispatcher operations enqueue async tasks. We can await EnqueueAsync in our MockDispatcher.
        // But since MockDispatcher runs synchronously, the change would have happened immediately if subscribed.
        Assert.Empty(viewModel.Apps);
    }

    // --- FOLDER WATCHER TESTS ---

    [Fact]
    public async Task LoadAppsAsync_RegistersWatcherForEachFolder()
    {
        // Arrange
        const string folder1 = @"C:\Shortcuts1";
        const string folder2 = @"C:\Shortcuts2";
        _fileSystem.AddDirectory(folder1);
        _fileSystem.AddDirectory(folder2);
        _settingsService.AddShortcutFolder(folder1);
        _settingsService.AddShortcutFolder(folder2);

        var viewModel = CreateViewModel();

        // Act
        await viewModel.LoadAppsAsync();

        // Assert: one watcher per folder was registered
        Assert.Equal(1, _fileSystem.GetWatcherCount(folder1));
        Assert.Equal(1, _fileSystem.GetWatcherCount(folder2));
    }

    [Fact]
    public async Task LoadAppsAsync_ReplacesWatchersOnReload()
    {
        // Arrange
        const string folderPath = @"C:\Shortcuts";
        _fileSystem.AddDirectory(folderPath);
        _settingsService.AddShortcutFolder(folderPath);

        var viewModel = CreateViewModel();

        // Act: load twice with same folder list — second load should skip rebuild (no-op)
        await viewModel.LoadAppsAsync();
        await viewModel.LoadAppsAsync();

        // Assert: still exactly one watcher (unchanged folder list reuses existing watcher)
        Assert.Equal(1, _fileSystem.GetWatcherCount(folderPath));
    }

    [Fact]
    public async Task Dispose_RemovesAllFolderWatchers()
    {
        // Arrange
        const string folder1 = @"C:\Shortcuts1";
        const string folder2 = @"C:\Shortcuts2";
        _fileSystem.AddDirectory(folder1);
        _fileSystem.AddDirectory(folder2);
        _settingsService.AddShortcutFolder(folder1);
        _settingsService.AddShortcutFolder(folder2);

        var viewModel = CreateViewModel();
        await viewModel.LoadAppsAsync();

        // Act
        viewModel.Dispose();

        // Assert: watchers were disposed and unregistered from MockFileSystem
        Assert.Equal(0, _fileSystem.GetWatcherCount(folder1));
        Assert.Equal(0, _fileSystem.GetWatcherCount(folder2));
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

    [Fact]
    public async Task LoadAppsAsync_MultipleFolders_ItemsClusteredByFolderOrder()
    {
        // Arrange: two folders — custom order groups items by folder rather than sorting globally.
        // "Zephyr" from folder 1 comes before "Alpha" from folder 2 because folder 1 has lower order.
        string folder2 = Path.Combine("C:", "Games");
        _fileSystem.CreateDirectory(folder2);
        _fileSystem.AddFile(Path.Combine(_shortcutFolder, "Zephyr.lnk"));
        _fileSystem.AddFile(Path.Combine(folder2, "Alpha.lnk"));

        var settingsService = CreateSettingsServiceWithFolders(
            (_shortcutFolder, "Shortcuts", 0),
            (folder2, "Games", 1));

        var vm = new MainViewModel(_shortcutService, _iconService, _imageFactory, _dispatcher,
            _appLauncher, _fileSystem, settingsService, _windowService);

        await vm.LoadAppsAsync();

        // Folder 1 (order 0) items come before folder 2 (order 1) items.
        // Within each single-item folder, alphabetical order trivially holds.
        Assert.Equal(2, vm.Apps.Count);
        Assert.Equal("Zephyr", vm.Apps[0].Name); // from Shortcuts (order 0)
        Assert.Equal("Alpha", vm.Apps[1].Name);  // from Games (order 1)
    }

    [Fact]
    public async Task LoadAppsAsync_AppliesCustomItemOrder()
    {
        // Arrange: two files in alphabetical order on disk; custom order reverses them
        string folderPath = _shortcutFolder;
        _fileSystem.AddFile(Path.Combine(folderPath, "B App.lnk"));
        _fileSystem.AddFile(Path.Combine(folderPath, "A App.lnk"));

        // Custom order: B before A (alphabetical would put A first)
        _settingsService.SetItemOrder(folderPath, ["B App.lnk", "A App.lnk"]);

        var vm = CreateViewModel();
        await vm.LoadAppsAsync();

        Assert.Equal(2, vm.Apps.Count);
        Assert.Equal("B App", vm.Apps[0].Name);
        Assert.Equal("A App", vm.Apps[1].Name);
    }

    [Fact]
    public void IsFilterEmpty_TrueWhenNoFilter()
    {
        var vm = CreateViewModel();
        Assert.True(vm.IsFilterEmpty);
    }

    [Fact]
    public void IsFilterEmpty_FalseWhenFilterSet()
    {
        var vm = CreateViewModel();
        vm.FilterText = "calc";
        Assert.False(vm.IsFilterEmpty);
    }

    [Fact]
    public async Task PersistItemOrder_PersistsOrderAndSyncsApps()
    {
        // Arrange: two items loaded in alphabetical order
        _fileSystem.AddFile(Path.Combine(_shortcutFolder, "A App.lnk"));
        _fileSystem.AddFile(Path.Combine(_shortcutFolder, "B App.lnk"));

        var vm = CreateViewModel();
        await vm.LoadAppsAsync();

        // Simulate DnD reorder: move B before A directly in FilteredApps
        var bItem = vm.FilteredApps.First(a => a.Name == "B App");
        var aItem = vm.FilteredApps.First(a => a.Name == "A App");
        vm.FilteredApps.Move(vm.FilteredApps.IndexOf(bItem), vm.FilteredApps.IndexOf(aItem));

        // Act
        vm.PersistItemOrder();

        // Persisted order reflects new arrangement
        var persisted = _settingsService.GetItemOrder(_shortcutFolder);
        Assert.Equal(["B App.lnk", "A App.lnk"], persisted);

        // Apps is synced to FilteredApps
        Assert.Equal(2, vm.Apps.Count);
        Assert.Equal("B App", vm.Apps[0].Name);
        Assert.Equal("A App", vm.Apps[1].Name);
    }

    [Fact]
    public async Task PersistItemOrder_DoesNotTriggerDoubleRebuild()
    {
        // Arrange
        _fileSystem.AddFile(Path.Combine(_shortcutFolder, "A App.lnk"));
        _fileSystem.AddFile(Path.Combine(_shortcutFolder, "B App.lnk"));

        var vm = CreateViewModel();
        await vm.LoadAppsAsync();

        int filterRebuildCount = 0;
        vm.FilteredApps.CollectionChanged += (_, _) => filterRebuildCount++;

        // Act: Apps.ReplaceAll in PersistItemOrder must not cascade into a second FilteredApps.ReplaceAll
        vm.PersistItemOrder();

        Assert.Equal(0, filterRebuildCount);
    }

    [Fact]
    public async Task PersistItemOrder_GroupedMode_ReadsFromGroupedAppsNotFilteredApps()
    {
        // Bug: PersistItemOrder always read from FilteredApps. In Grouped view, WinUI mutates
        // the per-group AppItemGroup collection during drag — FilteredApps is never touched.
        // The stale order in FilteredApps was written to the settings store, discarding the reorder.
        _fileSystem.AddFile(Path.Combine(_shortcutFolder, "A App.lnk"));
        _fileSystem.AddFile(Path.Combine(_shortcutFolder, "B App.lnk"));

        _settingsService.FolderViewMode = FolderViewMode.Grouped;
        var vm = CreateViewModel();
        await vm.LoadAppsAsync();

        // Simulate a stale FilteredApps with a different order than GroupedApps (as if a drag
        // happened in merged view but not in grouped view).
        var aItem = vm.FilteredApps.First(a => a.Name == "A App");
        var bItem = vm.FilteredApps.First(a => a.Name == "B App");
        vm.FilteredApps.Move(vm.FilteredApps.IndexOf(aItem), vm.FilteredApps.IndexOf(bItem));
        // FilteredApps is now [B App, A App]; GroupedApps[0].AllItems is still [A App, B App]

        vm.PersistItemOrder();

        // Must persist from GroupedApps (A first, B second), not the stale FilteredApps order
        var persisted = _settingsService.GetItemOrder(_shortcutFolder);
        Assert.Equal(["A App.lnk", "B App.lnk"], persisted);
    }

    [Fact]
    public async Task PersistItemOrderExecuteCommand_CatchesExceptionAndLogs_WhenManagerThrows()
    {
        _fileSystem.AddFile(Path.Combine(_shortcutFolder, "A App.lnk"));

        var vm = CreateViewModel();
        await vm.LoadAppsAsync();
        _settingsStore.ShouldThrow = true; // Force a failure when persisting

        using var stringWriter = new System.IO.StringWriter();
        var listener = new System.Diagnostics.TextWriterTraceListener(stringWriter);
        System.Diagnostics.Trace.Listeners.Add(listener);

        try
        {
            // Act
            var ex = Record.Exception(() => vm.PersistItemOrderExecuteCommand.Execute(null));

            // Verify exception is caught and logged, not propagated
            Assert.Null(ex);
            listener.Flush();
            Assert.Contains("Failed to persist item order", stringWriter.ToString());
        }
        finally
        {
            System.Diagnostics.Trace.Listeners.Remove(listener);
        }
    }

    // --- TOGGLE GROUP / CLEAR FILTER TESTS ---

    [Fact]
    public async Task ToggleGroupCommand_CollapsesGroup()
    {
        _fileSystem.AddFile(Path.Combine(_shortcutFolder, "Alpha.lnk"));
        _settingsService.CollapsibleGroups = true;
        var vm = CreateViewModel();
        await vm.LoadAppsAsync();
        var group = vm.GroupedApps.First();

        vm.ToggleGroupCommand.Execute(group);

        Assert.True(group.IsCollapsed);
    }

    [Fact]
    public async Task ToggleGroupCommand_ExpandsCollapsedGroup()
    {
        _fileSystem.AddFile(Path.Combine(_shortcutFolder, "Alpha.lnk"));
        _settingsService.CollapsibleGroups = true;
        var vm = CreateViewModel();
        await vm.LoadAppsAsync();
        var group = vm.GroupedApps.First();
        group.IsCollapsed = true;

        vm.ToggleGroupCommand.Execute(group);

        Assert.False(group.IsCollapsed);
        Assert.Single(group); // item restored
        Assert.Equal("Alpha", group[0].Name);
    }

    [Fact]
    public async Task ToggleGroupCommand_NoOp_WhenCollapsibleGroupsDisabled()
    {
        _fileSystem.AddFile(Path.Combine(_shortcutFolder, "Alpha.lnk"));
        _settingsService.CollapsibleGroups = false;
        var vm = CreateViewModel();
        await vm.LoadAppsAsync();
        var group = vm.GroupedApps.First();

        vm.ToggleGroupCommand.Execute(group);

        Assert.False(group.IsCollapsed);
        Assert.Single(group); // item unchanged
    }

    [Fact]
    public async Task ClearFilter_RestoresFullItemSet()
    {
        _fileSystem.AddFile(Path.Combine(_shortcutFolder, "Alpha.lnk"));
        _fileSystem.AddFile(Path.Combine(_shortcutFolder, "Beta.lnk"));
        var vm = CreateViewModel();
        await vm.LoadAppsAsync();
        vm.FilterText = "alp";
        Assert.Single(vm.FilteredApps);

        vm.ClearFilter();

        Assert.Equal(2, vm.FilteredApps.Count);
        Assert.Equal(string.Empty, vm.FilterText);
    }

    [Fact]
    public async Task ClearFilter_PreservesGroupCollapseState()
    {
        // Collapsed group stays collapsed after filter is cleared — only the filter changes
        _fileSystem.AddFile(Path.Combine(_shortcutFolder, "Alpha.lnk"));
        _fileSystem.AddFile(Path.Combine(_shortcutFolder, "Beta.lnk"));
        _settingsService.CollapsibleGroups = true;
        var vm = CreateViewModel();
        await vm.LoadAppsAsync();
        var group = vm.GroupedApps.First();
        group.IsCollapsed = true;
        vm.FilterText = "alp"; // hide group entirely? no — "alp" matches Alpha so placeholder stays
        Assert.Single(group);  // placeholder

        vm.ClearFilter();

        // Group stays collapsed; placeholder remains; filter text is empty
        Assert.True(group.IsCollapsed);
        Assert.Single(group);
        Assert.Equal(string.Empty, group[0].Name); // placeholder sentinel
        Assert.Equal(string.Empty, vm.FilterText);
    }

    // --- WATCHER INTEGRATION TESTS ---

    [Fact]
    public async Task FileWatcherCallback_TriggersReload_AndPicksUpNewFile()
    {
        // Arrange: initial load with one file
        _fileSystem.AddFile(Path.Combine(_shortcutFolder, "Alpha.lnk"));
        var vm = CreateViewModel();
        await vm.LoadAppsAsync();
        Assert.Single(vm.FilteredApps);

        // Add a second file AFTER the initial load so the watcher reload picks it up
        _fileSystem.AddFile(Path.Combine(_shortcutFolder, "Beta.lnk"));

        var reloadTcs = new TaskCompletionSource();
        vm.FilteredApps.CollectionChanged += (_, _) =>
        {
            // Guard on Count so a Reset/clear event before the Add does not resolve the TCS early.
            if (vm.FilteredApps.Count == 2)
                reloadTcs.TrySetResult();
        };

        // Act: simulate a directory change event — fires the registered watcher callback
        // which calls _ = LoadAppsAsync() internally
        _fileSystem.SimulateChange(_shortcutFolder);

        await reloadTcs.Task.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);

        // Assert: both files are visible after the watcher-triggered reload
        Assert.Equal(2, vm.FilteredApps.Count);
    }
}
