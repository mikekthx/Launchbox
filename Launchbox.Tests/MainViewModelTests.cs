using Launchbox.Helpers;
using Launchbox.Models;
using Launchbox.Services;
using Launchbox.ViewModels;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

        // Create SettingsService with MockStore
        var settingsStore = new MockSettingsStore();
        var startupService = new MockStartupService();
        _settingsService = new SettingsService(settingsStore, startupService);
        _settingsService.ShortcutsPath = _shortcutFolder;

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
        // Change settings to a path that doesn't exist
        string newPath = Path.Combine("C:", "NewShortcuts");
        _settingsService.ShortcutsPath = newPath;

        var viewModel = CreateViewModel();

        Assert.False(_fileSystem.DirectoryExists(newPath));

        viewModel.OpenShortcutsFolderCommand.Execute(null);

        Assert.True(_fileSystem.DirectoryExists(newPath));
        Assert.Equal(newPath, _appLauncher.LastOpenedFolder);
    }

    [Fact]
    public void OpenShortcutsFolderCommand_CatchesException_WhenDirectoryCreationFails()
    {
        // Arrange
        var throwingFileSystem = new ThrowingFileSystem();
        var viewModel = new MainViewModel(
            _shortcutService,
            _iconService,
            _imageFactory,
            _dispatcher,
            _appLauncher,
            throwingFileSystem,
            _settingsService,
            _windowService);

        string newPath = Path.Combine("C:", "NewShortcuts");
        _settingsService.ShortcutsPath = newPath;

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
}
