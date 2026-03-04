using System;

namespace Launchbox.Services;

/// <summary>
/// Manages loading and saving the application window's position and size to persistent storage.
/// </summary>
public class WindowPositionManager
{
    private const string SETTING_KEY_X = "WinX";
    private const string SETTING_KEY_Y = "WinY";
    private const string SETTING_KEY_WIDTH = "WinW";
    private const string SETTING_KEY_HEIGHT = "WinH";

    private readonly ISettingsStore _settings;

    /// <summary>
    /// Initializes a new instance of the <see cref="WindowPositionManager"/> class.
    /// </summary>
    /// <param name="settings">The settings store used to persist window dimensions.</param>
    public WindowPositionManager(ISettingsStore settings)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
    }

    /// <summary>
    /// Attempts to retrieve the saved window position and dimensions.
    /// </summary>
    /// <param name="x">The X coordinate of the window.</param>
    /// <param name="y">The Y coordinate of the window.</param>
    /// <param name="width">The width of the window.</param>
    /// <param name="height">The height of the window.</param>
    /// <returns><c>true</c> if the position was successfully loaded; otherwise, <c>false</c>.</returns>
    public bool TryGetWindowPosition(out int x, out int y, out int width, out int height)
    {
        x = 0;
        y = 0;
        width = 0;
        height = 0;

        if (_settings.TryGetValue(SETTING_KEY_X, out var winX) &&
            _settings.TryGetValue(SETTING_KEY_Y, out var winY) &&
            _settings.TryGetValue(SETTING_KEY_WIDTH, out var winW) &&
            _settings.TryGetValue(SETTING_KEY_HEIGHT, out var winH) &&
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

    /// <summary>
    /// Saves the current window position and dimensions to persistent storage.
    /// </summary>
    /// <param name="x">The X coordinate of the window.</param>
    /// <param name="y">The Y coordinate of the window.</param>
    /// <param name="width">The width of the window.</param>
    /// <param name="height">The height of the window.</param>
    public void SaveWindowPosition(int x, int y, int width, int height)
    {
        _settings.SetValue(SETTING_KEY_X, x);
        _settings.SetValue(SETTING_KEY_Y, y);
        _settings.SetValue(SETTING_KEY_WIDTH, width);
        _settings.SetValue(SETTING_KEY_HEIGHT, height);
    }
}
