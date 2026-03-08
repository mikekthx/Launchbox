using Launchbox.Services;
using Microsoft.UI.Xaml;
using System;

namespace Launchbox.Tests;

public class MockWindowService : IWindowService
{
    public bool IsVisible { get; set; }
    public event EventHandler<bool>? VisibilityChanged;
    public event EventHandler<string>? HotkeyRegistrationFailed;

    public void RaiseVisibilityChanged(bool isVisible)
    {
        IsVisible = isVisible;
        VisibilityChanged?.Invoke(this, isVisible);
    }

    public bool HideCalled { get; private set; }
    public bool ToggleVisibilityCalled { get; private set; }
    public bool InitializeCalled { get; private set; }
    public bool OnActivatedCalled { get; private set; }
    public bool ExitCalled { get; private set; }
    public bool OpenSettingsCalled { get; private set; }

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
    public void Hide()
    {
        HideCalled = true;
    }

    public void Dispose()
    {
    }

    public void Exit()
    {
        ExitCalled = true;
    }

    public void OpenSettings()
    {
        OpenSettingsCalled = true;
    }
}
