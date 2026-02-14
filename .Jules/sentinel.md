## 2026-01-14 - Mixed Slash UNC Paths
**Vulnerability:** `IconService.IsUnsafePath` relied on checking `\\` and `//` but missed mixed-slash UNC paths (e.g., `/\attacker` or `\/attacker`), which Windows APIs treat as valid UNC paths, potentially leading to NTLM credential theft.
**Learning:** `Uri.IsUnc` behavior differs across platforms (Linux vs Windows). On Linux, `Uri` correctly flags `/\` as UNC, but on Windows, it might throw or fail to detect it if malformed, requiring explicit string checks for robust security.
**Prevention:** Always normalize paths using `Path.GetFullPath` before security checks, and explicitly check for all UNC start patterns (`\\`, `//`, `/\`, `\/`) when dealing with Windows file paths.
