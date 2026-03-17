using Launchbox.Services;

namespace Launchbox.Tests;

public class MockShortcutResolver : IShortcutResolver
{
    private readonly string? _target;

    public MockShortcutResolver(string? target = null)
    {
        _target = target;
    }

    public ShortcutMetadata? Resolve(string shortcutPath)
    {
        return new ShortcutMetadata(_target, null, null);
    }
}
