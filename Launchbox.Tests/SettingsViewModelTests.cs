using Launchbox.Helpers;
using Launchbox.Services;
using Launchbox.ViewModels;
using System;
using System.Threading.Tasks;
using Windows.System;
using Xunit;

namespace Launchbox.Tests;

public class SettingsViewModelTests
{
    private (SettingsService, MockStartupService, MockFilePickerService, SettingsViewModel) CreateViewModel()
    {
        var settingsStore = new MockSettingsStore();
        var startupService = new MockStartupService();
        var settingsService = new SettingsService(settingsStore, startupService);
        var pickerService = new MockFilePickerService();
        var windowService = new MockWindowService();

        var viewModel = new SettingsViewModel(settingsService, windowService, pickerService);

        return (settingsService, startupService, pickerService, viewModel);
    }

    [Fact]
    public void ShortcutsPath_UpdatesService_WhenChanged()
    {
        var (service, _, _, vm) = CreateViewModel();

        vm.ShortcutsPath = @"C:\NewPath";

        Assert.Equal(@"C:\NewPath", service.ShortcutsPath);
    }

    [Fact]
    public void ShortcutsPath_RaisesPropertyChanged_WhenServiceChanges()
    {
        var (service, _, _, vm) = CreateViewModel();
        bool raised = false;
        vm.PropertyChanged += (s, e) => { if (e.PropertyName == nameof(SettingsViewModel.ShortcutsPath)) raised = true; };

        service.ShortcutsPath = @"C:\ExternalChange";

        Assert.True(raised);
        Assert.Equal(@"C:\ExternalChange", vm.ShortcutsPath);
    }

    [Fact]
    public async Task RunAtStartup_UpdatesServiceAsync()
    {
        var (service, startup, _, vm) = CreateViewModel();

        vm.RunAtStartup = true;

        // Wait for async void property setter to propagate
        var timeout = DateTime.Now.AddSeconds(1);
        while (!service.IsRunAtStartup && DateTime.Now < timeout)
        {
            await Task.Delay(10);
        }

        Assert.True(service.IsRunAtStartup);
        Assert.True(startup.IsEnabled);
    }

    [Fact]
    public void SelectedModifier_ConvertsToConstants()
    {
        var (service, _, _, vm) = CreateViewModel();

        vm.SelectedModifier = "Ctrl";
        Assert.Equal(Constants.MOD_CONTROL, service.HotkeyModifiers);

        vm.SelectedModifier = "Win";
        Assert.Equal(Constants.MOD_WIN, service.HotkeyModifiers);

        vm.SelectedModifier = "Alt";
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
    public void HotkeyKeyString_AcceptsNumericEnumValues()
    {
        var (service, _, _, vm) = CreateViewModel();

        // "36" is the integer value for VirtualKey.Home
        // Enum.TryParse accepts string representations of underlying integer values
        vm.HotkeyKeyString = "36";
        Assert.Equal((int)VirtualKey.Home, service.HotkeyKey);
    }

    [Fact]
    public async Task BrowseFolderCommand_UpdatesPath_WhenFolderSelected()
    {
        var (_, _, picker, vm) = CreateViewModel();
        picker.SelectedFolder = @"C:\PickedFolder";

        // Pass a dummy object as window
        vm.BrowseFolderCommand.Execute(new object());

        // Wait for async void
        var timeout = DateTime.Now.AddSeconds(1);
        while (vm.ShortcutsPath != @"C:\PickedFolder" && DateTime.Now < timeout)
        {
            await Task.Delay(10);
        }

        Assert.Equal(@"C:\PickedFolder", vm.ShortcutsPath);
    }

    [Fact]
    public async Task BrowseFolderCommand_DoesNothing_WhenCancelled()
    {
        var (_, _, picker, vm) = CreateViewModel();
        picker.SelectedFolder = null; // Cancelled
        var oldPath = vm.ShortcutsPath;

        vm.BrowseFolderCommand.Execute(new object());

        // Wait briefly to ensure it didn't change
        await Task.Delay(50);

        Assert.Equal(oldPath, vm.ShortcutsPath);
    }
}
