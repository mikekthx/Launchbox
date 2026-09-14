1. **Modify `IWindowService.cs` to add dragging contract**
   - Use Python script replacement to add `void StartDrag();`, `void UpdateDrag();`, and `void EndDrag();` to the interface.

2. **Modify `WindowService.cs` to implement window dragging state and logic**
   - Add the dragging state variables (`_isDraggingWindow`, `_dragStartWindowPos`, `_dragStartCursorPos`) that currently live in `MainWindow.xaml.cs`.
   - Implement `StartDrag()`, `UpdateDrag()`, and `EndDrag()` using Python script string replacement.

3. **Modify `MainWindow.xaml.cs` to delegate dragging to `IWindowService`**
   - Remove the dragging state variables.
   - Update `RootGrid_PointerPressed`, `RootGrid_PointerMoved`, `RootGrid_PointerReleased`, and `RootGrid_PointerCaptureLost` to delegate the core logic to `_windowService.StartDrag()`, `_windowService.UpdateDrag()`, and `_windowService.EndDrag()` using python string replacements.
   - We will preserve the UI-specific `GridViewItem` hit-testing check and `CapturePointer`/`ReleasePointerCapture` in the view's event handlers.

4. **Verify changes by building and testing**
   - Run `dotnet build` and `dotnet test` in bash to verify everything builds correctly.

5. **Pre-commit step**
   - Complete pre-commit steps to ensure proper testing, verification, review, and reflection are done.

6. **Submit PR**
   - Use the `submit` tool to create the PR with the Mason title and details.
