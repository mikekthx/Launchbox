using Launchbox.Helpers;
using Launchbox.Services;
using Xunit;

namespace Launchbox.Tests;

public class SettingsServiceTests
{
    [Fact]
    public void ShortcutsPath_ReturnsDefault_WhenNotSet()
    {
        var settingsStore = new MockSettingsStore();
        var startupService = new MockStartupService();
        var service = new SettingsService(settingsStore, startupService, new ShortcutFolderManager(settingsStore));

        var path = service.ShortcutsPath;

        Assert.Contains("Shortcuts", path);
    }

    [Fact]
    public async Task SetRunAtStartupAsync_Reverts_WhenEnableFails()
    {
        var settingsStore = new MockSettingsStore();
        var startupService = new MockStartupService { Success = false };
        var service = new SettingsService(settingsStore, startupService, new ShortcutFolderManager(settingsStore));

        await service.SetRunAtStartupAsync(true);

        Assert.False(service.IsRunAtStartup);
        Assert.False(startupService.IsEnabled);
    }

    [Fact]
    public void ShortcutsPath_ReflectsFirstFolder_WhenFolderAdded()
    {
        var settingsStore = new MockSettingsStore();
        var startupService = new MockStartupService();

        // Pre-populate store with the desired folder so it becomes order 0 (the first folder)
        var json = System.Text.Json.JsonSerializer.Serialize(new[]
        {
            new Launchbox.Models.ShortcutFolder { Path = @"C:\Test\Shortcuts", Label = "Shortcuts", Order = 0 }
        });
        settingsStore.SetValue("ShortcutFolders", json);

        var service = new SettingsService(settingsStore, startupService, new ShortcutFolderManager(settingsStore));

        Assert.Equal(@"C:\Test\Shortcuts", service.ShortcutsPath);
    }

    [Fact]
    public void Hotkey_ReturnsDefault_WhenNotSet()
    {
        var settingsStore = new MockSettingsStore();
        var startupService = new MockStartupService();
        var service = new SettingsService(settingsStore, startupService, new ShortcutFolderManager(settingsStore));

        Assert.Equal(Constants.MOD_ALT, service.HotkeyModifiers);
        Assert.Equal(Constants.VK_S, service.HotkeyKey);
    }

    [Fact]
    public void Hotkey_SavesAndRetrievesValues()
    {
        var settingsStore = new MockSettingsStore();
        var startupService = new MockStartupService();
        var service = new SettingsService(settingsStore, startupService, new ShortcutFolderManager(settingsStore));

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
        var service = new SettingsService(settingsStore, startupService, new ShortcutFolderManager(settingsStore));

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
        var service = new SettingsService(settingsStore, startupService, new ShortcutFolderManager(settingsStore));

        await service.InitializeAsync();

        Assert.True(service.IsRunAtStartup);
    }

    [Fact]
    public async Task InitializeAsync_HandlesException_WhenStartupServiceFails()
    {
        var settingsStore = new MockSettingsStore();
        var startupService = new MockStartupService { ShouldFail = true };
        var service = new SettingsService(settingsStore, startupService, new ShortcutFolderManager(settingsStore));

        // This should not throw if the issue is fixed
        var exception = await Record.ExceptionAsync(() => service.InitializeAsync());
        Assert.Null(exception);
        Assert.False(service.IsRunAtStartup);
    }

    [Fact]
    public async Task InitializeAsync_LogsError_WhenStartupServiceFails()
    {
        using var sw = new System.IO.StringWriter();
        using var listener = new System.Diagnostics.TextWriterTraceListener(sw);
        System.Diagnostics.Trace.Listeners.Add(listener);

        try
        {
            var settingsStore = new MockSettingsStore();
            var startupService = new MockStartupService { ShouldFail = true };
            var service = new SettingsService(settingsStore, startupService, new ShortcutFolderManager(settingsStore));

            await service.InitializeAsync();

            System.Diagnostics.Trace.Flush();
            string output = sw.ToString();

            Assert.Contains("Failed to initialize settings (StartupService):", output);
        }
        finally
        {
            System.Diagnostics.Trace.Listeners.Remove(listener);
        }
    }

    [Theory]
    [InlineData(@"\\attacker\share")]
    [InlineData(@"\\?\UNC\attacker\share")]
    [InlineData(@"//attacker/share")]
    public void ShortcutsPath_IgnoresUnsafeFolderInManager(string unsafePath)
    {
        var settingsStore = new MockSettingsStore();
        var startupService = new MockStartupService();

        // Inject unsafe path before constructing manager so it's loaded from tampered store
        settingsStore.SetValue("ShortcutFolders", $"[{{\"Order\":0,\"Path\":\"{unsafePath.Replace("\\", "\\\\")}\",\"Label\":\"Test\"}}]");

        var service = new SettingsService(settingsStore, startupService, new ShortcutFolderManager(settingsStore));

        var path = service.ShortcutsPath;

        Assert.NotEqual(unsafePath, path);
        Assert.Contains("Shortcuts", path);
    }

    [Fact]
    public void GetItemOrder_ReturnsEmpty_WhenNotSet()
    {
        var svc = new SettingsService(
            new MockSettingsStore(),
            new MockStartupService(),
            new ShortcutFolderManager(new MockSettingsStore()));

        var order = svc.GetItemOrder(@"C:\Desktop\Shortcuts");

        Assert.Empty(order);
    }

    [Fact]
    public void SetItemOrder_PersistsAndRetrievesOrder()
    {
        var store = new MockSettingsStore();
        var svc = new SettingsService(
            store,
            new MockStartupService(),
            new ShortcutFolderManager(new MockSettingsStore()));

        var names = new List<string> { "Notepad.lnk", "Calculator.lnk", "Paint.lnk" };
        var result = svc.SetItemOrder(@"C:\Desktop\Shortcuts", names);

        Assert.True(result);
        var retrieved = svc.GetItemOrder(@"C:\Desktop\Shortcuts");
        Assert.Equal(names, retrieved);
    }

    [Fact]
    public void SetItemOrder_PreservesOtherFolderOrders()
    {
        var store = new MockSettingsStore();
        var svc = new SettingsService(
            store,
            new MockStartupService(),
            new ShortcutFolderManager(new MockSettingsStore()));

        svc.SetItemOrder(@"C:\FolderA", ["A1.lnk", "A2.lnk"]);
        svc.SetItemOrder(@"C:\FolderB", ["B1.lnk", "B2.lnk"]);

        Assert.Equal(["A1.lnk", "A2.lnk"], svc.GetItemOrder(@"C:\FolderA"));
        Assert.Equal(["B1.lnk", "B2.lnk"], svc.GetItemOrder(@"C:\FolderB"));
    }

    [Fact]
    public void GetItemOrder_ReturnsCaseInsensitiveMatch()
    {
        var store = new MockSettingsStore();
        var svc = new SettingsService(
            store,
            new MockStartupService(),
            new ShortcutFolderManager(new MockSettingsStore()));

        svc.SetItemOrder(@"C:\Desktop\Shortcuts", ["Notepad.lnk"]);

        var order = svc.GetItemOrder(@"c:\desktop\shortcuts");
        Assert.Equal(["Notepad.lnk"], order);
    }
}
