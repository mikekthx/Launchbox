using System;
using System.Diagnostics;

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
    /// If any write fails, rolls back to the previous values to prevent partial state.
    /// </summary>
    /// <param name="x">The X coordinate of the window.</param>
    /// <param name="y">The Y coordinate of the window.</param>
    /// <param name="width">The width of the window.</param>
    /// <param name="height">The height of the window.</param>
    /// <returns><c>true</c> if all values were saved successfully; otherwise, <c>false</c>.</returns>
    public bool SaveWindowPosition(int x, int y, int width, int height)
    {
        // Capture previous values before writing so we can roll back on partial failure
        TryGetWindowPosition(out int oldX, out int oldY, out int oldW, out int oldH);

        (string Key, object Value)[] entries =
        [
            (SETTING_KEY_X, x),
            (SETTING_KEY_Y, y),
            (SETTING_KEY_WIDTH, width),
            (SETTING_KEY_HEIGHT, height),
        ];

        for (int i = 0; i < entries.Length; i++)
        {
            if (!_settings.SetValue(entries[i].Key, entries[i].Value))
            {
                Trace.WriteLine($"Failed to save window position: SetValue returned false for {entries[i].Key}");

                // Roll back previously written entries to prevent a mixed old/new tuple
                (string Key, object Value)[] rollback =
                [
                    (SETTING_KEY_X, oldX),
                    (SETTING_KEY_Y, oldY),
                    (SETTING_KEY_WIDTH, oldW),
                    (SETTING_KEY_HEIGHT, oldH),
                ];

                for (int j = 0; j < i; j++)
                {
                    if (!_settings.SetValue(rollback[j].Key, rollback[j].Value))
                    {
                        Trace.WriteLine($"Failed to roll back window position for {rollback[j].Key}");
                    }
                }

                return false;
            }
        }

        return true;
    }
}
