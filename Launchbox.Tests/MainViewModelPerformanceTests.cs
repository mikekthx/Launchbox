using Launchbox.Services;
using Launchbox.ViewModels;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Launchbox.Tests;

[Trait("Category", "Performance")]
public class MainViewModelPerformanceTests
{
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

        var settingsStore = new MockSettingsStore();
        var settingsService = new SettingsService(settingsStore, new MockStartupService());
        settingsService.ShortcutsPath = shortcutFolder;

        var shortcutService = new ShortcutService(fileSystem);
        var iconService = new IconService(fileSystem);

        var vm = new MainViewModel(
            shortcutService,
            iconService,
            new MockImageFactory(),
            new MockDispatcher(),
            new MockAppLauncher(),
            fileSystem,
            settingsService,
            new MockWindowService());

        var sw = Stopwatch.StartNew();
        await vm.LoadAppsAsync();
        sw.Stop();

        Assert.True(sw.Elapsed.TotalSeconds < 2.0,
            $"LoadAppsAsync took {sw.Elapsed.TotalSeconds:F2}s — expected < 2.0s");
        Assert.Equal(500, vm.Apps.Count);
    }
}
