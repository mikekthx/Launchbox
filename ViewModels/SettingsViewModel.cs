using Launchbox.Helpers;
using Launchbox.Services;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using Windows.System;

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
        get
        {
            var key = (VirtualKey)_settingsService.HotkeyKey;
            // Return digit characters for Number0-Number9 to keep UI clean
            if (key >= VirtualKey.Number0 && key <= VirtualKey.Number9)
            {
                return ((char)key).ToString();
            }
            return key.ToString();
        }
        set
        {
            if (!string.IsNullOrEmpty(value))
            {
                // Try to parse full key name (e.g. "F1", "Home", "Enter")
                if (Enum.TryParse<VirtualKey>(value, true, out var key))
                {
                    // Ensure it's a valid key
                    if (Enum.IsDefined(typeof(VirtualKey), key))
                    {
                        _settingsService.HotkeyKey = (int)key;
                    }
                }
                // Fallback for single char (e.g. "1" -> Number1, "a" -> A)
                else if (value.Length == 1 && char.IsLetterOrDigit(value[0]))
                {
                    char c = char.ToUpperInvariant(value[0]);
                    var virtualKey = (VirtualKey)c;

                    if (Enum.IsDefined(typeof(VirtualKey), virtualKey))
                    {
                        _settingsService.HotkeyKey = (int)virtualKey;
                    }
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
