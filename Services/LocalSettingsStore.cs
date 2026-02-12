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
        return _settings.Values.TryGetValue(key, out value);
    }

    public void SetValue(string key, object? value)
    {
        _settings.Values[key] = value;
    }
}
