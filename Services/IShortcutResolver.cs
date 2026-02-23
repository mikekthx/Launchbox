namespace Launchbox.Services;

public interface IShortcutResolver
{
    string? ResolveTarget(string shortcutPath);
}
