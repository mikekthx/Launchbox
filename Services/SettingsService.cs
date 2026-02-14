using Launchbox.Helpers;
using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace Launchbox.Services;

public class SettingsService : INotifyPropertyChanged
{
    private readonly ISettingsStore _store;
    private readonly IStartupService _startupService;

    public event PropertyChangedEventHandler? PropertyChanged;
    public event EventHandler? SettingsChanged;

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
                return path;
            }
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "Shortcuts");
        }
        set
        {
            if (ShortcutsPath != value)
            {
                _store.SetValue(nameof(ShortcutsPath), value);
                OnPropertyChanged();
                SettingsChanged?.Invoke(this, EventArgs.Empty);
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
                SettingsChanged?.Invoke(this, EventArgs.Empty);
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
                SettingsChanged?.Invoke(this, EventArgs.Empty);
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
