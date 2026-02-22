using System;
using System.Diagnostics;
using Windows.Storage;

namespace Launchbox.Services;

public class LocalSettingsStore : ISettingsStore
{
    private readonly ApplicationDataContainer _settings;

    public LocalSettingsStore()
    {
        _settings = ApplicationData.Current.LocalSettings;
    }

    public bool TryGetValue(string key, out object? value)
    {
        try
        {
            return _settings.Values.TryGetValue(key, out value);
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"Failed to read setting {key}: {ex.Message}");
            value = null;
            return false;
        }
    }

    public void SetValue(string key, object? value)
    {
        try
        {
            _settings.Values[key] = value;
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"Failed to write setting {key}: {ex.Message}");
        }
    }
}
