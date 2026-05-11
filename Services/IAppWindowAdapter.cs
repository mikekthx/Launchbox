using System;

namespace Launchbox.Services;

public interface IAppWindowAdapter
{
    bool IsVisible { get; }
    Windows.Graphics.SizeInt32 Size { get; }
    Windows.Graphics.PointInt32 Position { get; }
    event EventHandler? PositionOrSizeChanged;
    void Show();
    void Hide();
    void Move(Windows.Graphics.PointInt32 point);
    void Resize(Windows.Graphics.SizeInt32 size);
    void MoveAndResize(Windows.Graphics.RectInt32 rect);
    Windows.Graphics.RectInt32 GetWorkArea();
    Windows.Graphics.RectInt32 GetWorkAreaForPoint(Windows.Graphics.PointInt32 point);
    bool IsRectOnAnyDisplay(Windows.Graphics.RectInt32 rect);
}
