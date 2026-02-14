## 2024-05-23 - Avoiding Redundant FileExists Checks
**Learning:** Checking `File.Exists(path)` before `File.GetLastWriteTime(path)` is redundant because `GetLastWriteTime` returns a specific default value (1601-01-01) for missing files. Combining this with a high-level `Directory.Exists` check for optional resource folders (like `.icons`) can significantly reduce syscalls in tight loops.
**Action:** When checking for optional files, rely on the "missing" return value of the data-fetching method (e.g. timestamp, handle, null) rather than a separate existence check, and guard groups of checks with a single directory existence check if possible.
## 2026-01-14 - WinRT Stream Optimization
**Learning:** `InMemoryRandomAccessStream` coupled with `DataWriter` incurs unnecessary double-copying and overhead when wrapping an existing `byte[]`.
**Action:** Use `System.IO.MemoryStream` (wrapping the byte array) combined with the `.AsRandomAccessStream()` extension method for significantly faster and lighter stream creation when feeding `BitmapImage.SetSourceAsync`.
