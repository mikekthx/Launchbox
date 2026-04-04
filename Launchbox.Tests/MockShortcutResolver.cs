using Launchbox.Services;

namespace Launchbox.Tests;

public class MockShortcutResolver : IShortcutResolver
{
    private readonly string? _target;
    private readonly string? _arguments;
    private readonly string? _workingDirectory;
    private readonly bool _returnNull;

    public MockShortcutResolver(string? target = null, string? arguments = null, string? workingDirectory = null, bool returnNull = false)
    {
        _target = target;
        _arguments = arguments;
        _workingDirectory = workingDirectory;
        _returnNull = returnNull;
    }

    public ShortcutMetadata? Resolve(string shortcutPath)
    {
        if (_returnNull) return null;
        return new ShortcutMetadata(_target, _arguments, _workingDirectory);
    }
}
