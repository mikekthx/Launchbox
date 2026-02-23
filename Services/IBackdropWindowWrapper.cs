namespace Launchbox.Services;

public interface IBackdropWindowWrapper
{
    bool IsBackdropSet { get; }
    bool IsDesktopAcrylicBackdropSet { get; }
    void ClearBackdrop();
    void SetDesktopAcrylicBackdrop();
}
