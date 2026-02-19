## 2026-02-18 - [Reliability] Prevent premature disposal of BitmapImage source stream
**Learning:** In WinUI/UWP, `BitmapImage.SetSourceAsync` requires the stream to remain open for the lifetime of the `BitmapImage`. Disposing the stream immediately (via `using`) can cause silent rendering failures or crashes if the image is accessed later.
**Action:** Avoid `using` on `MemoryStream` when passing it to `BitmapImage.SetSourceAsync`. `MemoryStream` (over byte array) holds no unmanaged resources and is safe for GC.

## 2026-02-18 - [Reliability] GDI+ Concurrency Instability
**Learning:** `System.Drawing` (GDI+) functions like `Icon.FromHandle`, `Icon.ToBitmap`, and `Image.Save` are not thread-safe and can cause `ExternalException` or random crashes when invoked concurrently from multiple threads (e.g., `Parallel.ForEachAsync`), even on separate instances.
**Action:** Wrap GDI+ operations in a `lock` block when used in parallel execution paths to serialize access to the underlying GDI+ subsystem.

## 2026-02-18 - [Reliability] Expand Environment Variables in File Paths
**Learning:** `File.Exists` and similar APIs do not automatically expand environment variables (e.g., `%SystemRoot%`). Paths retrieved from INI files or shortcuts often contain these variables, leading to silent failures (file not found) if not explicitly expanded using `Environment.ExpandEnvironmentVariables`.
**Action:** Always call `Environment.ExpandEnvironmentVariables` on file paths originating from external configuration (INI, Registry, User Input) before using them in file system operations.
