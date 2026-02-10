namespace Launchbox;

public interface ISettingsStore
{
    bool TryGetValue(string key, out object? value);
    void SetValue(string key, object? value);
}
