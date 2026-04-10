using System.Collections.Generic;

namespace Launchbox.Services;

/// <summary>
/// Internal seam used by <see cref="LocalSettingsStore"/> to wrap
/// <c>ApplicationDataContainer</c> for testability. Consumer code should
/// depend on <see cref="ISettingsStore"/>, not this interface.
/// </summary>
public interface ISettingsContainer
{
    bool TryGetValue(string key, out object? value);
    void SetValue(string key, object? value);
    void SetValues(IReadOnlyDictionary<string, object?> values);
}
