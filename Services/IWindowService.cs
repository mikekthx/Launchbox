using System;

namespace Launchbox.Services;

public interface IWindowService : IDisposable
{
    /// <summary>
    /// Gets a value indicating whether the window is currently visible.
    /// </summary>
    bool IsVisible { get; }

    /// <summary>
    /// Occurs when the window visibility changes.
    /// </summary>
    event EventHandler<bool>? VisibilityChanged;

    /// <summary>
    /// Occurs immediately before the window is shown (before AppWindow.Show is called).
    /// Subscribe here to set state that must be ready before the first Activated event fires.
    /// </summary>
    event EventHandler? Showing;

    /// <summary>
    /// Occurs when registering a global hotkey fails.
    /// </summary>
    event EventHandler<string>? HotkeyRegistrationFailed;

    /// <summary>
    /// Initializes the window service, setting up the window handle, app window, hotkeys, and window procedure hook.
    /// </summary>
    void Initialize();

    /// <summary>
    /// Handles the window activation state change, hiding the window when deactivated.
    /// </summary>
    /// <param name="isDeactivated">True when the window is losing focus (deactivated).</param>
    void OnActivated(bool isDeactivated);

    /// <summary>
    /// Toggles the visibility of the window. Shows and activates it if hidden, or hides it if visible.
    /// </summary>
    void ToggleVisibility();

    /// <summary>
    /// Resets the window position to the center of the screen and saves the position.
    /// </summary>
    void ResetPosition();

    /// <summary>
    /// Hides the window.
    /// </summary>
    void Hide();

    /// <summary>
    /// Exits the application by closing the main window.
    /// </summary>
    void Exit();

    /// <summary>
    /// Opens the settings window, or activates it if already open.
    /// </summary>
    void OpenSettings();
}
