using Launchbox.Services;
using System;
using System.Collections.Generic;

namespace Launchbox.Tests;

public class MockSettingsStore : ISettingsStore
{
    private readonly Dictionary<string, object> _store = new();

    public bool ShouldThrow { get; set; }

    public bool TryGetValue(string key, out object? value)
    {
        if (ShouldThrow)
        {
            throw new Exception("Settings store failure");
        }
        return _store.TryGetValue(key, out value);
    }

    public void SetValue(string key, object? value)
    {
        if (ShouldThrow)
        {
            throw new Exception("Settings store failure");
        }

        if (value != null)
        {
            _store[key] = value;
        }
        else
        {
            _store.Remove(key);
        }
    }
}
