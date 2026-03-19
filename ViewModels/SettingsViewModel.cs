using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Launchbox.Helpers;
using Launchbox.Services;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.System;

namespace Launchbox.ViewModels;

public partial class SettingsViewModel : ObservableObject, IDisposable
{
    private readonly SettingsService _settingsService;
    private readonly IWindowService _windowService;
    private readonly IFilePickerService _filePickerService;
    private readonly SemaphoreSlim _startupToggleLock = new(1, 1);
    private bool _pendingStartupValue;

    private static readonly Dictionary<string, int> MODIFIER_MAP = new()
    {
        { "Alt", Constants.MOD_ALT },
        { "Ctrl", Constants.MOD_CONTROL },
        { "Shift", Constants.MOD_SHIFT },
        { "Win", Constants.MOD_WIN },
    };

    public IReadOnlyList<LocalizedOption> Modifiers { get; } =
    [
        new("Alt", Localization.GetString("Modifier_Alt")),
        new("Ctrl", Localization.GetString("Modifier_Ctrl")),
        new("Shift", Localization.GetString("Modifier_Shift")),
        new("Win", Localization.GetString("Modifier_Win")),
    ];

    public IReadOnlyList<LocalizedOption> GridSizeOptions { get; } =
    [
        new("Small", Localization.GetString("GridSize_Small")),
        new("Medium", Localization.GetString("GridSize_Medium")),
        new("Large", Localization.GetString("GridSize_Large")),
    ];

    public LocalizedOption SelectedGridSize
    {
        get => GridSizeOptions.FirstOrDefault(o => o.Value == _settingsService.GridSize.ToString())
            ?? GridSizeOptions[1]; // Default to Medium
        set
        {
            if (value is not null && Enum.TryParse<GridSize>(value.Value, out var g))
                _settingsService.GridSize = g;
        }
    }

    public SettingsViewModel(SettingsService settingsService, IWindowService windowService, IFilePickerService filePickerService)
    {
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _windowService = windowService ?? throw new ArgumentNullException(nameof(windowService));
        _filePickerService = filePickerService ?? throw new ArgumentNullException(nameof(filePickerService));

        _pendingStartupValue = _settingsService.IsRunAtStartup;
        _settingsService.PropertyChanged += OnServicePropertyChanged;
    }

    [RelayCommand]
    private async Task BrowseFolderAsync()
    {
        try
        {
            var folder = await _filePickerService.PickSingleFolderAsync();
            if (!string.IsNullOrEmpty(folder))
            {
                ShortcutsPath = folder;
            }
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"Failed to browse for folder: {PathSecurity.GetSafeExceptionMessage(ex)}");
        }
    }

    private async Task SetRunAtStartupSafeAsync(bool value)
    {
        try
        {
            await _startupToggleLock.WaitAsync();
        }
        catch (ObjectDisposedException)
        {
            // Window is closing and Dispose() was called — operation is moot.
            return;
        }

        try
        {
            await _settingsService.SetRunAtStartupAsync(value);
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"Failed to set run at startup: {PathSecurity.GetSafeExceptionMessage(ex)}");
        }
        finally
        {
            try
            {
                _startupToggleLock.Release();
            }
            catch (ObjectDisposedException)
            {
                // Window closed between WaitAsync and Release — safe to ignore.
            }
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
        else if (e.PropertyName == nameof(SettingsService.GridSize))
            OnPropertyChanged(nameof(SelectedGridSize));
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
            if (_pendingStartupValue != value)
            {
                _pendingStartupValue = value;
                _ = SetRunAtStartupSafeAsync(value);
            }
        }
    }

    /// <summary>
    /// Gets or sets the modifier key for the global hotkey (e.g., Ctrl, Shift, Win, Alt).
    /// </summary>
    public LocalizedOption SelectedModifier
    {
        get
        {
            var key = MODIFIER_MAP.FirstOrDefault(kv => kv.Value == _settingsService.HotkeyModifiers).Key ?? "Alt";
            return Modifiers.FirstOrDefault(o => o.Value == key) ?? Modifiers[0];
        }
        set
        {
            if (value is not null && MODIFIER_MAP.TryGetValue(value.Value, out var modifier))
                _settingsService.HotkeyModifiers = modifier;
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
                // Fallback for single char (e.g. "1" -> Number1, "a" -> A)
                // Prioritize this for alphanumeric chars because Enum.TryParse("5") returns VirtualKey.XButton1 (5)
                // whereas we want VirtualKey.Number5 (53).
                if (value.Length == 1 && char.IsLetterOrDigit(value[0]))
                {
                    char c = char.ToUpperInvariant(value[0]);
                    var virtualKey = (VirtualKey)c;

                    if (Enum.IsDefined(typeof(VirtualKey), virtualKey))
                    {
                        _settingsService.HotkeyKey = (int)virtualKey;
                    }
                }
                // Try to parse full key name (e.g. "F1", "Home", "Enter")
                else if (Enum.TryParse<VirtualKey>(value, true, out var key))
                {
                    // Ensure it's a valid key
                    if (Enum.IsDefined(typeof(VirtualKey), key))
                    {
                        _settingsService.HotkeyKey = (int)key;
                    }
                }
            }
            // Always notify to refresh UI (e.g., if user typed invalid char, revert to old value)
            OnPropertyChanged(nameof(HotkeyKeyString));
        }
    }

    [RelayCommand]
    private void ResetPosition() => _windowService.ResetPosition();

    public void Dispose()
    {
        _settingsService.PropertyChanged -= OnServicePropertyChanged;
        _startupToggleLock.Dispose();
        GC.SuppressFinalize(this);
    }
}
