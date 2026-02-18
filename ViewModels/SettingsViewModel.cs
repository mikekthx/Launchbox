using Launchbox.Helpers;
using Launchbox.Services;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace Launchbox.ViewModels;

public class SettingsViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly SettingsService _settingsService;
    private readonly IWindowService _windowService;
    private readonly IFilePickerService _filePickerService;

    public event PropertyChangedEventHandler? PropertyChanged;

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
        _ = _settingsService.InitializeAsync();

        ResetPositionCommand = new SimpleCommand(() => _windowService.ResetPosition());
        BrowseFolderCommand = new SimpleCommand(BrowseFolderAsync);
    }

    private async void BrowseFolderAsync(object? parameter)
    {
        if (parameter == null) return; // Need window handle

        var folder = await _filePickerService.PickSingleFolderAsync(parameter);
        if (!string.IsNullOrEmpty(folder))
        {
            ShortcutsPath = folder;
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
                // Fire and forget async call
                _ = _settingsService.SetRunAtStartupAsync(value);
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

    private void OnPropertyChanged([CallerMemberName] string? name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public void Dispose()
    {
        _settingsService.PropertyChanged -= OnServicePropertyChanged;
        GC.SuppressFinalize(this);
    }
}
