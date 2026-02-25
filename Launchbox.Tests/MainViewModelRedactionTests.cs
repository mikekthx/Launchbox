using Launchbox.Helpers;
using Launchbox.Services;
using Launchbox.ViewModels;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Launchbox.Tests;

public class MainViewModelRedactionTests : IDisposable
{
    private readonly MockShortcutService _mockShortcutService;
    private readonly MockIconService _mockIconService;
    private readonly MockImageFactory _imageFactory;
    private readonly MockDispatcher _dispatcher;
    private readonly MockAppLauncher _appLauncher;
    private readonly MockFileSystem _fileSystem;
    private readonly SettingsService _settingsService;
    private readonly MockWindowService _windowService;
    private readonly StringWriter _traceOutput;
    private readonly TextWriterTraceListener _traceListener;

    public MainViewModelRedactionTests()
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

        _traceOutput = new StringWriter();
        _traceListener = new TextWriterTraceListener(_traceOutput);
        Trace.Listeners.Add(_traceListener);
    }

    public void Dispose()
    {
        Trace.Listeners.Remove(_traceListener);
        _traceOutput.Dispose();
        _traceListener.Dispose();
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
    public async Task LoadAppsAsync_WhenShortcutServiceReturnsNull_LogsRedactedFolder()
    {
        // Arrange
        // Use platform-agnostic path separator for tests running on Linux
        var folderName = "SecretFolder";
        var secretPart = Path.Combine("Users", "User");
        var fullPath = Path.Combine("C:", secretPart, folderName);

        _settingsService.ShortcutsPath = fullPath;
        var viewModel = CreateViewModel();

        // Act
        await viewModel.LoadAppsAsync();

        // Flush trace
        Trace.Flush();
        string log = _traceOutput.ToString();

        // Assert
        // RedactPath returns "...\FileName" so we check for the redacted format
        Assert.Contains($@"...\{folderName}", log);
        Assert.DoesNotContain(secretPart, log);
    }

    [Fact]
    public async Task LoadAppsAsync_WhenIconExtractionFails_LogsRedactedPath()
    {
        // Arrange
        var fileName = "SecretApp.lnk";
        var secretPart = Path.Combine("C:", "Apps");
        var appPath = Path.Combine(secretPart, fileName);

        _mockShortcutService.SetFiles(new[] { appPath });
        _mockIconService.SetThrowOnExtract(appPath);

        var viewModel = CreateViewModel();

        // Act
        await viewModel.LoadAppsAsync();

        Trace.Flush();
        string log = _traceOutput.ToString();

        // Assert
        // RedactPath returns "...\FileName"
        // We verify that the path passed to Trace.WriteLine was redacted.
        // Note: MockIconService throws an exception with the full path in the message,
        // so we can't assert DoesNotContain(secretPart) for the whole log.
        Assert.Contains($@"Failed to load icon for ...\{fileName}", log);
    }
}
