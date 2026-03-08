## 2024-05-23 - Avoiding Redundant FileExists Checks
**Learning:** Checking `File.Exists(path)` before `File.GetLastWriteTime(path)` is redundant because `GetLastWriteTime` returns a specific default value (1601-01-01) for missing files. Combining this with a high-level `Directory.Exists` check for optional resource folders (like `.icons`) can significantly reduce syscalls in tight loops.
**Action:** When checking for optional files, rely on the "missing" return value of the data-fetching method (e.g. timestamp, handle, null) rather than a separate existence check, and guard groups of checks with a single directory existence check if possible.
## 2026-01-14 - WinRT Stream Optimization
**Learning:** `InMemoryRandomAccessStream` coupled with `DataWriter` incurs unnecessary double-copying and overhead when wrapping an existing `byte[]`.
**Action:** Use `System.IO.MemoryStream` (wrapping the byte array) combined with the `.AsRandomAccessStream()` extension method for significantly faster and lighter stream creation when feeding `BitmapImage.SetSourceAsync`.

## 2026-03-08 - Prevent ThreadPool Starvation in MainViewModel
**Learning:** In bounded concurrent loops (`Parallel.ForEachAsync` with a low `MaxDegreeOfParallelism`), executing synchronous blocking I/O calls directly within the loop body can severely starve the thread pool. This happens because the worker threads are blocked entirely waiting for I/O instead of yielding back to the async state machine to schedule continuations or other application tasks.
**Action:** When forced to use synchronous I/O within async loops, wrap the blocking call (e.g., `_iconService.ExtractIconBytes`) inside an `await Task.Run(...)`. This offloads the blocking work to background threads, yielding the worker thread and ensuring responsiveness in the UI dispatcher.
