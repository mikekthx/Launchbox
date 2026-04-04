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

    [Fact]
    public void GetItemOrder_ReturnsCaseInsensitiveMatch_AfterRoundTrip()
    {
        // Bug: DeserializeItemOrders used a case-sensitive dictionary, so a casing mismatch
        // after loading from the store (simulating a restart) lost the persisted order.
        var store = new MockSettingsStore();
        var svc1 = new SettingsService(store, new MockStartupService(), new ShortcutFolderManager(new MockSettingsStore()));
        svc1.SetItemOrder(@"C:\Desktop\Shortcuts", ["Notepad.lnk", "Calc.lnk"]);

        // New service instance loads from the same store (simulates app restart)
        var svc2 = new SettingsService(store, new MockStartupService(), new ShortcutFolderManager(new MockSettingsStore()));

        // Look up with different casing — must still return the persisted order
        var order = svc2.GetItemOrder(@"c:\desktop\shortcuts");
        Assert.Equal(["Notepad.lnk", "Calc.lnk"], order);
    }

    [Fact]
    public void SetItemOrders_WritesAllFoldersInOneCall()
    {
        var store = new MockSettingsStore();
        var svc = new SettingsService(
            store,
            new MockStartupService(),
            new ShortcutFolderManager(new MockSettingsStore()));

        var orders = new Dictionary<string, List<string>>
        {
            [@"C:\FolderA"] = ["A2.lnk", "A1.lnk"],
            [@"C:\FolderB"] = ["B1.lnk"],
        };

        var result = svc.SetItemOrders(orders);

        Assert.True(result);
        Assert.Equal(["A2.lnk", "A1.lnk"], svc.GetItemOrder(@"C:\FolderA"));
        Assert.Equal(["B1.lnk"], svc.GetItemOrder(@"C:\FolderB"));
    }

    [Fact]
    public void MergeItemOrders_PreservesAbsentFolderOrders()
    {
        var store = new MockSettingsStore();
        var svc = new SettingsService(
            store,
            new MockStartupService(),
            new ShortcutFolderManager(new MockSettingsStore()));

        // Pre-save orders for two folders
        svc.SetItemOrders(new Dictionary<string, List<string>>
        {
            [@"C:\FolderA"] = ["A1.lnk", "A2.lnk"],
            [@"C:\FolderB"] = ["B1.lnk", "B2.lnk"],
        });

        // Merge update only touches FolderA (FolderB is unavailable/empty)
        var result = svc.MergeItemOrders(new Dictionary<string, List<string>>
        {
            [@"C:\FolderA"] = ["A2.lnk", "A1.lnk"],
        });

        Assert.True(result);
        Assert.Equal(["A2.lnk", "A1.lnk"], svc.GetItemOrder(@"C:\FolderA"));
        // FolderB's saved order is preserved, not erased
        Assert.Equal(["B1.lnk", "B2.lnk"], svc.GetItemOrder(@"C:\FolderB"));
    }

    [Fact]
    public void PersistItemOrders_ReturnsFalse_WhenSizeExceedsLimit()
    {
        var store = new MockSettingsStore();
        var svc = new SettingsService(
            store,
            new MockStartupService(),
            new ShortcutFolderManager(new MockSettingsStore()));

        // Build a dict large enough to exceed the 7168-byte limit
        var hugeNames = Enumerable.Range(0, 500).Select(i => $"VeryLongShortcutName_{i:D4}.lnk").ToList();
        var orders = new Dictionary<string, List<string>>
        {
            [@"C:\Shortcuts"] = hugeNames,
        };

        var result = svc.SetItemOrders(orders);

        Assert.False(result);
        // Store was not written — existing orders are unchanged
        Assert.Empty(svc.GetItemOrder(@"C:\Shortcuts"));
    }

    [Theory]
    [InlineData(nameof(SettingsService.KeepCentered))]
    [InlineData(nameof(SettingsService.CollapsibleGroups))]
    [InlineData(nameof(SettingsService.FolderViewMode))]
    [InlineData(nameof(SettingsService.GridSize))]
    [InlineData(nameof(SettingsService.HotkeyModifiers))]
    [InlineData(nameof(SettingsService.HotkeyKey))]
    public void PropertySetter_FiresPropertyChanged_EvenOnWriteFailure(string propertyName)
    {
        // Bug: setters used `if (X != value && store.SetValue(...)) { OnPropertyChanged(); }`
        // so a failed write silently left the UI out of sync with the persisted value.
        var store = new MockSettingsStore();
        store.FailSetValueKeys.Add(propertyName);
        var svc = new SettingsService(store, new MockStartupService(), new ShortcutFolderManager(new MockSettingsStore()));

        bool notified = false;
        svc.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == propertyName) notified = true;
        };

        // Set a non-default value for each property type so the equality guard is bypassed
        switch (propertyName)
        {
            case nameof(SettingsService.KeepCentered): svc.KeepCentered = true; break;
            case nameof(SettingsService.CollapsibleGroups): svc.CollapsibleGroups = false; break;
            case nameof(SettingsService.FolderViewMode): svc.FolderViewMode = Launchbox.Models.FolderViewMode.Grouped; break;
            case nameof(SettingsService.GridSize): svc.GridSize = Launchbox.Helpers.GridSize.Large; break;
            case nameof(SettingsService.HotkeyModifiers): svc.HotkeyModifiers = Constants.MOD_CONTROL; break;
            case nameof(SettingsService.HotkeyKey): svc.HotkeyKey = (int)'A'; break;
        }

        Assert.True(notified); // binding must be notified so it can revert
    }

    [Fact]
    public async Task SetRunAtStartupAsync_Disable_RevertsAndDoesNotThrow_WhenDisableFails()
    {
        var settingsStore = new MockSettingsStore();
        var startupService = new MockStartupService { IsEnabled = false };
        var service = new SettingsService(settingsStore, startupService, new ShortcutFolderManager(settingsStore));

        // Seed: enable startup so IsRunAtStartup = true
        await service.SetRunAtStartupAsync(true);
        Assert.True(service.IsRunAtStartup);

        // Now make the OS throw on the next call
        startupService.ShouldFail = true;

        // Setting to false triggers DisableStartupAsync which will throw
        var ex = await Record.ExceptionAsync(() => service.SetRunAtStartupAsync(false));

        Assert.Null(ex);
        // UI toggle must revert — the disable failed, so startup is still enabled
        Assert.True(service.IsRunAtStartup);
    }
}
