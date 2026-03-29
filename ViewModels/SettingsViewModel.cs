using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Launchbox.Helpers;
using Launchbox.Models;
using Launchbox.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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

    public IReadOnlyList<LocalizedOption> ViewModeOptions { get; } =
    [
        new("Merged", Localization.GetString("Settings_ViewMode_Merged")),
        new("Grouped", Localization.GetString("Settings_ViewMode_Grouped"))
    ];

    public LocalizedOption SelectedViewModeOption
    {
        get => ViewModeOptions.FirstOrDefault(o => o.Value == _settingsService.FolderViewMode.ToString())
            ?? ViewModeOptions[0];
        set
        {
            if (value is not null && Enum.TryParse<FolderViewMode>(value.Value, out var mode))
            {
                _settingsService.FolderViewMode = mode;
            }
        }
    }

    public bool CollapsibleGroups
    {
        get => _settingsService.CollapsibleGroups;
        set => _settingsService.CollapsibleGroups = value;
    }

    public ObservableCollection<ShortcutFolder> Folders { get; } = [];

    public SettingsViewModel(SettingsService settingsService, IWindowService windowService, IFilePickerService filePickerService)
    {
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _windowService = windowService ?? throw new ArgumentNullException(nameof(windowService));
        _filePickerService = filePickerService ?? throw new ArgumentNullException(nameof(filePickerService));

        _pendingStartupValue = _settingsService.IsRunAtStartup;
        _settingsService.PropertyChanged += OnServicePropertyChanged;

        RefreshFolders();
    }

    [RelayCommand]
    private async Task AddFolderAsync()
    {
        try
        {
            var path = await _filePickerService.PickSingleFolderAsync();
            if (!string.IsNullOrEmpty(path))
            {
                if (_settingsService.AddShortcutFolder(path))
                {
                    RefreshFolders();
                }
            }
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"Failed to add folder: {PathSecurity.GetSafeExceptionMessage(ex)}");
        }
    }

    [RelayCommand]
    private void RemoveFolder(int order)
    {
        if (_settingsService.RemoveShortcutFolder(order))
        {
            RefreshFolders();
        }
    }

    [RelayCommand]
    private void MoveFolderUp(int order)
    {
        if (order > 0 && _settingsService.ReorderShortcutFolder(order, order - 1))
        {
            RefreshFolders();
        }
    }

    [RelayCommand]
    private void MoveFolderDown(int order)
    {
        var folders = _settingsService.GetShortcutFolders();
        if (order < folders.Count - 1 && _settingsService.ReorderShortcutFolder(order, order + 1))
        {
            RefreshFolders();
        }
    }

    // ViewModel stays platform-agnostic — no WinUI types (ContentDialog, XamlRoot).
    // The rename UI lives in SettingsWindow.xaml.cs code-behind.
    public void ApplyRename(int order, string newLabel)
    {
        if (_settingsService.RenameShortcutFolder(order, newLabel))
        {
            RefreshFolders();
        }
    }

    public void SetFolderSequence(IReadOnlyList<string> orderedPaths)
    {
        if (_settingsService.SetShortcutFolderSequence(orderedPaths))
            RefreshFolders();
    }

    private void RefreshFolders()
    {
        Folders.Clear();
        foreach (var f in _settingsService.GetShortcutFolders())
        {
            Folders.Add(f);
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
        if (e.PropertyName == nameof(SettingsService.IsRunAtStartup))
        {
            _pendingStartupValue = _settingsService.IsRunAtStartup;
            OnPropertyChanged(nameof(RunAtStartup));
        }
        else if (e.PropertyName == nameof(SettingsService.HotkeyModifiers))
            OnPropertyChanged(nameof(SelectedModifier));
        else if (e.PropertyName == nameof(SettingsService.HotkeyKey))
            OnPropertyChanged(nameof(HotkeyKeyString));
        else if (e.PropertyName == nameof(SettingsService.GridSize))
            OnPropertyChanged(nameof(SelectedGridSize));
        else if (e.PropertyName == nameof(SettingsService.KeepCentered))
            OnPropertyChanged(nameof(KeepCentered));
        else if (e.PropertyName == SettingsService.SHORTCUT_FOLDERS_KEY)
            RefreshFolders();
        else if (e.PropertyName == nameof(SettingsService.FolderViewMode))
            OnPropertyChanged(nameof(SelectedViewModeOption));
        else if (e.PropertyName == nameof(SettingsService.CollapsibleGroups))
            OnPropertyChanged(nameof(CollapsibleGroups));
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
            var parsedKey = ParseVirtualKey(value);
            if (parsedKey.HasValue)
            {
                _settingsService.HotkeyKey = (int)parsedKey.Value;
            }
            // Always notify to refresh UI (e.g., if user typed invalid char, revert to old value)
            OnPropertyChanged(nameof(HotkeyKeyString));
        }
    }

    private static VirtualKey? ParseVirtualKey(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return null;
        }

        // Fallback for single char (e.g. "1" -> Number1, "a" -> A)
        // Prioritize this for alphanumeric chars because Enum.TryParse("5") returns VirtualKey.XButton1 (5)
        // whereas we want VirtualKey.Number5 (53).
        if (value.Length == 1 && char.IsLetterOrDigit(value[0]))
        {
            char c = char.ToUpperInvariant(value[0]);
            var virtualKey = (VirtualKey)c;

            if (Enum.IsDefined(typeof(VirtualKey), virtualKey))
            {
                return virtualKey;
            }
        }
        // Try to parse full key name (e.g. "F1", "Home", "Enter")
        // Reject purely numeric strings — Enum.TryParse("112") silently maps to VirtualKey.F1
        else if (!value.All(char.IsDigit) && Enum.TryParse<VirtualKey>(value, true, out var key))
        {
            // Ensure it's a valid key
            if (Enum.IsDefined(typeof(VirtualKey), key))
            {
                return key;
            }
        }

        return null;
    }

    public bool KeepCentered
    {
        get => _settingsService.KeepCentered;
        set => _settingsService.KeepCentered = value;
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
