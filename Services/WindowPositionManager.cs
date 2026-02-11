using System;

namespace Launchbox.Services;

public class WindowPositionManager
{
    private readonly ISettingsStore _settings;

    public WindowPositionManager(ISettingsStore settings)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
    }

    public bool TryGetWindowPosition(out int x, out int y, out int width, out int height)
    {
        x = 0;
        y = 0;
        width = 0;
        height = 0;

        if (_settings.TryGetValue("WinX", out var winX) &&
            _settings.TryGetValue("WinY", out var winY) &&
            _settings.TryGetValue("WinW", out var winW) &&
            _settings.TryGetValue("WinH", out var winH) &&
            winX is int valX && winY is int valY && winW is int valW && winH is int valH)
        {
            x = valX;
            y = valY;
            width = valW;
            height = valH;
            return true;
        }

        return false;
    }

    public void SaveWindowPosition(int x, int y, int width, int height)
    {
        _settings.SetValue("WinX", x);
        _settings.SetValue("WinY", y);
        _settings.SetValue("WinW", width);
        _settings.SetValue("WinH", height);
    }
}
