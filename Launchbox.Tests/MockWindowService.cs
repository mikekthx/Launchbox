using Launchbox.Services;
using System;

namespace Launchbox.Tests;

public class MockWindowService : IWindowService
{
    public bool IsVisible { get; set; }
    public event EventHandler<bool>? VisibilityChanged;
    // Suppress CS0067: events required by IWindowService but not raised in most tests
#pragma warning disable CS0067
    public event EventHandler? Showing;
    public event EventHandler<string>? HotkeyRegistrationFailed;
#pragma warning restore CS0067

    public void RaiseVisibilityChanged(bool isVisible)
    {
        IsVisible = isVisible;
        VisibilityChanged?.Invoke(this, isVisible);
    }

    public bool HideCalled { get; private set; }
    public bool ToggleVisibilityCalled { get; private set; }
    public bool OpenSettingsCalled { get; private set; }

    public void Initialize() { }

    public void OnActivated(bool isDeactivated) { }

    public void ToggleVisibility()
    {
        ToggleVisibilityCalled = true;
    }

    public void ResetPosition() { }
    public void Hide()
    {
        HideCalled = true;
    }

    public void Dispose() { }

    public void Exit() { }

    public void OpenSettings()
    {
        OpenSettingsCalled = true;
    }
}
