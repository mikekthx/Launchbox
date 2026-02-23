using Launchbox.Services;

namespace Launchbox.Tests;

public class MockShortcutResolver : IShortcutResolver
{
    private readonly string? _target;

    public MockShortcutResolver(string? target = null)
    {
        _target = target;
    }

    public string? ResolveTarget(string shortcutPath)
    {
        return _target;
    }
}
