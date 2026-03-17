using CommunityToolkit.Mvvm.ComponentModel;
using Launchbox.Helpers;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace Launchbox.Services;

public class SettingsService : ObservableObject
{
    private readonly ISettingsStore _store;
    private readonly IStartupService _startupService;

    public SettingsService(ISettingsStore store, IStartupService startupService)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _startupService = startupService ?? throw new ArgumentNullException(nameof(startupService));
    }

    /// <summary>
    /// Gets or sets the path to the folder containing application shortcuts.
    /// When getting, environment variables in the path are automatically expanded.
    /// The path is continuously verified for security boundaries; accessing or setting an unsafe path is blocked and logged.
    /// </summary>
    public string ShortcutsPath
    {
        get
        {
            if (_store.TryGetValue(nameof(ShortcutsPath), out var val) && val is string path)
            {
                var expandedPath = Environment.ExpandEnvironmentVariables(path);
                if (!PathSecurity.IsUnsafePath(expandedPath))
                {
                    return expandedPath;
                }
                Trace.WriteLine($"Ignored unsafe ShortcutsPath from settings: {PathSecurity.RedactPath(expandedPath)}");
            }
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "Shortcuts");
        }
        set
        {
            if (PathSecurity.IsUnsafePath(value))
            {
                Trace.WriteLine($"Blocked setting unsafe ShortcutsPath: {PathSecurity.RedactPath(value)}");
                OnPropertyChanged();
                return;
            }

            if (ShortcutsPath != value)
            {
                _store.SetValue(nameof(ShortcutsPath), value);
                OnPropertyChanged();
            }
        }
    }

    // Valid modifier flags: any combination of MOD_ALT (1), MOD_CONTROL (2), MOD_SHIFT (4), MOD_WIN (8)
    private const int VALID_MODIFIER_MASK = Constants.MOD_ALT | Constants.MOD_CONTROL | Constants.MOD_SHIFT | Constants.MOD_WIN;

    public int HotkeyModifiers
    {
        get
        {
            if (_store.TryGetValue(nameof(HotkeyModifiers), out var val) && val is int mod
                && mod > 0 && (mod & ~VALID_MODIFIER_MASK) == 0)
            {
                return mod;
            }
            return Constants.MOD_ALT;
        }
        set
        {
            if (HotkeyModifiers != value)
            {
                _store.SetValue(nameof(HotkeyModifiers), value);
                OnPropertyChanged();
            }
        }
    }

    public int HotkeyKey
    {
        get
        {
            // Virtual key codes range from 0x01 to 0xFE
            if (_store.TryGetValue(nameof(HotkeyKey), out var val) && val is int key
                && key >= 0x01 && key <= 0xFE)
            {
                return key;
            }
            return Constants.VK_S;
        }
        set
        {
            if (HotkeyKey != value)
            {
                _store.SetValue(nameof(HotkeyKey), value);
                OnPropertyChanged();
            }
        }
    }

    public GridSize GridSize
    {
        get
        {
            if (_store.TryGetValue(nameof(GridSize), out var val) && val is string s
                && Enum.TryParse<GridSize>(s, ignoreCase: true, out var parsed))
            {
                return parsed;
            }
            return GridSize.Medium;
        }
        set
        {
            if (GridSize != value)
            {
                _store.SetValue(nameof(GridSize), value.ToString());
                OnPropertyChanged();
            }
        }
    }

    private bool _isRunAtStartup;
    public bool IsRunAtStartup
    {
        get => _isRunAtStartup;
        private set => SetProperty(ref _isRunAtStartup, value);
    }

    public async Task InitializeAsync()
    {
        try
        {
            if (_startupService.IsSupported)
            {
                IsRunAtStartup = await _startupService.IsRunAtStartupEnabledAsync();
            }
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"Failed to initialize settings (StartupService): {PathSecurity.GetSafeExceptionMessage(ex)}");
        }
    }

    public async Task SetRunAtStartupAsync(bool enable)
    {
        if (!_startupService.IsSupported) return;

        if (enable)
        {
            bool success = await _startupService.TryEnableStartupAsync();
            if (success)
            {
                IsRunAtStartup = true;
            }
            else
            {
                // Revert: OS denied the startup enable request, so reset the toggle to false.
                IsRunAtStartup = false;
            }
        }
        else
        {
            await _startupService.DisableStartupAsync();
            IsRunAtStartup = false;
        }
    }

}
