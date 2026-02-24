## 2026-02-17 - Icon Selection Strategy
**Learning:** `IconService` implements a dual-file check strategy for custom icons, looking for both `.png` and `.ico` files. It resolves the final icon by comparing pixel area (width * height), preferring the higher resolution image. If resolutions are identical, it defaults to the `.png` file for better modern compatibility.
**Action:** When modifying icon logic, always maintain this resolution-based preference and the fallback to PNG.

## 2024-05-22 - Concurrency Cache Pattern
**Learning:** `IconService` uses a complex `while(true)` loop with `ConcurrentDictionary<TKey, Lazy<TValue>>` to implement cache expiration. This pattern handles race conditions where multiple threads might access an expired entry simultaneously, but it adds significant cognitive load.
**Action:** When encountering this pattern, encapsulate it in a dedicated helper method or class to hide the complexity from the business logic.
