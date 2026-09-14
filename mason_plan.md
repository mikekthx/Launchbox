1. **Move Window Dragging logic from `MainWindow.xaml.cs` to `IWindowService` (and its implementation `WindowService.cs`)**
   - The dragging of the window requires maintaining state (like `_isDraggingWindow`, `_dragStartWindowPos`, `_dragStartCursorPos`) and calling OS-level/WinUI APIs (`NativeMethods.GetCursorPos`, `AppWindow.Move`). Right now, all of this lives directly in `MainWindow.xaml.cs`.
   - `IWindowService` handles `Show`, `Hide`, `CenterWindow`, `ClampToWorkArea`, etc. Moving the dragging logic there keeps all window-manipulation responsibility in one place, following the Single Responsibility Principle and reducing code-behind in the view.
   - We will define `StartDrag`, `UpdateDrag`, and `EndDrag` methods on `IWindowService`.
   - Update `MainWindow.xaml.cs` pointer event handlers to just call into `_windowService`.
