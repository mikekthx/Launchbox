namespace Launchbox.Services;

public record ShortcutMetadata(string? Target, string? Arguments, string? WorkingDirectory);

public interface IShortcutResolver
{
    ShortcutMetadata? Resolve(string shortcutPath);
    string? ResolveTarget(string shortcutPath);
    string? ResolveArguments(string shortcutPath);
    string? ResolveWorkingDirectory(string shortcutPath);
}
