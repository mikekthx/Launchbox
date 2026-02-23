using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace Launchbox.Services;

public class BackdropWindowWrapper : IBackdropWindowWrapper
{
    private readonly Window _window;

    public BackdropWindowWrapper(Window window)
    {
        _window = window;
    }

    public bool IsBackdropSet => _window.SystemBackdrop != null;

    public bool IsDesktopAcrylicBackdropSet => _window.SystemBackdrop is DesktopAcrylicBackdrop;

    public void ClearBackdrop()
    {
        _window.SystemBackdrop = null;
    }

    public void SetDesktopAcrylicBackdrop()
    {
        _window.SystemBackdrop = new DesktopAcrylicBackdrop();
    }
}
