using Launchbox.Services;
using Microsoft.UI.Xaml;

namespace Launchbox.Tests;

public class MockWindowService : IWindowService
{
    public bool HideCalled { get; private set; }
    public bool ToggleVisibilityCalled { get; private set; }
    public bool InitializeCalled { get; private set; }
    public bool OnActivatedCalled { get; private set; }

    public void Initialize()
    {
        InitializeCalled = true;
    }

    public void OnActivated(WindowActivatedEventArgs args)
    {
        OnActivatedCalled = true;
    }

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
