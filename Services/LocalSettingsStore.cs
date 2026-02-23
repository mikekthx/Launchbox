using System;
using System.Diagnostics;
using Windows.Storage;

namespace Launchbox.Services;

public class LocalSettingsStore : ISettingsStore
{
    private readonly ISettingsContainer _settings;

    public LocalSettingsStore(ISettingsContainer settings)
    {
        _settings = settings;
    }

    public LocalSettingsStore() : this(new ApplicationDataContainerWrapper(ApplicationData.Current.LocalSettings))
    {
    }

    public bool TryGetValue(string key, out object? value)
    {
        try
        {
            return _settings.TryGetValue(key, out value);
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
            _settings.SetValue(key, value);
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"Failed to write setting {key}: {ex.Message}");
        }
    }

    private class ApplicationDataContainerWrapper : ISettingsContainer
    {
        private readonly ApplicationDataContainer _container;

        public ApplicationDataContainerWrapper(ApplicationDataContainer container)
        {
            _container = container;
        }

        public bool TryGetValue(string key, out object? value)
        {
            return _container.Values.TryGetValue(key, out value);
        }

        public void SetValue(string key, object? value)
        {
            _container.Values[key] = value;
        }
    }
}
