## 2026-02-17 - Icon Selection Strategy
**Learning:** `IconService` implements a dual-file check strategy for custom icons, looking for both `.png` and `.ico` files. It resolves the final icon by comparing pixel area (width * height), preferring the higher resolution image. If resolutions are identical, it defaults to the `.png` file for better modern compatibility.
**Action:** When modifying icon logic, always maintain this resolution-based preference and the fallback to PNG.

## 2024-05-22 - Concurrency Cache Pattern
**Learning:** `IconService` uses a complex `while(true)` loop with `ConcurrentDictionary<TKey, Lazy<TValue>>` to implement cache expiration. This pattern handles race conditions where multiple threads might access an expired entry simultaneously, but it adds significant cognitive load.
**Action:** When encountering this pattern, encapsulate it in a dedicated helper method or class to hide the complexity from the business logic.

## 2026-03-24 - GetWithExpirationRetry Clarity
**Learning:** `IconService` uses a complex `GetWithExpirationRetry` method to handle cache expiration in a concurrent environment. This replaces a previous `while(true)` loop. The method signature and logic are generic but conceptually dense. The inline lambdas for cache expiration logic in `GetCachedDirectoryInfo` and `GetCachedLastWriteTime` are particularly verbose.
**Action:** No direct action needed as the pattern is already encapsulated into a helper method, but this remains a complex area.

## 2026-05-26 - Refactoring Multi-line Code via Bash
**Learning:** Using  with inline heredocs for code replacements can often fail due to malformed patch formats or whitespace issues.
**Action:** Prefer using a targeted Python script with exact  to reliably swap multi-line blocks of code during automated refactoring sessions.

## 2026-05-26 - Refactoring Multi-line Code via Bash
**Learning:** Using `patch` with inline heredocs for code replacements can often fail due to malformed patch formats or whitespace issues.
**Action:** Prefer using a targeted Python script with exact `content.replace()` to reliably swap multi-line blocks of code during automated refactoring sessions.
