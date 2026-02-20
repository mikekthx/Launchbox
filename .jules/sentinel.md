## 2026-02-17 - Unrestricted File Execution
**Vulnerability:** `WinUILauncher.Launch` permitted execution of any file with `.lnk` or `.url` extension, regardless of path (including UNC paths), enabling NTLM leaks and remote code execution via network shares.
**Learning:** Checking file extensions alone is insufficient; path origin must also be validated to prevent loading from untrusted zones (e.g., SMB shares).
**Prevention:** Integrate centralized path validation (`PathSecurity.IsUnsafePath`) into all process launching mechanisms (`Process.Start`).

## 2026-01-14 - Mixed Slash UNC Paths
**Vulnerability:** `IconService.IsUnsafePath` relied on checking `\\` and `//` but missed mixed-slash UNC paths (e.g., `/\attacker` or `\/attacker`), which Windows APIs treat as valid UNC paths, potentially leading to NTLM credential theft.
**Learning:** `Uri.IsUnc` behavior differs across platforms (Linux vs Windows). On Linux, `Uri` correctly flags `/\` as UNC, but on Windows, it might throw or fail to detect it if malformed, requiring explicit string checks for robust security.
**Prevention:** Always normalize paths using `Path.GetFullPath` before security checks, and explicitly check for all UNC start patterns (`\\`, `//`, `/\`, `\/`) when dealing with Windows file paths.

## 2025-05-23 - NTLM Leak via Direct UNC Paths
**Vulnerability:** `IconService.ExtractIconBytes` accepted direct UNC paths, bypassing `ResolveIconPath` checks for `.url` files, potentially leaking NTLM credentials via `GetLastWriteTime` and `PrivateExtractIcons`.
**Learning:** Input sanitization must be applied at the entry point of public methods (`ExtractIconBytes`), not just in helper methods (`ResolveIconPath`) or specific file types (`.url`).
**Prevention:** Validate all file paths against `IsUnsafePath` immediately upon entry in `IconService` methods before any file system access.

## 2026-06-25 - Icon Path Environment Variables
**Vulnerability:** `IconService.ResolveIconPath` failed to expand environment variables (e.g., `%SystemRoot%`) before file validation, causing failure for system icons and potentially masking malicious paths hidden behind variables.
**Learning:** `File.Exists` does not expand environment variables, and unexpanded paths might bypass path security checks intended for the resolved path.
**Prevention:** Always expand environment variables (`Environment.ExpandEnvironmentVariables`) immediately when retrieving paths from external configuration (e.g., INI files) before validation.

## 2026-06-26 - Invalid File Execution & Symlinks
**Vulnerability:** `WinUILauncher.Launch` allowed execution without verifying file existence, potentially enabling directory traversal or phantom path execution. Additionally, `GetPrivateProfileString` (used in `GetIniValue`) can access NTLM shares via symlinks even if the path string looks local.
**Learning:** `File.Exists` returns true for symlinks, so `IsUnsafePath` string checks can be bypassed by local symlinks pointing to UNC shares.
**Prevention:** Enforce `File.Exists` for basic validation, and consider checking `FileAttributes.ReparsePoint` before reading sensitive file formats (like INI) to prevent redirect attacks.
