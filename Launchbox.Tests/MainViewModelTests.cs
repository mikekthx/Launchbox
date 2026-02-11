using Xunit;
using Launchbox;
using Launchbox.ViewModels;
using Launchbox.Services;
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
}

public class MockAppLauncher : IAppLauncher
{
    public string? LastLaunchedPath { get; private set; }

    public void Launch(string path)
    {
        LastLaunchedPath = path;
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
        // LoadAppsCommand executes LoadAppsAsync
        if (viewModel.LoadAppsCommand.CanExecute(null))
        {
             viewModel.LoadAppsCommand.Execute(null);
        }

        // Wait for async operations to complete
        // Since LoadAppsAsync is async void, we have to wait a bit
        await Task.Delay(500);

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

    private MainViewModel CreateViewModel()
    {
        return new MainViewModel(
            _shortcutService,
            _iconService,
            _imageFactory,
            _dispatcher,
            _appLauncher,
            _shortcutFolder);
    }
}
