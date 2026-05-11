using Launchbox.Services;
using System;
using System.Collections.Generic;

namespace Launchbox.Tests;

public class MockAppWindowAdapter : IAppWindowAdapter
{
    public bool IsVisible { get; set; }
    public Windows.Graphics.SizeInt32 Size { get; private set; } = new Windows.Graphics.SizeInt32(650, 700);
    public Windows.Graphics.PointInt32 Position { get; private set; }
    public Windows.Graphics.RectInt32 WorkArea { get; set; } = new Windows.Graphics.RectInt32(0, 0, 1920, 1040);
    public bool RectOnAnyDisplay { get; set; } = true;

    public int ShowCount { get; private set; }
    public int HideCount { get; private set; }
    public List<Windows.Graphics.SizeInt32> ResizeCalls { get; } = [];
    public List<Windows.Graphics.PointInt32> MoveCalls { get; } = [];
    public List<Windows.Graphics.RectInt32> MoveAndResizeCalls { get; } = [];

    public event EventHandler? PositionOrSizeChanged;

    public void Show() => ShowCount++;
    public void Hide() => HideCount++;

    public void Move(Windows.Graphics.PointInt32 point)
    {
        Position = point;
        MoveCalls.Add(point);
    }

    public void Resize(Windows.Graphics.SizeInt32 size)
    {
        Size = size;
        ResizeCalls.Add(size);
    }

    public void MoveAndResize(Windows.Graphics.RectInt32 rect)
    {
        Position = new Windows.Graphics.PointInt32(rect.X, rect.Y);
        Size = new Windows.Graphics.SizeInt32(rect.Width, rect.Height);
        MoveAndResizeCalls.Add(rect);
    }

    public Windows.Graphics.RectInt32 GetWorkArea() => WorkArea;
    public Windows.Graphics.RectInt32 GetWorkAreaForPoint(Windows.Graphics.PointInt32 point) => WorkArea;
    public bool IsRectOnAnyDisplay(Windows.Graphics.RectInt32 rect) => RectOnAnyDisplay;

    public void FirePositionOrSizeChanged() => PositionOrSizeChanged?.Invoke(this, EventArgs.Empty);
}
