# Deduplicate COM Interop in Launch Pipeline

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development (if subagents available) or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Eliminate the double COM shortcut resolution that happens on every `.lnk` launch by making `WinUILauncher` the sole owner of shortcut validation and removing `IShortcutResolver` from `ProcessStarter`.

**Architecture:** Today, `WinUILauncher.Launch()` resolves a `.lnk` via COM to validate target/args/workingDir, then calls `ProcessStarter.Start()`, which resolves the *same* `.lnk` via COM again to re-validate args/workingDir. The fix removes the `IShortcutResolver` dependency from `ProcessStarter` entirely. `ProcessStarter` keeps its `ProcessStartInfo`-level guards (unsafe FileName, Arguments, WorkingDirectory) but no longer resolves shortcuts — that's `WinUILauncher`'s job. This halves the COM work per launch and eliminates the risk of the two validation implementations drifting apart.

**Tech Stack:** C#, xUnit, COM interop (IShellLinkW)

---

## File Map

### Modified Files

| File | Changes |
|------|---------|
| `Services/ProcessStarter.cs` | Remove `IShortcutResolver` constructor parameter and `.lnk` resolution block |
| `Services/WinUILauncher.cs` | No changes — already owns all shortcut validation |
| `MainWindow.xaml.cs` | Update composition: stop passing `shortcutResolver` to `ProcessStarter` |
| `Launchbox.Tests/ProcessStarterTests.cs` | Update constructor calls (no resolver needed) |
| `Launchbox.Tests/ProcessStarterSecurityTests.cs` | Update constructor calls (no resolver needed) |
| `Launchbox.Tests/WinUILauncherSecurityTests.cs` | Add tests for `.lnk` with unsafe args/workingDir to confirm WinUILauncher catches what ProcessStarter no longer does |

### Unchanged Files

| File | Why unchanged |
|------|---------------|
| `Services/IProcessStarter.cs` | Interface doesn't mention resolver — `Start(ProcessStartInfo)` signature unchanged |
| `Services/WindowsShortcutResolver.cs` | Still used by `WinUILauncher` — untouched |
| `Services/IShortcutResolver.cs` | Still used by `WinUILauncher` — untouched |
| `Launchbox.Tests/MockProcessStarter.cs` | Mock implements `IProcessStarter` which is unchanged |
| `Launchbox.Tests/MockShortcutResolver.cs` | Still used by `WinUILauncherSecurityTests` — untouched |

---

## Task 1: Add WinUILauncher Tests for Shortcut Args/WorkingDir Validation

Before removing ProcessStarter's shortcut resolution, prove that WinUILauncher already covers these cases. These tests must pass *before* any production code changes — they document the existing safety net.

**Files:**
- Modify: `Launchbox.Tests/MockShortcutResolver.cs`
- Modify: `Launchbox.Tests/WinUILauncherSecurityTests.cs`

- [ ] **Step 1: Extend MockShortcutResolver to support args and workingDir**

The current `MockShortcutResolver` only accepts a `target` parameter. To test WinUILauncher's args/workingDir validation, it needs to return full `ShortcutMetadata`.

In `Launchbox.Tests/MockShortcutResolver.cs`, replace the class with:

```csharp
using Launchbox.Services;

namespace Launchbox.Tests;

public class MockShortcutResolver : IShortcutResolver
{
    private readonly string? _target;
    private readonly string? _arguments;
    private readonly string? _workingDirectory;

    public MockShortcutResolver(string? target = null, string? arguments = null, string? workingDirectory = null)
    {
        _target = target;
        _arguments = arguments;
        _workingDirectory = workingDirectory;
    }

    public ShortcutMetadata? Resolve(string shortcutPath)
    {
        return new ShortcutMetadata(_target, _arguments, _workingDirectory);
    }
}
```

- [ ] **Step 2: Add tests for .lnk with unsafe arguments**

Add to `Launchbox.Tests/WinUILauncherSecurityTests.cs`:

```csharp
[Theory]
[InlineData(@"\\server\share\file")]
[InlineData("//server/share/file")]
[InlineData(@"\/server/share/file")]
[InlineData(@"/\server/share/file")]
public void Launch_Blocks_Lnk_With_UnsafeArguments(string unsafeArgs)
{
    var shortcutResolver = new MockShortcutResolver(
        target: @"C:\Program Files\App.exe",
        arguments: unsafeArgs);
    var launcher = new WinUILauncher(shortcutResolver, _processStarter, _fileSystem);

    _fileSystem.AddFile(@"C:\safe\shortcut.lnk");

    launcher.Launch(@"C:\safe\shortcut.lnk");

    Assert.False(_processStarter.WasStarted);
}
```

- [ ] **Step 3: Add test for .lnk with unsafe working directory**

Add to `Launchbox.Tests/WinUILauncherSecurityTests.cs`:

```csharp
[Fact]
public void Launch_Blocks_Lnk_With_UnsafeWorkingDirectory()
{
    var shortcutResolver = new MockShortcutResolver(
        target: @"C:\Program Files\App.exe",
        workingDirectory: @"\\attacker\share");
    var launcher = new WinUILauncher(shortcutResolver, _processStarter, _fileSystem);

    _fileSystem.AddFile(@"C:\safe\shortcut.lnk");

    launcher.Launch(@"C:\safe\shortcut.lnk");

    Assert.False(_processStarter.WasStarted);
}
```

- [ ] **Step 4: Add test for .lnk with safe args and workingDir**

Add to `Launchbox.Tests/WinUILauncherSecurityTests.cs`:

```csharp
[Fact]
public void Launch_Allows_Lnk_With_SafeArgumentsAndWorkingDirectory()
{
    var shortcutResolver = new MockShortcutResolver(
        target: @"C:\Program Files\App.exe",
        arguments: @"--verbose --output C:\temp",
        workingDirectory: @"C:\Program Files");
    var launcher = new WinUILauncher(shortcutResolver, _processStarter, _fileSystem);

    _fileSystem.AddFile(@"C:\safe\shortcut.lnk");

    launcher.Launch(@"C:\safe\shortcut.lnk");

    Assert.True(_processStarter.WasStarted);
}
```

- [ ] **Step 5: Add edge case test for .lnk with null target but unsafe args**

This covers the case where COM resolution fails to resolve a target (returns null) but the shortcut has embedded unsafe arguments. WinUILauncher allows null-target `.lnk` files through (line 55-61) but must still check args on lines 73-88.

Add to `Launchbox.Tests/WinUILauncherSecurityTests.cs`:

```csharp
[Fact]
public void Launch_Blocks_Lnk_With_NullTarget_But_UnsafeArguments()
{
    var shortcutResolver = new MockShortcutResolver(
        target: null,
        arguments: @"\\attacker\share\payload");
    var launcher = new WinUILauncher(shortcutResolver, _processStarter, _fileSystem);

    _fileSystem.AddFile(@"C:\safe\shortcut.lnk");

    launcher.Launch(@"C:\safe\shortcut.lnk");

    Assert.False(_processStarter.WasStarted);
}
```

- [ ] **Step 6: Run tests to verify they all pass**

Run: `dotnet test Launchbox.Tests/Launchbox.Tests.csproj --filter "FullyQualifiedName~WinUILauncherSecurityTests" -v n`
Expected: ALL PASS — WinUILauncher already validates args and workingDir (lines 73-88 of `WinUILauncher.cs`).

- [ ] **Step 7: Run full test suite to check for regressions from MockShortcutResolver change**

Run: `dotnet test Launchbox.Tests/Launchbox.Tests.csproj -v n`
Expected: ALL PASS — existing callers of `MockShortcutResolver(target)` still work because `arguments` and `workingDirectory` default to `null`.

- [ ] **Step 8: Commit**

```bash
git add Launchbox.Tests/MockShortcutResolver.cs Launchbox.Tests/WinUILauncherSecurityTests.cs
git commit -m "test: add WinUILauncher tests for shortcut args/workingDir validation"
```

---

## Task 2: Remove IShortcutResolver from ProcessStarter

Now that WinUILauncher's coverage is proven, remove the duplicate resolution from ProcessStarter.

**Files:**
- Modify: `Services/ProcessStarter.cs`
- Modify: `Launchbox.Tests/ProcessStarterTests.cs`
- Modify: `Launchbox.Tests/ProcessStarterSecurityTests.cs`

- [ ] **Step 1: Remove IShortcutResolver dependency from ProcessStarter**

This is a refactor, not a feature — the TDD red-green cycle doesn't apply because we're removing code, not adding it. The existing `ProcessStarterSecurityTests` UNC-path tests will verify the remaining guards still work after the change.

Replace `Services/ProcessStarter.cs` with:

```csharp
using Launchbox.Helpers;
using System;
using System.Diagnostics;

namespace Launchbox.Services;

// Shortcut (.lnk/.url) metadata validation is the caller's responsibility.
// This class only validates what is visible in the ProcessStartInfo fields.
public class ProcessStarter : IProcessStarter
{
    public Process? Start(ProcessStartInfo startInfo)
    {
        if (startInfo != null)
        {
            if (PathSecurity.IsUnsafePath(startInfo.FileName))
            {
                throw new UnauthorizedAccessException($"Execution of unsafe path '{PathSecurity.RedactPath(startInfo.FileName)}' is denied.");
            }

            if (!string.IsNullOrEmpty(startInfo.WorkingDirectory) && PathSecurity.IsUnsafePath(startInfo.WorkingDirectory))
            {
                throw new UnauthorizedAccessException("Execution with unsafe working directory is denied.");
            }

            if (!string.IsNullOrEmpty(startInfo.Arguments) &&
                (startInfo.Arguments.Contains(@"\\") || startInfo.Arguments.Contains("//") ||
                 startInfo.Arguments.Contains(@"\/") || startInfo.Arguments.Contains(@"/\")))
            {
                throw new UnauthorizedAccessException("Execution with unsafe arguments is denied.");
            }

            return Process.Start(startInfo);
        }

        return null;
    }
}
```

**What changed:**
- Removed `IShortcutResolver` field and constructor parameter
- Removed the `.lnk`-specific `UseShellExecute` block (lines 25-39 of the old file) that called `_shortcutResolver.Resolve()` — this validation is already done by `WinUILauncher`
- Kept `ProcessStartInfo`-level guards for FileName, WorkingDirectory, and Arguments — these protect against misuse from any caller, not just the launcher pipeline
- Added a comment documenting the responsibility boundary

- [ ] **Step 2: Update ProcessStarterTests constructor**

In `Launchbox.Tests/ProcessStarterTests.cs`, change the constructor from:

```csharp
public ProcessStarterTests()
{
    _processStarter = new ProcessStarter(new MockShortcutResolver());
}
```

to:

```csharp
public ProcessStarterTests()
{
    _processStarter = new ProcessStarter();
}
```

- [ ] **Step 3: Update ProcessStarterSecurityTests constructor**

In `Launchbox.Tests/ProcessStarterSecurityTests.cs`, change the constructor from:

```csharp
public ProcessStarterSecurityTests()
{
    _processStarter = new ProcessStarter(new MockShortcutResolver());
}
```

to:

```csharp
public ProcessStarterSecurityTests()
{
    _processStarter = new ProcessStarter();
}
```

- [ ] **Step 4: Add test for ProcessStartInfo-level args guard (defense-in-depth)**

This test documents that even after removing shortcut resolution, `ProcessStarter` still catches unsafe arguments passed directly via `ProcessStartInfo`. Add to `Launchbox.Tests/ProcessStarterSecurityTests.cs`:

```csharp
[Theory]
[InlineData(@"\\server\share\payload")]
[InlineData("//server/share/payload")]
public void Start_ThrowsUnauthorizedAccessException_ForUnsafeArguments(string unsafeArgs)
{
    var startInfo = new ProcessStartInfo("cmd.exe")
    {
        Arguments = unsafeArgs,
        UseShellExecute = false
    };

    var exception = Assert.Throws<UnauthorizedAccessException>(() => _processStarter.Start(startInfo));
    Assert.Contains("Execution with unsafe arguments", exception.Message);
}
```

- [ ] **Step 5: Run ProcessStarter tests**

Run: `dotnet test Launchbox.Tests/Launchbox.Tests.csproj --filter "FullyQualifiedName~ProcessStarter" -v n`
Expected: ALL PASS — UNC path blocking tests still pass via `ProcessStartInfo`-level guards. The new args test also passes.

- [ ] **Step 6: Run full test suite**

Run: `dotnet test Launchbox.Tests/Launchbox.Tests.csproj -v n`
Expected: ALL PASS.

- [ ] **Step 7: Commit**

```bash
git add Services/ProcessStarter.cs Launchbox.Tests/ProcessStarterTests.cs Launchbox.Tests/ProcessStarterSecurityTests.cs
git commit -m "refactor: remove duplicate COM interop from ProcessStarter

WinUILauncher already resolves and validates .lnk metadata (target,
arguments, working directory) before calling ProcessStarter. The
duplicate IShortcutResolver call in ProcessStarter doubled COM work
on every launch and created two validation implementations that
could drift apart.

ProcessStarter retains its ProcessStartInfo-level guards (unsafe
FileName, Arguments, WorkingDirectory) as defense-in-depth."
```

---

## Task 3: Update Composition Root

**Files:**
- Modify: `MainWindow.xaml.cs`

- [ ] **Step 1: Update ProcessStarter construction in MainWindow.xaml.cs**

In `MainWindow.xaml.cs`, find the line:

```csharp
var processStarter = new ProcessStarter(shortcutResolver);
```

Replace with:

```csharp
var processStarter = new ProcessStarter();
```

- [ ] **Step 2: Build the app**

Run: `dotnet build Launchbox.csproj -p:Platform=x64`
Expected: Build succeeds.

- [ ] **Step 3: Run full test suite**

Run: `dotnet test Launchbox.Tests/Launchbox.Tests.csproj -v n`
Expected: ALL PASS.

- [ ] **Step 4: Run dotnet format**

Run: `dotnet format Launchbox.sln`
Expected: No changes, or minor whitespace fixes only.

- [ ] **Step 5: Commit**

```bash
git add MainWindow.xaml.cs
git commit -m "refactor: update composition root for simplified ProcessStarter"
```

---

## Task 4: Update TODO.md

**Files:**
- Modify: `TODO.md`

- [ ] **Step 1: Check off the resolved TODO item**

In `TODO.md`, change line 115 from:

```markdown
- [ ] Duplicate COM interop: WinUILauncher resolves `.lnk` target/args/workdir for validation, then ProcessStarter resolves the same metadata again -- doubles COM work on every launch and creates two policy implementations that can drift (WinUILauncher.cs:53-64, ProcessStarter.cs:25-30) [Gemini+Codex]
```

to:

```markdown
- [x] Duplicate COM interop: WinUILauncher resolves `.lnk` target/args/workdir for validation, then ProcessStarter resolves the same metadata again -- doubles COM work on every launch and creates two policy implementations that can drift (WinUILauncher.cs:53-64, ProcessStarter.cs:25-30) [Gemini+Codex]
```

- [ ] **Step 2: Commit**

```bash
git add TODO.md
git commit -m "docs: mark duplicate COM interop TODO as resolved"
```
