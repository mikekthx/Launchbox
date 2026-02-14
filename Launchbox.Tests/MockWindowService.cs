using Launchbox.Services;

namespace Launchbox.Tests;

public class MockWindowService : IWindowService
{
    public void ToggleVisibility() { }
    public void ResetPosition() { }
    public void Cleanup() { }
}
