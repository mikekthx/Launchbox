## 2026-02-18 - [Reliability] Prevent premature disposal of BitmapImage source stream
**Learning:** Custom window dragging logic that relies solely on `PointerReleased` can leave the app in a "stuck" dragging state if the interaction is interrupted by system events (e.g., Alt-Tab, notifications) which fire `PointerCanceled` or `PointerCaptureLost` instead.
**Action:** Explicitly handle `PointerCanceled` and `PointerCaptureLost` events to reset dragging state and release pointer capture, ensuring the UI recovers gracefully from interruptions.

## 2026-03-08 - [Reliability] Prevent memory leaks in MainWindow due to unsubscribed events
**Learning:** In WinUI 3/UWP, failing to explicitly unsubscribe from UI event handlers (like `PointerPressed`) or Window events (like `Activated`, `Closed`) when a window is closed can lead to memory leaks due to circular references between managed C# wrappers and native C++ objects.
**Action:** Always explicitly unsubscribe (`-=`) from all dynamically attached event handlers during window disposal or closure (`Closed` event).