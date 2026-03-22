using Launchbox.Services;
using System;

namespace Launchbox.Tests;

public class MockBackdropWindowWrapper : IBackdropWindowWrapper
{
    public bool IsBackdropSet { get; set; }
    public bool IsDesktopAcrylicBackdropSet { get; set; }

    public bool ClearBackdropCalled { get; private set; }
    public bool SetDesktopAcrylicBackdropCalled { get; private set; }
    public bool ShouldThrowOnClearBackdrop { get; set; }

    public void ClearBackdrop()
    {
        ClearBackdropCalled = true;
        if (ShouldThrowOnClearBackdrop) throw new Exception("Simulated error in ClearBackdrop");
        IsBackdropSet = false;
        IsDesktopAcrylicBackdropSet = false;
    }

    public bool ShouldThrowOnSetDesktopAcrylicBackdrop { get; set; }

    public void SetDesktopAcrylicBackdrop()
    {
        SetDesktopAcrylicBackdropCalled = true;
        if (ShouldThrowOnSetDesktopAcrylicBackdrop) throw new Exception("Simulated error in SetDesktopAcrylicBackdrop");
        IsBackdropSet = true;
        IsDesktopAcrylicBackdropSet = true;
    }
}
