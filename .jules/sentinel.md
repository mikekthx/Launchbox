## 2026-01-14 - Mixed Slash UNC Paths
**Vulnerability:** `IconService.IsUnsafePath` relied on checking `\\` and `//` but missed mixed-slash UNC paths (e.g., `/\attacker` or `\/attacker`), which Windows APIs treat as valid UNC paths, potentially leading to NTLM credential theft.
**Learning:** `Uri.IsUnc` behavior differs across platforms (Linux vs Windows). On Linux, `Uri` correctly flags `/\` as UNC, but on Windows, it might throw or fail to detect it if malformed, requiring explicit string checks for robust security.
**Prevention:** Always normalize paths using `Path.GetFullPath` before security checks, and explicitly check for all UNC start patterns (`\\`, `//`, `/\`, `\/`) when dealing with Windows file paths.

## 2025-05-23 - NTLM Leak via Direct UNC Paths
**Vulnerability:** `IconService.ExtractIconBytes` accepted direct UNC paths, bypassing `ResolveIconPath` checks for `.url` files, potentially leaking NTLM credentials via `GetLastWriteTime` and `PrivateExtractIcons`.
**Learning:** Input sanitization must be applied at the entry point of public methods (`ExtractIconBytes`), not just in helper methods (`ResolveIconPath`) or specific file types (`.url`).
**Prevention:** Validate all file paths against `IsUnsafePath` immediately upon entry in `IconService` methods before any file system access.
