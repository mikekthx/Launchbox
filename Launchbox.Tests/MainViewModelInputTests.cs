using Launchbox.ViewModels;
using Launchbox.Models;
using Launchbox.Services;
using Windows.System;
using System.IO;
using System.Threading.Tasks;
using System.Text.Json;
using Xunit;

namespace Launchbox.Tests;

public class MainViewModelInputTests
{
    private readonly MockFileSystem _fileSystem;
    private readonly MockShortcutService _shortcutService;
    private readonly MockIconService _iconService;
    private readonly MockImageFactory _imageFactory;
    private readonly MockAppLauncher _appLauncher;
    private readonly MockDispatcher _dispatcher;
    private readonly SettingsService _settingsService;
    private readonly MockWindowService _windowService;
    private readonly string _shortcutFolder;
    private readonly MockSettingsStore _settingsStore;

    public MainViewModelInputTests()
    {
        _shortcutFolder = Path.Combine("C:", "TestShortcuts");
        _fileSystem = new MockFileSystem();
        _fileSystem.CreateDirectory(_shortcutFolder);

        _shortcutService = new MockShortcutService();
        _iconService = new MockIconService();
        _imageFactory = new MockImageFactory();
        _appLauncher = new MockAppLauncher();
        _dispatcher = new MockDispatcher();
        _windowService = new MockWindowService();

        _settingsStore = new MockSettingsStore();
        _settingsStore.SetValue("ShortcutFolders", JsonSerializer.Serialize([new { Path = _shortcutFolder, Label = "Apps", Order = 0 }]));
        _settingsService = new SettingsService(_settingsStore, new MockStartupService(), new ShortcutFolderManager(_settingsStore));
    }

    private MainViewModel CreateViewModel()
    {
        return new MainViewModel(_shortcutService, _iconService, _imageFactory, _dispatcher, _appLauncher, _fileSystem, _settingsService, _windowService);
    }

    [Fact]
    public async Task SearchBoxKeyDown_EnterWithSelectedItem_LaunchesApp()
    {
        _shortcutService.SetFiles([Path.Combine(_shortcutFolder, "Alpha.lnk")]);
        var vm = CreateViewModel();
        await vm.LoadAppsAsync();

        vm.SelectedItem = vm.FilteredApps[0];

        vm.SearchBoxKeyDownCommand.Execute(VirtualKey.Enter);

        Assert.Equal(Path.Combine(_shortcutFolder, "Alpha.lnk"), _appLauncher.LastLaunchedPath);
    }

    [Fact]
    public void SearchBoxKeyDown_EnterWithNoSelectedItem_DoesNothing()
    {
        var vm = CreateViewModel();
        vm.SelectedItem = null;

        vm.SearchBoxKeyDownCommand.Execute(VirtualKey.Enter);

        Assert.Null(_appLauncher.LastLaunchedPath);
    }

    [Fact]
    public void SearchBoxKeyDown_Down_FiresSearchBoxDownKeyPressed()
    {
        var vm = CreateViewModel();
        bool eventFired = false;
        vm.SearchBoxDownKeyPressed += (s, e) => eventFired = true;

        vm.SearchBoxKeyDownCommand.Execute(VirtualKey.Down);

        Assert.True(eventFired);
    }
}
