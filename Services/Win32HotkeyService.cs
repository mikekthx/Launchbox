using System;
using System.Diagnostics.CodeAnalysis;

namespace Launchbox.Services;

/// <summary>
/// Production implementation of <see cref="INativeHotkeyService"/> that delegates
/// to the Win32 RegisterHotKey / UnregisterHotKey APIs via <see cref="NativeMethods"/>.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed class Win32HotkeyService : INativeHotkeyService
{
    public bool RegisterHotKey(IntPtr hWnd, int id, uint modifiers, uint virtualKey)
        => NativeMethods.RegisterHotKey(hWnd, id, modifiers, virtualKey);

    public bool UnregisterHotKey(IntPtr hWnd, int id)
        => NativeMethods.UnregisterHotKey(hWnd, id);
}
