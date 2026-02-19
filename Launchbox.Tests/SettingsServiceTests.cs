using System.Threading.Tasks;
using Xunit;
using Launchbox.Helpers;
using Launchbox.Services;

namespace Launchbox.Tests;

public class SettingsServiceTests
{
    [Fact]
    public void ShortcutsPath_ReturnsDefault_WhenNotSet()
    {
        var settingsStore = new MockSettingsStore();
        var startupService = new MockStartupService();
        var service = new SettingsService(settingsStore, startupService);

        var path = service.ShortcutsPath;

        Assert.Contains("Shortcuts", path);
    }

    [Fact]
    public void ShortcutsPath_SavesAndRetrievesValue()
    {
        var settingsStore = new MockSettingsStore();
        var startupService = new MockStartupService();
        var service = new SettingsService(settingsStore, startupService);

        service.ShortcutsPath = @"C:\Test\Shortcuts";

        Assert.Equal(@"C:\Test\Shortcuts", service.ShortcutsPath);
        Assert.True(settingsStore.TryGetValue("ShortcutsPath", out var val));
        Assert.Equal(@"C:\Test\Shortcuts", val);
    }

    [Fact]
    public void Hotkey_ReturnsDefault_WhenNotSet()
    {
        var settingsStore = new MockSettingsStore();
        var startupService = new MockStartupService();
        var service = new SettingsService(settingsStore, startupService);

        Assert.Equal(Constants.MOD_ALT, service.HotkeyModifiers);
        Assert.Equal(Constants.VK_S, service.HotkeyKey);
    }

    [Fact]
    public void Hotkey_SavesAndRetrievesValues()
    {
        var settingsStore = new MockSettingsStore();
        var startupService = new MockStartupService();
        var service = new SettingsService(settingsStore, startupService);

        service.HotkeyModifiers = Constants.MOD_CONTROL;
        service.HotkeyKey = (int)'K';

        Assert.Equal(Constants.MOD_CONTROL, service.HotkeyModifiers);
        Assert.Equal((int)'K', service.HotkeyKey);
    }

    [Fact]
    public async Task SetRunAtStartupAsync_UpdatesServiceAndProperty()
    {
        var settingsStore = new MockSettingsStore();
        var startupService = new MockStartupService();
        var service = new SettingsService(settingsStore, startupService);

        await service.SetRunAtStartupAsync(true);

        Assert.True(service.IsRunAtStartup);
        Assert.True(startupService.IsEnabled);

        await service.SetRunAtStartupAsync(false);

        Assert.False(service.IsRunAtStartup);
        Assert.False(startupService.IsEnabled);
    }

    [Fact]
    public async Task InitializeAsync_LoadsStartupState()
    {
        var settingsStore = new MockSettingsStore();
        var startupService = new MockStartupService { IsEnabled = true };
        var service = new SettingsService(settingsStore, startupService);

        await service.InitializeAsync();

        Assert.True(service.IsRunAtStartup);
    }
}
