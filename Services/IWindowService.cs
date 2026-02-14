namespace Launchbox.Services;

public interface IWindowService
{
    void ToggleVisibility();
    void ResetPosition();
    void Cleanup();
    void Hide();
}
