namespace Launchbox.Services;

public interface ISettingsContainer
{
    bool TryGetValue(string key, out object? value);
    void SetValue(string key, object? value);
}
