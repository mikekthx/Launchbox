## 2026-01-14 - WinRT Stream Optimization
**Learning:** `InMemoryRandomAccessStream` coupled with `DataWriter` incurs unnecessary double-copying and overhead when wrapping an existing `byte[]`.
**Action:** Use `System.IO.MemoryStream` (wrapping the byte array) combined with the `.AsRandomAccessStream()` extension method for significantly faster and lighter stream creation when feeding `BitmapImage.SetSourceAsync`.
