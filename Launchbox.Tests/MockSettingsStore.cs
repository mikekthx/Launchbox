using Launchbox.Services;
using System;
using System.Collections.Generic;

namespace Launchbox.Tests;

public class MockSettingsStore : ISettingsStore
{
    private readonly Dictionary<string, object> _store = new();

    public bool ShouldThrow { get; set; }

    /// <summary>
    /// When set, <see cref="SetValue"/> returns false for the specified keys instead of writing.
    /// </summary>
    public HashSet<string> FailSetValueKeys { get; } = new(StringComparer.OrdinalIgnoreCase);

    public bool TryGetValue(string key, out object? value)
    {
        if (ShouldThrow)
        {
            throw new Exception("Settings store failure");
        }
        return _store.TryGetValue(key, out value);
    }

    public bool SetValue(string key, object? value)
    {
        if (ShouldThrow)
        {
            throw new Exception("Settings store failure");
        }

        if (FailSetValueKeys.Contains(key))
        {
            return false;
        }

        if (value != null)
        {
            _store[key] = value;
        }
        else
        {
            _store.Remove(key);
        }
        return true;
    }
}
