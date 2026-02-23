using Microsoft.UI.Xaml;

namespace Launchbox.Services;

public interface IWindowService
{
    /// <summary>
    /// Initializes the window service, setting up the window handle, app window, hotkeys, and window procedure hook.
    /// </summary>
    void Initialize();

    /// <summary>
    /// Handles the window activated event, hiding the window when deactivated.
    /// </summary>
    /// <param name="args">The window activation event arguments.</param>
    void OnActivated(WindowActivatedEventArgs args);

    /// <summary>
    /// Toggles the visibility of the window. Shows and activates it if hidden, or hides it if visible.
    /// </summary>
    void ToggleVisibility();

    /// <summary>
    /// Resets the window position to the center of the screen and saves the position.
    /// </summary>
    void ResetPosition();

    /// <summary>
    /// Cleans up resources, unregisters hotkeys, and restores the original window procedure.
    /// </summary>
    void Cleanup();

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
