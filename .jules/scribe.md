## 2026-02-17 - Icon Selection Strategy
**Learning:** `IconService` implements a dual-file check strategy for custom icons, looking for both `.png` and `.ico` files. It resolves the final icon by comparing pixel area (width * height), preferring the higher resolution image. If resolutions are identical, it defaults to the `.png` file for better modern compatibility.
**Action:** When modifying icon logic, always maintain this resolution-based preference and the fallback to PNG.
