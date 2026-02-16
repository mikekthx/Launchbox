## 2025-02-18 - [Reliability] Prevent premature disposal of BitmapImage source stream
**Learning:** In WinUI/UWP, `BitmapImage.SetSourceAsync` requires the stream to remain open for the lifetime of the `BitmapImage`. Disposing the stream immediately (via `using`) can cause silent rendering failures or crashes if the image is accessed later.
**Action:** Avoid `using` on `MemoryStream` when passing it to `BitmapImage.SetSourceAsync`. `MemoryStream` (over byte array) holds no unmanaged resources and is safe for GC.
