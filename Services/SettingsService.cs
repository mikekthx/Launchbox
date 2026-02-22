using Launchbox.Helpers;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace Launchbox.Services;

public class SettingsService : INotifyPropertyChanged
{
    private readonly ISettingsStore _store;
    private readonly IStartupService _startupService;

    public event PropertyChangedEventHandler? PropertyChanged;

    public SettingsService(ISettingsStore store, IStartupService startupService)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _startupService = startupService ?? throw new ArgumentNullException(nameof(startupService));
    }

    public string ShortcutsPath
    {
        get
        {
            if (_store.TryGetValue(nameof(ShortcutsPath), out var val) && val is string path)
            {
                if (!PathSecurity.IsUnsafePath(path))
                {
                    return path;
                }
                Trace.WriteLine($"Ignored unsafe ShortcutsPath from settings: {path}");
            }
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "Shortcuts");
        }
        set
        {
            if (PathSecurity.IsUnsafePath(value))
            {
                Trace.WriteLine($"Blocked setting unsafe ShortcutsPath: {value}");
                OnPropertyChanged();
                return;
            }

            if (ShortcutsPath != value)
            {
                if (!PathSecurity.IsUnsafePath(value))
                {
                    _store.SetValue(nameof(ShortcutsPath), value);
                }
                OnPropertyChanged();
            }
        }
    }

    public int HotkeyModifiers
    {
        get
        {
            if (_store.TryGetValue(nameof(HotkeyModifiers), out var val) && val is int mod)
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
            if (_store.TryGetValue(nameof(HotkeyKey), out var val) && val is int key)
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

    private bool _isRunAtStartup;
    public bool IsRunAtStartup
    {
        get => _isRunAtStartup;
        private set
        {
            if (_isRunAtStartup != value)
            {
                _isRunAtStartup = value;
                OnPropertyChanged();
            }
        }
    }

    public async Task InitializeAsync()
    {
        if (_startupService.IsSupported)
        {
            IsRunAtStartup = await _startupService.IsRunAtStartupEnabledAsync();
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
                // Revert if failed
                IsRunAtStartup = false;
            }
        }
        else
        {
            await _startupService.DisableStartupAsync();
            IsRunAtStartup = false;
        }
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
