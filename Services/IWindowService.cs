using Microsoft.UI.Xaml;

namespace Launchbox.Services;

public interface IWindowService
{
    void Initialize();
    void OnActivated(WindowActivatedEventArgs args);
    void ToggleVisibility();
    void ResetPosition();
    void Cleanup();
    void Hide();
}
