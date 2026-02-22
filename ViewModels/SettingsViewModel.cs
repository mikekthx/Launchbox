using Launchbox.Helpers;
using Launchbox.Services;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows.Input;

namespace Launchbox.ViewModels;

public class SettingsViewModel : ViewModelBase, IDisposable
{
    private readonly SettingsService _settingsService;
    private readonly IWindowService _windowService;
    private readonly IFilePickerService _filePickerService;

    public ICommand ResetPositionCommand { get; }
    public ICommand BrowseFolderCommand { get; }

    public ObservableCollection<string> Modifiers { get; } = new() { "Alt", "Ctrl", "Shift", "Win" };

    public SettingsViewModel(SettingsService settingsService, IWindowService windowService, IFilePickerService filePickerService)
    {
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _windowService = windowService ?? throw new ArgumentNullException(nameof(windowService));
        _filePickerService = filePickerService ?? throw new ArgumentNullException(nameof(filePickerService));

        _settingsService.PropertyChanged += OnServicePropertyChanged;

        // Ensure startup status is fresh
        _ = InitializeSettingsAsync();

        ResetPositionCommand = new SimpleCommand(() => _windowService.ResetPosition());
        BrowseFolderCommand = new SimpleCommand(BrowseFolderAsync);
    }

    private async void BrowseFolderAsync(object? parameter)
    {
        if (parameter == null) return; // Need window handle

        try
        {
            var folder = await _filePickerService.PickSingleFolderAsync(parameter);
            if (!string.IsNullOrEmpty(folder))
            {
                ShortcutsPath = folder;
            }
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"Failed to browse for folder: {ex.Message}");
        }
    }

    private async Task InitializeSettingsAsync()
    {
        try
        {
            await _settingsService.InitializeAsync();
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"Failed to initialize settings: {ex.Message}");
        }
    }

    private async Task SetRunAtStartupSafeAsync(bool value)
    {
        try
        {
            await _settingsService.SetRunAtStartupAsync(value);
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"Failed to set run at startup: {ex.Message}");
        }
    }

    private void OnServicePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SettingsService.ShortcutsPath))
            OnPropertyChanged(nameof(ShortcutsPath));
        else if (e.PropertyName == nameof(SettingsService.IsRunAtStartup))
            OnPropertyChanged(nameof(RunAtStartup));
        else if (e.PropertyName == nameof(SettingsService.HotkeyModifiers))
            OnPropertyChanged(nameof(SelectedModifier));
        else if (e.PropertyName == nameof(SettingsService.HotkeyKey))
            OnPropertyChanged(nameof(HotkeyKeyString));
    }

    public string ShortcutsPath
    {
        get => _settingsService.ShortcutsPath;
        set => _settingsService.ShortcutsPath = value;
    }

    public bool RunAtStartup
    {
        get => _settingsService.IsRunAtStartup;
        set
        {
            if (_settingsService.IsRunAtStartup != value)
            {
                _ = SetRunAtStartupSafeAsync(value);
            }
        }
    }

    public string SelectedModifier
    {
        get
        {
            var mod = _settingsService.HotkeyModifiers;
            if (mod == Constants.MOD_CONTROL) return "Ctrl";
            if (mod == Constants.MOD_SHIFT) return "Shift";
            if (mod == Constants.MOD_WIN) return "Win";
            return "Alt";
        }
        set
        {
            int mod = Constants.MOD_ALT;
            if (value == "Ctrl") mod = Constants.MOD_CONTROL;
            else if (value == "Shift") mod = Constants.MOD_SHIFT;
            else if (value == "Win") mod = Constants.MOD_WIN;

            _settingsService.HotkeyModifiers = mod;
        }
    }

    public string HotkeyKeyString
    {
        get => ((char)_settingsService.HotkeyKey).ToString();
        set
        {
            if (!string.IsNullOrEmpty(value))
            {
                string upper = value.ToUpperInvariant();
                char c = upper[0];
                // Only allow A-Z, 0-9
                if ((c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9'))
                {
                    _settingsService.HotkeyKey = (int)c;
                }
            }
            // Always notify to refresh UI (e.g., if user typed invalid char, revert to old value)
            OnPropertyChanged(nameof(HotkeyKeyString));
        }
    }

    public void Dispose()
    {
        _settingsService.PropertyChanged -= OnServicePropertyChanged;
        GC.SuppressFinalize(this);
    }
}
