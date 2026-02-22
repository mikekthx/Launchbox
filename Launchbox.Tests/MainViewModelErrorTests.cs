using Launchbox.Services;
using Launchbox.ViewModels;
using System;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Launchbox.Tests;

public class MainViewModelErrorTests
{
    private readonly MockShortcutService _mockShortcutService;
    private readonly MockIconService _mockIconService;
    private readonly MockImageFactory _imageFactory;
    private readonly MockDispatcher _dispatcher;
    private readonly MockAppLauncher _appLauncher;
    private readonly MockFileSystem _fileSystem;
    private readonly SettingsService _settingsService;
    private readonly MockWindowService _windowService;

    public MainViewModelErrorTests()
    {
        _mockShortcutService = new MockShortcutService();
        _mockIconService = new MockIconService();
        _imageFactory = new MockImageFactory();
        _dispatcher = new MockDispatcher();
        _appLauncher = new MockAppLauncher();
        _fileSystem = new MockFileSystem();
        _windowService = new MockWindowService();

        var settingsStore = new MockSettingsStore();
        var startupService = new MockStartupService();
        _settingsService = new SettingsService(settingsStore, startupService);
    }

    private MainViewModel CreateViewModel()
    {
        return new MainViewModel(
            _mockShortcutService,
            _mockIconService,
            _imageFactory,
            _dispatcher,
            _appLauncher,
            _fileSystem,
            _settingsService,
            _windowService);
    }

    [Fact]
    public async Task LoadAppsAsync_WhenIconExtractionFails_LogsErrorAndContinues()
    {
        // Arrange
        var app1 = @"C:\Apps\App1.lnk";
        var app2 = @"C:\Apps\App2.lnk";
        var files = new[] { app1, app2 };
        _mockShortcutService.SetFiles(files);

        // App1 has icon
        _mockIconService.AddIcon(app1, [1, 2, 3]);

        // App2 fails extraction
        _mockIconService.SetThrowOnExtract(app2);

        var viewModel = CreateViewModel();

        // Act
        await viewModel.LoadAppsAsync();

        // Assert
        Assert.Equal(2, viewModel.Apps.Count);

        var item1 = viewModel.Apps.FirstOrDefault(a => a.Path == app1);
        Assert.NotNull(item1);
        Assert.NotNull(item1.Icon); // Should have icon

        var item2 = viewModel.Apps.FirstOrDefault(a => a.Path == app2);
        Assert.NotNull(item2);
        Assert.Null(item2.Icon); // Should NOT have icon due to failure, but exists
    }
}
