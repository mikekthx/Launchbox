using Launchbox.Services;

namespace Launchbox.Tests;

public class MockBackdropWindowWrapper : IBackdropWindowWrapper
{
    public bool IsBackdropSet { get; set; }
    public bool IsDesktopAcrylicBackdropSet { get; set; }

    public bool ClearBackdropCalled { get; private set; }
    public bool SetDesktopAcrylicBackdropCalled { get; private set; }

    public void ClearBackdrop()
    {
        ClearBackdropCalled = true;
        IsBackdropSet = false;
        IsDesktopAcrylicBackdropSet = false;
    }

    public void SetDesktopAcrylicBackdrop()
    {
        SetDesktopAcrylicBackdropCalled = true;
        IsBackdropSet = true;
        IsDesktopAcrylicBackdropSet = true;
    }
}
