using System;

namespace Launchbox.Services;

public interface INativeHotkeyService
{
    bool RegisterHotKey(IntPtr hWnd, int id, uint modifiers, uint virtualKey);
    bool UnregisterHotKey(IntPtr hWnd, int id);
}
