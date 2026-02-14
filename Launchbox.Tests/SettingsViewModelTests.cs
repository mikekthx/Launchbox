using System;
using System.Threading.Tasks;
using Launchbox.Helpers;
using Launchbox.Services;
using Launchbox.ViewModels;
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

    [Fact]
    public void HotkeyKeyString_ValidatesAndUpdates()
    {
        var (service, _, _, vm) = CreateViewModel();

        vm.HotkeyKeyString = "k";
        Assert.Equal((int)'K', service.HotkeyKey); // Converted to upper char code

        vm.HotkeyKeyString = "5";
        Assert.Equal((int)'5', service.HotkeyKey);

        // Invalid char
        var oldKey = service.HotkeyKey;
        vm.HotkeyKeyString = "?";
        Assert.Equal(oldKey, service.HotkeyKey); // Should not change
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
