# Full Code Audit Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Review every production file in Launchbox for unnecessary code, performance issues, security regressions, and correctness bugs — then fix what's found.

**Architecture:** File-by-file review organized in 5 phases by risk priority. Each logic-bearing file gets 3-model review (Claude + Gemini + Codex). Interfaces and thin wrappers get Claude-only necessity checks. One commit per phase.

**Tech Stack:** C# / .NET 10 / WinUI 3, xUnit tests, Multi-CLI for Gemini/Codex consultation

**Spec:** `docs/superpowers/specs/2026-03-22-full-code-audit-design.md`

**Global Constraints (from spec):**
- No feature additions — this is a subtraction exercise
- No new abstractions — only remove or simplify existing ones
- No refactoring for its own sake — if code works correctly and isn't bloated, leave it
- No public interface changes unless something is clearly wrong
- Preserve all security gates — when in doubt, keep the security code
- One commit per phase with a clear summary of changes

**Note on `@filepath` references:** In the Gemini/Codex prompt templates below, `@filepath` references (e.g., `@Services/WinUILauncher.cs`) are **Multi-CLI directives** — the Ask-Gemini and Ask-Codex tools automatically resolve these to the file contents when dispatching the prompt. They are not literal file paths in the prompt text.

---

## Pre-Audit Setup

- [ ] **Step 1: Sync and baseline**

```bash
git pull
dotnet build Launchbox.csproj -p:Platform=x64
dotnet test Launchbox.Tests/Launchbox.Tests.csproj
```

Confirm: build succeeds, all tests pass. Record test count as baseline.

- [ ] **Step 2: Create working branch**

```bash
git checkout -b audit/full-code-review
```

---

## Task 1: Phase 1 — Security-Critical Services

**Files (full 3-model review):**
- `Services/WinUILauncher.cs` (125 lines) — shortcut validation, process launching
- `Services/ProcessStarter.cs` (32 lines) — process execution gate
- `Services/WindowsShortcutResolver.cs` (117 lines) — COM interop, .lnk/.url resolution
- `Helpers/PathSecurity.cs` (178 lines) — path validation, UNC detection, redaction
- `Services/FileSystem.cs` (131 lines) — file I/O, INI parsing
- `Services/ShellLink.cs` — COM interop wrapper

**Files (Claude-only necessity check):**
- `Services/IProcessStarter.cs`, `Services/IShortcutResolver.cs`, `Services/IFileSystem.cs`, `Services/IAppLauncher.cs`

### Review Process

- [ ] **Step 1: Claude reviews all 6 logic-bearing files**

Read each file. For each, list specific findings under 4 criteria:
1. Unnecessary code (dead code, unused abstractions, over-engineering)
2. Performance (allocations, redundant operations)
3. Security (gates intact? regressions?)
4. Correctness (bugs, race conditions, broken assumptions)

- [ ] **Step 2: Dispatch Gemini review**

Send all 6 files to Gemini via Ask-Gemini (model: `gemini-3.1-pro-preview`) with this prompt template:

```
Review these files from a WinUI 3 desktop app launcher for: (1) unnecessary/dead code,
(2) performance issues, (3) security regressions, (4) correctness bugs. List specific
findings with file name, line number, and reasoning. These are security-critical files
that handle shortcut validation, process launching, path security, and file I/O.

Files to review: @Services/WinUILauncher.cs @Services/ProcessStarter.cs
@Services/WindowsShortcutResolver.cs @Helpers/PathSecurity.cs @Services/FileSystem.cs
@Services/ShellLink.cs

For context on project conventions: @CLAUDE.md
```

- [ ] **Step 3: Dispatch Codex review in parallel**

Send the same files to Codex via Ask-Codex (model: `gpt-5.4`) with equivalent prompt.

- [ ] **Step 4: Synthesize findings**

For each distinct finding from any model:
- 2+ models flagged it → fix immediately
- 1 model flagged it → evaluate reasoning, fix if sound

Document the synthesis: what was found, what was fixed, what was skipped and why.

- [ ] **Step 5: Claude-only necessity check on interfaces**

Read `IProcessStarter.cs`, `IShortcutResolver.cs`, `IFileSystem.cs`, `IAppLauncher.cs`. For each, verify:
- Does it have a mock in the test project? (grep for `Mock` + interface name)
- Does it have multiple implementations?
- If no mock and single implementation → flag as potentially unnecessary (but do NOT remove if it's the test seam pattern documented in CLAUDE.md)

- [ ] **Step 6: Apply fixes**

Edit files based on synthesized findings. Do not change security gates unless all 3 models agree.

- [ ] **Step 7: Verify Phase 1**

```bash
dotnet build Launchbox.csproj -p:Platform=x64
dotnet test Launchbox.Tests/Launchbox.Tests.csproj --filter "FullyQualifiedName~WinUILauncher|FullyQualifiedName~ProcessStarter|FullyQualifiedName~PathSecurity|FullyQualifiedName~FileSystem|FullyQualifiedName~ShortcutResolver"
```

- [ ] **Step 8: Commit Phase 1**

```bash
git add -A
git commit -m "audit: phase 1 — security-critical services review and cleanup"
```

---

## Task 2: Phase 2 — Core Logic

**Files (full 3-model review):**
- `ViewModels/MainViewModel.cs` (398 lines) — app loading, filtering, grouping
- `ViewModels/SettingsViewModel.cs` (327 lines) — settings binding, hotkey parsing
- `Services/SettingsService.cs` (259 lines) — settings coordination
- `Services/ShortcutFolderManager.cs` (162 lines) — folder persistence, JSON serialization
- `Services/IconService.cs` (393 lines) — icon extraction, caching, GDI
- `Services/ShortcutService.cs` — shortcut discovery and filtering
- `Services/LocalSettingsStore.cs` — settings persistence wrapper
- `Services/ProcessService.cs` — higher-level process operations

**Files (Claude-only necessity check):**
- `Services/IIconService.cs`, `Services/IShortcutService.cs`, `Services/ISettingsStore.cs`, `Services/ISettingsContainer.cs`, `Services/IProcessService.cs`, `Services/IStringProvider.cs`

### Review Process

- [ ] **Step 1: Claude reviews all 8 logic-bearing files**

Same 4-criteria analysis as Phase 1. Pay special attention to:
- MainViewModel: icon loading pipeline, grouped/merged mode logic, any unnecessary complexity in the LINQ chains
- IconService: cache management, GDI resource handling, any Jules-added helper extractions
- SettingsViewModel: the recently-refactored ParseVirtualKey method (from merged PR #229)

- [ ] **Step 2: Dispatch Gemini review**

```
Review these core logic files from a WinUI 3 desktop app launcher for: (1) unnecessary/dead
code, (2) performance issues on hot paths, (3) security regressions, (4) correctness bugs.
List specific findings with file name, line number, and reasoning.

Focus areas:
- MainViewModel: icon loading pipeline, grouped/merged mode logic, unnecessary complexity in LINQ chains
- IconService: cache management, GDI resource handling, any over-extracted helper methods
- SettingsViewModel: the ParseVirtualKey method (recently refactored in PR #229)

Files: @ViewModels/MainViewModel.cs @ViewModels/SettingsViewModel.cs @Services/SettingsService.cs
@Services/ShortcutFolderManager.cs @Services/IconService.cs @Services/ShortcutService.cs
@Services/LocalSettingsStore.cs @Services/ProcessService.cs

For context: @CLAUDE.md
```

- [ ] **Step 3: Dispatch Codex review in parallel**

Same files and focus areas, equivalent prompt, model `gpt-5.4`.

- [ ] **Step 4: Synthesize findings**

Same per-finding evaluation as Phase 1.

- [ ] **Step 5: Claude-only necessity check on interfaces**

Check `IIconService.cs`, `IShortcutService.cs`, `ISettingsStore.cs`, `ISettingsContainer.cs`, `IProcessService.cs`, `IStringProvider.cs` for mock usage and multiple implementations.

- [ ] **Step 6: Apply fixes**

- [ ] **Step 7: Verify Phase 2**

```bash
dotnet build Launchbox.csproj -p:Platform=x64
dotnet test Launchbox.Tests/Launchbox.Tests.csproj --filter "FullyQualifiedName~MainViewModel|FullyQualifiedName~SettingsViewModel|FullyQualifiedName~SettingsService|FullyQualifiedName~ShortcutFolder|FullyQualifiedName~IconService|FullyQualifiedName~ShortcutService"
```

- [ ] **Step 8: Commit Phase 2**

```bash
git add -A
git commit -m "audit: phase 2 — core logic review and cleanup"
```

---

## Task 3: Phase 3 — Window & Platform Services

**Files (full 3-model review):**
- `Services/WindowService.cs` (512 lines) — hotkey, visibility, positioning
- `Services/WindowPositionManager.cs` (109 lines) — position persistence
- `Services/BackdropService.cs` (88 lines) — Mica/Acrylic effects
- `Services/NativeMethods.cs` (125 lines) — P/Invoke declarations
- `App.xaml.cs` — app lifecycle, service composition
- `MainWindow.xaml.cs` (233 lines) + `MainWindow.xaml` — composition, event wiring
- `SettingsWindow.xaml.cs` (77 lines) + `SettingsWindow.xaml` — settings UI

**Files (Claude-only necessity check — thin wrappers):**
- `Services/BackdropWindowWrapper.cs`, `Services/WinUIDispatcher.cs`, `Services/WinUIFilePickerService.cs`, `Services/WinUIImageFactory.cs`, `Services/WinUIStartupService.cs`, `Services/ResourceStringProvider.cs`

**Interfaces (Claude-only necessity check):**
- `Services/IBackdropService.cs`, `Services/IBackdropWindowWrapper.cs`, `Services/IDispatcher.cs`, `Services/IFilePickerService.cs`, `Services/IImageFactory.cs`, `Services/IStartupService.cs`, `Services/IWindowService.cs`

**Test coverage note:** `WindowService.cs`, `WindowPositionManager.cs`, and `BackdropService.cs` have dedicated test classes. `NativeMethods.cs`, `App.xaml.cs`, `MainWindow.xaml.cs`, and `SettingsWindow.xaml.cs` do not — they are UI/platform code that cannot be meaningfully unit tested. Changes to these files must be verified via build only.

### Review Process

- [ ] **Step 1: Claude reviews all 9 logic-bearing files**

Pay special attention to:
- WindowService.cs (512 lines — largest file, most likely to have bloat)
- MainWindow.xaml: verify all bindings are correct after the Bolt x:Bind revert
- NativeMethods.cs: all P/Invoke must have `SetLastError = true` per CLAUDE.md

- [ ] **Step 2: Dispatch Gemini review**

```
Review these window and platform service files from a WinUI 3 desktop app launcher for:
(1) unnecessary/dead code, (2) performance issues, (3) security regressions, (4) correctness
bugs. WindowService.cs is the largest file (512 lines) — pay special attention to potential
bloat. All P/Invoke in NativeMethods.cs must have SetLastError=true.

Files: @Services/WindowService.cs @Services/WindowPositionManager.cs @Services/BackdropService.cs
@Services/NativeMethods.cs @App.xaml.cs @MainWindow.xaml.cs @MainWindow.xaml
@SettingsWindow.xaml.cs @SettingsWindow.xaml

For context: @CLAUDE.md
```

- [ ] **Step 3: Dispatch Codex review in parallel**

Same files, equivalent prompt.

- [ ] **Step 4: Synthesize findings**

- [ ] **Step 5: Claude-only necessity check on wrappers and interfaces**

For each thin wrapper: does it add any logic beyond delegating to the WinUI API? If it's pure delegation, it exists for testability — leave it. If it adds logic, evaluate whether that logic belongs there.

- [ ] **Step 6: Apply fixes**

- [ ] **Step 7: Verify Phase 3**

```bash
dotnet build Launchbox.csproj -p:Platform=x64
dotnet test Launchbox.Tests/Launchbox.Tests.csproj --filter "FullyQualifiedName~WindowService|FullyQualifiedName~WindowPosition|FullyQualifiedName~Backdrop|FullyQualifiedName~MainViewModel"
```

- [ ] **Step 8: Commit Phase 3**

```bash
git add -A
git commit -m "audit: phase 3 — window and platform services review and cleanup"
```

---

## Task 4: Phase 4 — Helpers & Models

**Files (full 3-model review):**
- `Helpers/BulkObservableCollection.cs` (72 lines)
- `Helpers/ImageHeaderParser.cs` (111 lines)
- `Helpers/IconHelper.cs`
- `Helpers/Constants.cs`
- `Helpers/GridSize.cs`
- `Helpers/ListViewBaseExtensions.cs`
- `Helpers/Localization.cs`, `Helpers/LocalizedOption.cs`
- `Models/AppItem.cs`, `Models/AppItemGroup.cs`, `Models/ShortcutFolder.cs`, `Models/FolderViewMode.cs`
- `Helpers/BooleanToVisibilityConverter.cs`, `Helpers/CollapseChevronConverter.cs`, `Helpers/EmptyStringToCollapsedConverter.cs`
- `Services/IconCacheEntry.cs`

### Review Process

- [ ] **Step 1: Claude reviews all files**

These are smaller files. Focus on:
- Unnecessary abstractions (helpers that are only used once)
- Models with fields/methods that aren't used
- Constants that are dead
- Converters with unnecessary complexity

- [ ] **Step 2: Dispatch Gemini review**

```
Review these helper and model files from a WinUI 3 desktop app launcher for:
(1) unnecessary/dead code and unused abstractions, (2) performance issues,
(3) security regressions (especially PathSecurity helpers), (4) correctness bugs.
These are small utility files — focus on whether each file earns its existence
and whether any code within is unused.

Files: @Helpers/BulkObservableCollection.cs @Helpers/ImageHeaderParser.cs @Helpers/IconHelper.cs
@Helpers/Constants.cs @Helpers/GridSize.cs @Helpers/ListViewBaseExtensions.cs
@Helpers/Localization.cs @Helpers/LocalizedOption.cs @Models/AppItem.cs @Models/AppItemGroup.cs
@Models/ShortcutFolder.cs @Models/FolderViewMode.cs @Helpers/BooleanToVisibilityConverter.cs
@Helpers/CollapseChevronConverter.cs @Helpers/EmptyStringToCollapsedConverter.cs
@Services/IconCacheEntry.cs

For context: @CLAUDE.md
```

- [ ] **Step 3: Dispatch Codex review in parallel**

Same files, equivalent prompt.

- [ ] **Step 4: Synthesize findings**

- [ ] **Step 5: Apply fixes**

- [ ] **Step 6: Verify Phase 4**

```bash
dotnet build Launchbox.csproj -p:Platform=x64
dotnet test Launchbox.Tests/Launchbox.Tests.csproj --filter "FullyQualifiedName~BulkObservable|FullyQualifiedName~ImageHeader|FullyQualifiedName~IconHelper|FullyQualifiedName~AppItem|FullyQualifiedName~Converter"
```

- [ ] **Step 7: Commit Phase 4**

```bash
git add -A
git commit -m "audit: phase 4 — helpers and models review and cleanup"
```

---

## Task 5: Phase 5 — Test Hygiene & Final Verification

- [ ] **Step 1: Scan for broken/false-positive tests**

Look for tests that:
- Call `new BitmapImage()` or other WinUI types directly (COM false positives)
- Have `if (!OperatingSystem.IsWindows()) return;` guards that skip on CI
- Test trivially obvious behavior (constants equal themselves)

```bash
grep -rn "BitmapImage\|new BitmapImage" Launchbox.Tests/ --include="*.cs"
grep -rn "OperatingSystem.IsWindows" Launchbox.Tests/ --include="*.cs"
```

- [ ] **Step 2: Scan for stray files**

```bash
git ls-files | grep -iE "test_plan|FormatTest|BenchProject|test_path_chars|\.sh$" | grep -v ".github"
```

Remove any stray files found.

- [ ] **Step 3: Run full test suite**

```bash
dotnet test Launchbox.Tests/Launchbox.Tests.csproj
```

Compare test count against baseline from pre-audit. All tests must pass. If any tests broke due to audit changes, fix them.

- [ ] **Step 4: Run format check**

```bash
dotnet format Launchbox.sln --verify-no-changes
```

If failures, run `dotnet format Launchbox.sln` and include in commit.

- [ ] **Step 5: Release build verification**

```bash
dotnet build Launchbox.csproj -c Release -p:Platform=x64
```

- [ ] **Step 6: Commit Phase 5**

```bash
git add -A
git commit -m "audit: phase 5 — test hygiene and final verification"
```

---

## Post-Audit

- [ ] **Step 1: Write audit summary**

Document per-phase: what was found, what was fixed, what was intentionally left unchanged and why.

- [ ] **Step 2: Merge to main**

```bash
git checkout main
git merge audit/full-code-review
git push
```

- [ ] **Step 3: Update CLAUDE.md if any architectural patterns changed**

If the audit removed interfaces, changed service wiring, or altered conventions, update the relevant CLAUDE.md sections.
