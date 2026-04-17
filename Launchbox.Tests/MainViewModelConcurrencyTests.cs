using Launchbox.Helpers;
using Launchbox.Services;
using Launchbox.ViewModels;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Launchbox.Tests;

[Collection("Localization")]
public class MainViewModelConcurrencyTests
{
    private readonly MockFileSystem _fileSystem;
    private readonly MockIconService _iconService;
    private readonly MockImageFactory _imageFactory;
    private readonly MockDispatcher _dispatcher;
    private readonly MockAppLauncher _appLauncher;
    private readonly SettingsService _settingsService;
    private readonly MockWindowService _windowService;
    private readonly MockSettingsStore _settingsStore;
    private readonly string _shortcutFolder = Path.Combine("C:", "Shortcuts");

    public MainViewModelConcurrencyTests()
    {
        _fileSystem = new MockFileSystem();
        _iconService = new MockIconService();
        _imageFactory = new MockImageFactory();
        _dispatcher = new MockDispatcher();
        _appLauncher = new MockAppLauncher();
        _windowService = new MockWindowService();
        _settingsStore = new MockSettingsStore();
        _settingsStore.SetValue("ShortcutsPath", _shortcutFolder);
        _settingsService = new SettingsService(_settingsStore, new MockStartupService(), new ShortcutFolderManager(_settingsStore));
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

    private MainViewModel CreateViewModel(IShortcutService shortcutService) =>
        new(shortcutService, _iconService, _imageFactory, _dispatcher,
            _appLauncher, _fileSystem, _settingsService, _windowService);

    /// <summary>
    /// Shortcut service that blocks the first call on a semaphore gate,
    /// allowing tests to verify that a stale (superseded) load cannot overwrite
    /// a newer completed load.
    /// </summary>
    private sealed class GatedShortcutService : IShortcutService
    {
        private readonly SemaphoreSlim _gate;
        private readonly string[] _blockedCallFiles;
        private readonly string[] _fastCallFiles;
        private int _callCount;

        /// <summary>Signals when the first (blocking) call has started waiting on the gate.</summary>
        public TaskCompletionSource BlockingStarted { get; } = new();

        public GatedShortcutService(SemaphoreSlim gate, string[] blockedCallFiles, string[] fastCallFiles)
        {
            _gate = gate;
            _blockedCallFiles = blockedCallFiles;
            _fastCallFiles = fastCallFiles;
        }

        public string[]? GetShortcutFiles(string folderPath, IReadOnlyList<string> allowedExtensions, CancellationToken ct = default)
        {
            if (Interlocked.Increment(ref _callCount) == 1)
            {
                // Signal that we're about to block, then wait without observing the load's cancellation token.
                // The point of this test is that the CTS is cancelled WHILE we're blocked, and
                // ct.ThrowIfCancellationRequested() after we unblock discards our results.
                // TestContext.Current.CancellationToken is orthogonal — it lets the test runner reclaim
                // this thread-pool thread on timeout without affecting the race the test is modelling.
                BlockingStarted.TrySetResult();
                _gate.Wait(TestContext.Current.CancellationToken);
                return _blockedCallFiles;
            }
            return _fastCallFiles;
        }
    }

    [Fact]
    public async Task LoadAppsAsync_SupersededLoad_CannotOverwriteNewerUIState()
    {
        var gate = new SemaphoreSlim(0, 1);
        var gatedService = new GatedShortcutService(
            gate,
            blockedCallFiles: [$@"{_shortcutFolder}\Stale.lnk"],
            fastCallFiles: [$@"{_shortcutFolder}\Fresh.lnk"]);

        _fileSystem.AddFile($@"{_shortcutFolder}\Stale.lnk");
        _fileSystem.AddFile($@"{_shortcutFolder}\Fresh.lnk");

        var vm = CreateViewModel(gatedService);

        // Start Load #1 — it will block inside Task.Run waiting on the gate
        var load1 = vm.LoadAppsAsync();

        // Wait until Load #1 is actually blocking before firing Load #2
        await gatedService.BlockingStarted.Task.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);

        // Load #2 fires — this cancels Load #1's CTS and completes with the fresh file
        await vm.LoadAppsAsync();

        // Assert: UI reflects Load #2's result while Load #1 is still blocked
        Assert.Single(vm.FilteredApps);
        Assert.Equal("Fresh", vm.FilteredApps[0].Name);

        // Release Load #1's gate — it will unblock, then hit ct.ThrowIfCancellationRequested()
        gate.Release();
        await load1;

        // Assert: stale Load #1 result did not overwrite Load #2's UI state
        Assert.Single(vm.FilteredApps);
        Assert.Equal("Fresh", vm.FilteredApps[0].Name);
    }
}
