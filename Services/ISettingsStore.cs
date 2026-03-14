namespace Launchbox.Services;

/// <summary>
/// Read/write settings store consumed by <see cref="SettingsService"/> and
/// <see cref="WindowPositionManager"/>. The production implementation is
/// <see cref="LocalSettingsStore"/>.
/// </summary>
public interface ISettingsStore
{
    bool TryGetValue(string key, out object? value);
    void SetValue(string key, object? value);
}
