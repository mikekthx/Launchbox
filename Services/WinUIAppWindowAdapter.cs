using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using System;
using System.Diagnostics.CodeAnalysis;

namespace Launchbox.Services;

[ExcludeFromCodeCoverage]

/// <summary>
/// Production adapter that wraps <see cref="AppWindow"/> and a WinUI <see cref="Window"/>
/// to implement <see cref="IAppWindowAdapter"/>.
/// </summary>
public sealed class WinUIAppWindowAdapter : IAppWindowAdapter, IDisposable
{
    private readonly AppWindow _appWindow;
    private readonly Window _window;
    private bool _disposed;

    public WinUIAppWindowAdapter(AppWindow appWindow, Window window)
    {
        ArgumentNullException.ThrowIfNull(appWindow);
        ArgumentNullException.ThrowIfNull(window);
        _appWindow = appWindow;
        _window = window;
        _appWindow.Changed += AppWindow_Changed;
    }

    public bool IsVisible => _window.Visible;

    public Windows.Graphics.SizeInt32 Size => _appWindow.Size;

    public Windows.Graphics.PointInt32 Position => _appWindow.Position;

    public event EventHandler? PositionOrSizeChanged;

    private void AppWindow_Changed(AppWindow sender, AppWindowChangedEventArgs args)
    {
        if (args.DidPositionChange || args.DidSizeChange)
        {
            PositionOrSizeChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public void Show() => _appWindow.Show();

    public void Hide() => _appWindow.Hide();

    public void Move(Windows.Graphics.PointInt32 point) => _appWindow.Move(point);

    public void Resize(Windows.Graphics.SizeInt32 size) => _appWindow.Resize(size);

    public void MoveAndResize(Windows.Graphics.RectInt32 rect) => _appWindow.MoveAndResize(rect);

    public Windows.Graphics.RectInt32 GetWorkArea()
    {
        var displayArea = DisplayArea.GetFromWindowId(_appWindow.Id, DisplayAreaFallback.Primary);
        return displayArea.WorkArea;
    }

    public bool IsRectOnAnyDisplay(Windows.Graphics.RectInt32 rect)
    {
        var displayArea = DisplayArea.GetFromRect(rect, DisplayAreaFallback.None);
        return displayArea != null;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _appWindow.Changed -= AppWindow_Changed;
    }
}
