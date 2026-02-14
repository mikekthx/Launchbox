using Launchbox.Services;

namespace Launchbox.Tests;

public class MockWindowService : IWindowService
{
    public bool HideCalled { get; private set; }
    public bool ToggleVisibilityCalled { get; private set; }

    public void ToggleVisibility()
    {
        ToggleVisibilityCalled = true;
    }

    public void ResetPosition() { }
    public void Cleanup() { }
    public void Hide()
    {
        HideCalled = true;
    }
}
