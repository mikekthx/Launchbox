using Xunit;
using Launchbox;
using Launchbox.ViewModels;
using Launchbox.Services;
using Launchbox.Models;
using System.Threading.Tasks;
using System;
using System.IO;

namespace Launchbox.Tests;

public class MockImageFactory : IImageFactory
{
    public Task<object?> CreateImageAsync(byte[] imageBytes)
    {
        return Task.FromResult<object?>("MockImage");
    }
}

public class MockDispatcher : IDispatcher
{
    public void TryEnqueue(Action action)
    {
        action();
    }

    public Task EnqueueAsync(Func<Task> action)
    {
        return action();
    }
}

public class MockAppLauncher : IAppLauncher
{
    public string? LastLaunchedPath { get; private set; }
    public string? LastOpenedFolder { get; private set; }

    public void Launch(string path)
    {
        LastLaunchedPath = path;
    }

    public void OpenFolder(string path)
    {
        LastOpenedFolder = path;
    }
}

public class MainViewModelTests
{
    private readonly MockFileSystem _fileSystem;
    private readonly ShortcutService _shortcutService;
    private readonly IconService _iconService;
    private readonly MockImageFactory _imageFactory;
    private readonly MockDispatcher _dispatcher;
    private readonly MockAppLauncher _appLauncher;
    private readonly string _shortcutFolder = Path.Combine("C:", "Shortcuts");

    public MainViewModelTests()
    {
        _fileSystem = new MockFileSystem();
        _shortcutService = new ShortcutService(_fileSystem);
        _iconService = new IconService(_fileSystem);
        _imageFactory = new MockImageFactory();
        _dispatcher = new MockDispatcher();
        _appLauncher = new MockAppLauncher();

        _fileSystem.AddDirectory(_shortcutFolder);
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
        _fileSystem.AddDirectory(_shortcutFolder);

        viewModel.OpenShortcutsFolderCommand.Execute(null);

        Assert.Equal(_shortcutFolder, _appLauncher.LastOpenedFolder);
    }

    [Fact]
    public void OpenShortcutsFolderCommand_CreatesFolder_IfMissing()
    {
        var viewModel = CreateViewModel();
        // Ensure folder doesn't exist initially (though MockFileSystem starts empty except for what's added in Constructor)
        // In Constructor we added _shortcutFolder, so let's use a different one or clear it?
        // MockFileSystem doesn't have RemoveDirectory.

        // Let's create a NEW viewModel with a different path that doesn't exist
        string newPath = Path.Combine("C:", "NewShortcuts");
        var newViewModel = new MainViewModel(
            _shortcutService,
            _iconService,
            _imageFactory,
            _dispatcher,
            _appLauncher,
            _fileSystem,
            newPath);

        Assert.False(_fileSystem.DirectoryExists(newPath));

        newViewModel.OpenShortcutsFolderCommand.Execute(null);

        Assert.True(_fileSystem.DirectoryExists(newPath));
        Assert.Equal(newPath, _appLauncher.LastOpenedFolder);
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
            _shortcutFolder);
    }
}
