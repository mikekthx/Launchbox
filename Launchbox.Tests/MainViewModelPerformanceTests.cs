using Launchbox.Services;
using Launchbox.ViewModels;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Launchbox.Tests;

[Collection("Localization")]
[Trait("Category", "Performance")]
public class MainViewModelPerformanceTests
{
    private readonly ITestOutputHelper _output;

    public MainViewModelPerformanceTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task LoadAppsAsync_With500Files_CompletesWithinTwoSeconds()
    {
        var shortcutFolder = Path.Combine("C:", "Shortcuts");
        var fileSystem = new MockFileSystem();
        fileSystem.CreateDirectory(shortcutFolder);

        foreach (var i in Enumerable.Range(0, 500))
        {
            fileSystem.AddFile(Path.Combine(shortcutFolder, $"App{i}.lnk"));
        }

        // Pre-populate store so ShortcutFolderManager picks up the folder via legacy migration
        var settingsStore = new MockSettingsStore();
        settingsStore.SetValue("ShortcutsPath", shortcutFolder);
        var settingsService = new SettingsService(settingsStore, new MockStartupService(), new ShortcutFolderManager(settingsStore));

        var shortcutService = new ShortcutService(fileSystem);
        var iconService = new MockIconService();

        var vm = new MainViewModel(
            shortcutService,
            iconService,
            new MockImageFactory(),
            new MockDispatcher(),
            new MockAppLauncher(),
            fileSystem,
            settingsService,
            new MockWindowService());

        // Warmup pass to absorb JIT compilation cost
        await vm.LoadAppsAsync();

        var sw = Stopwatch.StartNew();
        await vm.LoadAppsAsync();
        sw.Stop();

        Assert.True(sw.Elapsed.TotalSeconds < 2.0,
            $"LoadAppsAsync took {sw.Elapsed.TotalSeconds:F2}s — expected < 2.0s");
        Assert.Equal(500, vm.Apps.Count);
        _output.WriteLine($"LoadAppsAsync with 500 files: {sw.Elapsed.TotalMilliseconds:F0}ms");
    }
}
