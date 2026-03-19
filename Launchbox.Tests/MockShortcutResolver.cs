using Launchbox.Services;

namespace Launchbox.Tests;

public class MockShortcutResolver : IShortcutResolver
{
    private readonly string? _target;
    private readonly string? _arguments;
    private readonly string? _workingDirectory;

    public MockShortcutResolver(string? target = null, string? arguments = null, string? workingDirectory = null)
    {
        _target = target;
        _arguments = arguments;
        _workingDirectory = workingDirectory;
    }

    public ShortcutMetadata? Resolve(string shortcutPath)
    {
        return new ShortcutMetadata(_target, _arguments, _workingDirectory);
    }
}
