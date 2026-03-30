using Launchbox.Helpers;
using Launchbox.Models;
using Launchbox.Services;
using Launchbox.ViewModels;
using System;
using System.Collections.Specialized;
using System.Linq;
using System.Threading.Tasks;
using Windows.System;
using Xunit;

namespace Launchbox.Tests;

[Collection("Localization")]
public class SettingsViewModelTests
{
    private (SettingsService, MockStartupService, MockFilePickerService, SettingsViewModel) CreateViewModel()
    {
        Localization.SetProvider(new MockStringProvider(new()
        {
            { "Modifier_Alt", "Alt" },
            { "Modifier_Ctrl", "Ctrl" },
            { "Modifier_Shift", "Shift" },
            { "Modifier_Win", "Win" },
            { "GridSize_Small", "Small" },
            { "GridSize_Medium", "Medium" },
            { "GridSize_Large", "Large" },
        }));
        var settingsStore = new MockSettingsStore();
        var startupService = new MockStartupService();
        var settingsService = new SettingsService(settingsStore, startupService, new ShortcutFolderManager(settingsStore));
        var pickerService = new MockFilePickerService();
        var windowService = new MockWindowService();

        var viewModel = new SettingsViewModel(settingsService, windowService, pickerService);

        return (settingsService, startupService, pickerService, viewModel);
    }


    [Fact]
    public async Task RunAtStartup_UpdatesServiceAsync()
    {
        var (service, startup, _, vm) = CreateViewModel();

        var tcs = new TaskCompletionSource();
        service.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(SettingsService.IsRunAtStartup))
                tcs.TrySetResult();
        };

        vm.RunAtStartup = true;

        await tcs.Task.WaitAsync(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);

        Assert.True(service.IsRunAtStartup);
        Assert.True(startup.IsEnabled);
    }

    [Fact]
    public void SelectedModifier_ConvertsToConstants()
    {
        var (service, _, _, vm) = CreateViewModel();

        vm.SelectedModifier = vm.Modifiers.First(o => o.Value == "Ctrl");
        Assert.Equal(Constants.MOD_CONTROL, service.HotkeyModifiers);

        vm.SelectedModifier = vm.Modifiers.First(o => o.Value == "Win");
        Assert.Equal(Constants.MOD_WIN, service.HotkeyModifiers);

        vm.SelectedModifier = vm.Modifiers.First(o => o.Value == "Alt");
        Assert.Equal(Constants.MOD_ALT, service.HotkeyModifiers);
    }

    [Theory]
    [InlineData("k", (int)VirtualKey.K)]
    [InlineData("5", (int)VirtualKey.Number5)]
    [InlineData("F1", (int)VirtualKey.F1)]
    [InlineData("Enter", (int)VirtualKey.Enter)]
    [InlineData("Home", (int)VirtualKey.Home)]
    public void HotkeyKeyString_ValidatesAndUpdates(string input, int expectedKey)
    {
        var (service, _, _, vm) = CreateViewModel();

        vm.HotkeyKeyString = input;
        Assert.Equal(expectedKey, service.HotkeyKey);
    }

    [Fact]
    public void HotkeyKeyString_RejectsInvalidInput()
    {
        var (service, _, _, vm) = CreateViewModel();
        var oldKey = service.HotkeyKey;

        // Invalid input
        vm.HotkeyKeyString = "InvalidKeyName";
        Assert.Equal(oldKey, service.HotkeyKey);

        // '?' corresponds to no standard virtual key for hotkeys
        vm.HotkeyKeyString = "?";
        Assert.Equal(oldKey, service.HotkeyKey);

        // '$' (ASCII 36) maps to VirtualKey.Home (36), but Enum.TryParse("$") fails because "$" is not a valid enum name.
        // The char.IsLetterOrDigit check prevents it from taking the single-char fallback path where (VirtualKey)'$' would be valid.
        vm.HotkeyKeyString = "$";
        Assert.Equal(oldKey, service.HotkeyKey);
    }

    [Fact]
    public void HotkeyKeyString_RejectsNumericEnumValues()
    {
        var (service, _, _, vm) = CreateViewModel();

        int originalKey = service.HotkeyKey;

        // "36" is the integer value for VirtualKey.Home — reject to prevent
        // Enum.TryParse from silently mapping numeric strings to unexpected keys
        vm.HotkeyKeyString = "36";
        Assert.Equal(originalKey, service.HotkeyKey);
    }

    [Fact]
    public async Task AddFolderCommand_AddsFolder_WhenFolderSelected()
    {
        var (_, _, picker, vm) = CreateViewModel();
        picker.SelectedFolder = @"C:\PickedFolder";

        var tcs = new TaskCompletionSource();
        vm.Folders.CollectionChanged += (s, e) => tcs.TrySetResult();

        vm.AddFolderCommand.Execute(new object());

        await tcs.Task.WaitAsync(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);

        Assert.Contains(vm.Folders, f => f.Path == @"C:\PickedFolder");
    }

    [Fact]
    public async Task AddFolderCommand_DoesNothing_WhenCancelled()
    {
        var (_, _, picker, vm) = CreateViewModel();
        picker.SelectedFolder = null; // Cancelled
        var initialCount = vm.Folders.Count;

        vm.AddFolderCommand.Execute(new object());

        // Wait briefly to ensure it didn't change
        await Task.Delay(50, TestContext.Current.CancellationToken);

        Assert.Equal(initialCount, vm.Folders.Count);
    }

    [Fact]
    public void SelectedGridSize_Default_IsMedium()
    {
        Localization.SetProvider(new MockStringProvider(new()
        {
            { "GridSize_Small", "Small" }, { "GridSize_Medium", "Medium" }, { "GridSize_Large", "Large" },
            { "Modifier_Alt", "Alt" }, { "Modifier_Ctrl", "Ctrl" }, { "Modifier_Shift", "Shift" }, { "Modifier_Win", "Win" },
        }));
        var store = new MockSettingsStore();
        var settingsService = new SettingsService(store, new MockStartupService(), new ShortcutFolderManager(store));
        var vm = new SettingsViewModel(settingsService, new MockWindowService(), new MockFilePickerService());
        Assert.Equal("Medium", vm.SelectedGridSize.Value);
    }

    [Fact]
    public void SelectedGridSize_WhenSet_UpdatesSettingsService()
    {
        Localization.SetProvider(new MockStringProvider(new()
        {
            { "GridSize_Small", "Small" }, { "GridSize_Medium", "Medium" }, { "GridSize_Large", "Large" },
            { "Modifier_Alt", "Alt" }, { "Modifier_Ctrl", "Ctrl" }, { "Modifier_Shift", "Shift" }, { "Modifier_Win", "Win" },
        }));
        var store = new MockSettingsStore();
        var settingsService = new SettingsService(store, new MockStartupService(), new ShortcutFolderManager(store));
        var vm = new SettingsViewModel(settingsService, new MockWindowService(), new MockFilePickerService());
        vm.SelectedGridSize = vm.GridSizeOptions.First(o => o.Value == "Large");
        Assert.Equal(GridSize.Large, settingsService.GridSize);
    }

    [Fact]
    public void SelectedGridSize_WhenServiceChanges_RaisesPropertyChanged()
    {
        Localization.SetProvider(new MockStringProvider(new()
        {
            { "GridSize_Small", "Small" }, { "GridSize_Medium", "Medium" }, { "GridSize_Large", "Large" },
            { "Modifier_Alt", "Alt" }, { "Modifier_Ctrl", "Ctrl" }, { "Modifier_Shift", "Shift" }, { "Modifier_Win", "Win" },
        }));
        var store = new MockSettingsStore();
        var settingsService = new SettingsService(store, new MockStartupService(), new ShortcutFolderManager(store));
        var vm = new SettingsViewModel(settingsService, new MockWindowService(), new MockFilePickerService());

        string? changed = null;
        vm.PropertyChanged += (_, e) => changed = e.PropertyName;
        settingsService.GridSize = GridSize.Small;

        Assert.Equal(nameof(SettingsViewModel.SelectedGridSize), changed);
    }

    [Fact]
    public void GridSizeOptions_ContainsThreeEntries()
    {
        Localization.SetProvider(new MockStringProvider(new()
        {
            { "GridSize_Small", "Small" }, { "GridSize_Medium", "Medium" }, { "GridSize_Large", "Large" },
            { "Modifier_Alt", "Alt" }, { "Modifier_Ctrl", "Ctrl" }, { "Modifier_Shift", "Shift" }, { "Modifier_Win", "Win" },
        }));
        var store = new MockSettingsStore();
        var settingsService = new SettingsService(store, new MockStartupService(), new ShortcutFolderManager(store));
        var vm = new SettingsViewModel(settingsService, new MockWindowService(), new MockFilePickerService());
        Assert.Equal(3, vm.GridSizeOptions.Count);
        Assert.Equal("Small", vm.GridSizeOptions[0].Value);
        Assert.Equal("Medium", vm.GridSizeOptions[1].Value);
        Assert.Equal("Large", vm.GridSizeOptions[2].Value);
    }

    [Fact]
    public void GridSize_Default_IsMedium()
    {
        var store = new MockSettingsStore();
        var service = new SettingsService(store, new MockStartupService(), new ShortcutFolderManager(store));
        Assert.Equal(GridSize.Medium, service.GridSize);
    }

    [Fact]
    public void GridSize_WhenSet_Persists()
    {
        var store = new MockSettingsStore();
        var service = new SettingsService(store, new MockStartupService(), new ShortcutFolderManager(store));
        service.GridSize = GridSize.Large;

        // Re-read from same store
        var service2 = new SettingsService(store, new MockStartupService(), new ShortcutFolderManager(store));
        Assert.Equal(GridSize.Large, service2.GridSize);
    }

    [Fact]
    public void GridSize_WhenSet_RaisesPropertyChanged()
    {
        var store = new MockSettingsStore();
        var service = new SettingsService(store, new MockStartupService(), new ShortcutFolderManager(store));
        string? changedProperty = null;
        service.PropertyChanged += (_, e) => changedProperty = e.PropertyName;

        service.GridSize = GridSize.Small;

        Assert.Equal(nameof(SettingsService.GridSize), changedProperty);
    }

    [Fact]
    public void GridSizeOptions_AreLocalizedOptions()
    {
        var (_, _, _, vm) = CreateViewModel();

        Assert.Equal("Small", vm.GridSizeOptions[0].Value);
        Assert.Equal("Small", vm.GridSizeOptions[0].DisplayName);
    }

    [Fact]
    public void SelectedGridSize_PersistsEnglishValue()
    {
        var (service, _, _, vm) = CreateViewModel();

        vm.SelectedGridSize = vm.GridSizeOptions.First(o => o.Value == "Medium");

        Assert.Equal(GridSize.Medium, service.GridSize);
    }

    [Fact]
    public void Modifiers_AreLocalizedOptions()
    {
        var (_, _, _, vm) = CreateViewModel();

        Assert.Equal("Alt", vm.Modifiers[0].Value);
    }

    [Fact]
    public void SelectedModifier_PersistsEnglishValue()
    {
        var (service, _, _, vm) = CreateViewModel();

        var ctrlOption = vm.Modifiers.First(o => o.Value == "Ctrl");
        vm.SelectedModifier = ctrlOption;

        Assert.Equal(Constants.MOD_CONTROL, service.HotkeyModifiers);
    }

    [Fact]
    public async Task RunAtStartup_RapidToggle_FinalStateMatchesLastToggle()
    {
        var (service, startup, _, vm) = CreateViewModel();

        // Rapidly toggle true then false — both should fire, not be dropped
        vm.RunAtStartup = true;
        vm.RunAtStartup = false;

        // Wait for both in-flight operations to complete
        await Task.Delay(200, TestContext.Current.CancellationToken);

        Assert.False(service.IsRunAtStartup);
        Assert.False(startup.IsEnabled);
    }

    [Fact]
    public async Task RunAtStartup_RapidToggle_TrueFalseTrueEndTrue()
    {
        var (service, startup, _, vm) = CreateViewModel();

        vm.RunAtStartup = true;
        vm.RunAtStartup = false;
        vm.RunAtStartup = true;

        await Task.Delay(200, TestContext.Current.CancellationToken);

        Assert.True(service.IsRunAtStartup);
        Assert.True(startup.IsEnabled);
    }

    [Fact]
    public async Task Dispose_WhileToggleInFlight_DoesNotThrow()
    {
        var (_, _, _, vm) = CreateViewModel();

        // Fire a toggle that will be in-flight
        vm.RunAtStartup = true;

        // Dispose immediately while the toggle may still be running
        vm.Dispose();

        // Give time for any in-flight async work to complete or fail
        await Task.Delay(100, TestContext.Current.CancellationToken);

        // If we get here without an unobserved exception, the test passes
    }

    [Fact]
    public void SetFolderSequence_ReordersAndRefreshesFolders()
    {
        var store = new MockSettingsStore();
        var folders = new System.Collections.Generic.List<ShortcutFolder>
        {
            new() { Path = @"C:\Desktop\A", Label = "A", Order = 0 },
            new() { Path = @"C:\Desktop\B", Label = "B", Order = 1 },
        };
        store.SetValue("ShortcutFolders", System.Text.Json.JsonSerializer.Serialize(folders));

        var settingsService = new SettingsService(
            store,
            new MockStartupService(),
            new ShortcutFolderManager(store));
        var vm = new SettingsViewModel(settingsService, new MockWindowService(), new MockFilePickerService());

        vm.SetFolderSequence([@"C:\Desktop\B", @"C:\Desktop\A"]);

        Assert.Equal(2, vm.Folders.Count);
        Assert.Equal("B", vm.Folders[0].Label);
        Assert.Equal("A", vm.Folders[1].Label);
    }

    [Fact]
    public async Task RenameFolderAsync_WhenValidLabelProvided_RenamesFolder()
    {
        var store = new MockSettingsStore();
        var folders = new System.Collections.Generic.List<ShortcutFolder>
        {
            new() { Path = @"C:\Desktop\A", Label = "OldLabel", Order = 0 }
        };
        store.SetValue("ShortcutFolders", System.Text.Json.JsonSerializer.Serialize(folders));

        var settingsService = new SettingsService(
            store,
            new MockStartupService(),
            new ShortcutFolderManager(store));
        var picker = new MockFilePickerService { RenameResult = "NewLabel" };
        var vm = new SettingsViewModel(settingsService, new MockWindowService(), picker);

        await vm.RenameFolderCommand.ExecuteAsync(vm.Folders[0]);

        Assert.Equal("NewLabel", vm.Folders[0].Label);
    }

    [Fact]
    public async Task RenameFolderAsync_WhenNullLabelProvided_DoesNotRenameFolder()
    {
        var store = new MockSettingsStore();
        var folders = new System.Collections.Generic.List<ShortcutFolder>
        {
            new() { Path = @"C:\Desktop\A", Label = "OldLabel", Order = 0 }
        };
        store.SetValue("ShortcutFolders", System.Text.Json.JsonSerializer.Serialize(folders));

        var settingsService = new SettingsService(
            store,
            new MockStartupService(),
            new ShortcutFolderManager(store));
        var picker = new MockFilePickerService { RenameResult = null };
        var vm = new SettingsViewModel(settingsService, new MockWindowService(), picker);

        await vm.RenameFolderCommand.ExecuteAsync(vm.Folders[0]);

        Assert.Equal("OldLabel", vm.Folders[0].Label);
    }

}
