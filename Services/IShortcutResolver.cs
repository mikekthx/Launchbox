namespace Launchbox.Services;

public interface IShortcutResolver
{
    string? ResolveTarget(string shortcutPath);
    string? ResolveArguments(string shortcutPath);
    string? ResolveWorkingDirectory(string shortcutPath);
}
