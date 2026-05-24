## 2026-02-18 - [Reliability] Prevent premature disposal of BitmapImage source stream
**Learning:** In WinUI/UWP, `BitmapImage.SetSourceAsync` requires the stream to remain open for the lifetime of the `BitmapImage`. Disposing the stream immediately (via `using`) can cause silent rendering failures or crashes if the image is accessed later.
**Action:** Avoid `using` on `MemoryStream` when passing it to `BitmapImage.SetSourceAsync`. `MemoryStream` (over byte array) holds no unmanaged resources and is safe for GC.

## 2026-02-18 - [Reliability] GDI+ Concurrency Instability
**Learning:** `System.Drawing` (GDI+) functions like `Icon.FromHandle`, `Icon.ToBitmap`, and `Image.Save` are not thread-safe and can cause `ExternalException` or random crashes when invoked concurrently from multiple threads (e.g., `Parallel.ForEachAsync`), even on separate instances.
**Action:** Wrap GDI+ operations in a `lock` block when used in parallel execution paths to serialize access to the underlying GDI+ subsystem.

## 2026-02-18 - [Reliability] Expand Environment Variables in File Paths
**Learning:** `File.Exists` and similar APIs do not automatically expand environment variables (e.g., `%SystemRoot%`). Paths retrieved from INI files or shortcuts often contain these variables, leading to silent failures (file not found) if not explicitly expanded using `Environment.ExpandEnvironmentVariables`.
**Action:** Always call `Environment.ExpandEnvironmentVariables` on file paths originating from external configuration (INI, Registry, User Input) before using them in file system operations.

## 2024-05-23 - [LocalSettingsStore Reliability]
**Learning:** `ApplicationDataContainer.Values` operations (accessing/setting keys) can throw exceptions (e.g. COMException, quota exceeded, file system errors), which crashes the app if unhandled on the UI thread.
**Action:** Wrap all `LocalSettings` interactions in `try-catch` blocks and log failures instead of crashing.

## 2026-02-18 - [UI Reliability] Handle Pointer Capture Loss
**Learning:** Custom window dragging logic that relies solely on `PointerReleased` can leave the app in a "stuck" dragging state if the interaction is interrupted by system events (e.g., Alt-Tab, notifications) which fire `PointerCanceled` or `PointerCaptureLost` instead.
**Action:** Explicitly handle `PointerCanceled` and `PointerCaptureLost` events to reset dragging state and release pointer capture, ensuring the UI recovers gracefully from interruptions.

## 2026-03-08 - [Reliability] Prevent memory leaks in MainWindow due to unsubscribed events
**Learning:** In WinUI 3/UWP, failing to explicitly unsubscribe from UI event handlers (like `PointerPressed`) or Window events (like `Activated`, `Closed`) when a window is closed can lead to memory leaks due to circular references between managed C# wrappers and native C++ objects.
**Action:** Always explicitly unsubscribe (`-=`) from all dynamically attached event handlers during window disposal or closure (`Closed` event).

## 2026-03-22 - [Reliability] Prevent unhandled exceptions in async void methods
**Learning:** In WinUI/WPF applications, unhandled exceptions within `async void` event handlers (e.g., calling `await dialog.ShowAsync()`) bypass standard exception handling and crash the application because they cannot be observed by an awaiter.
**Action:** Always wrap the bodies of `async void` methods in a `try/catch` block and log the exception rather than letting it crash the entire process.

## 2026-04-05 - [Reliability] Prevent UI desync on persistence failure
**Learning:** In the `SettingsService`, if a `SetValue` write to the underlying store fails, the `OnPropertyChanged` notification must still be triggered. This forces the UI (via the ViewModel) to re-read the setting from the store, which will return the old value and revert the UI's optimistic state.
**Action:** Always call `OnPropertyChanged` after an attempted change in `SettingsService`, even if the write operation returns `false`.
