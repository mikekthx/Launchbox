using Launchbox.Helpers;
using System;
using System.Collections.Generic;
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
            Trace.WriteLine($"Failed to read setting {key}: {PathSecurity.GetSafeExceptionMessage(ex)}");
            value = null;
            return false;
        }
    }

    public bool SetValue(string key, object? value)
    {
        try
        {
            _settings.SetValue(key, value);
            return true;
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"Failed to write setting {key}: {PathSecurity.GetSafeExceptionMessage(ex)}");
            return false;
        }
    }

    public bool SetValues(IReadOnlyDictionary<string, object?> values)
    {
        try
        {
            _settings.SetValues(values);
            return true;
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"Failed to write batched settings: {PathSecurity.GetSafeExceptionMessage(ex)}");
            return false;
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

        public void SetValues(IReadOnlyDictionary<string, object?> values)
        {
            foreach (var kvp in values)
            {
                _container.Values[kvp.Key] = kvp.Value;
            }
        }
    }
}
