# Full Code Audit — Design Spec

**Date:** 2026-03-22
**Goal:** Systematic review of the entire Launchbox production codebase to identify and remove unnecessary code, improve performance, verify security integrity, and fix correctness issues — particularly changes introduced by Jules automated agents.

## Context

Jules has contributed ~264 commits touching ~140 files (+11,696 / -3,693 lines) across the codebase. While many contributions are valuable (tests, security hardening), there is concern about feature creep: unnecessary abstractions, premature optimizations, over-engineered patterns, and potentially erroneous changes that accumulated without human review.

## Audit Criteria

Every production file is evaluated against four criteria, in priority order:

1. **Unnecessary code** — dead code, unused abstractions, over-engineered patterns, single-use helper extractions, premature optimizations that add complexity without measurable gain
2. **Performance** — allocations on hot paths, unnecessary LINQ in tight loops, redundant operations, unnecessary async overhead
3. **Security** — verify existing security gates (path validation, UNC detection, shortcut resolution) are intact and not weakened by refactors; no regressions in sanitization
4. **Correctness** — subtle bugs, race conditions, behavioral changes from refactors, broken assumptions

## Scope

### In Scope
- **All production `.cs` files** (~60 files) — full review for logic-bearing files; necessity check for interfaces and thin wrappers
- **All `.xaml` files** (MainWindow.xaml, SettingsWindow.xaml, App.xaml) — binding correctness only
- **Test files** (~50 files, ~6,000 lines) — light pass for broken/false-positive tests only
- **Stray files** — identify and remove files that don't belong (test_plan.sh, FormatTest/, BenchProject/, test_path_chars*.cs)

### Out of Scope
- XAML layout/styling choices
- Localization string content
- CI workflow configuration
- Documentation files
- Adding new features or abstractions

## Phases

### Phase 1 — Security-Critical Services
Logic-bearing files (full review):
- `Services/WinUILauncher.cs` — shortcut validation, process launching
- `Services/ProcessStarter.cs` — process execution gate
- `Services/WindowsShortcutResolver.cs` — COM interop, .lnk/.url resolution
- `Helpers/PathSecurity.cs` — path validation, UNC detection, redaction
- `Services/FileSystem.cs` — file I/O, INI parsing
- `Services/ShellLink.cs` — COM interop wrapper for .lnk resolution

Interfaces (necessity check — do they enable testing or have multiple implementations?):
- `Services/IProcessStarter.cs`, `Services/IShortcutResolver.cs`, `Services/IFileSystem.cs`, `Services/IAppLauncher.cs`

### Phase 2 — Core Logic
Logic-bearing files (full review):
- `ViewModels/MainViewModel.cs` — app loading, filtering, grouping
- `ViewModels/SettingsViewModel.cs` — settings binding, hotkey parsing
- `Services/SettingsService.cs` — settings coordination
- `Services/ShortcutFolderManager.cs` — folder persistence, JSON serialization
- `Services/IconService.cs` — icon extraction, caching, GDI
- `Services/ShortcutService.cs` — shortcut discovery and filtering
- `Services/LocalSettingsStore.cs` — settings persistence wrapper
- `Services/ProcessService.cs` — higher-level process operations

Interfaces (necessity check):
- `Services/IIconService.cs`, `Services/IShortcutService.cs`, `Services/ISettingsStore.cs`, `Services/ISettingsContainer.cs`, `Services/IProcessService.cs`, `Services/IStringProvider.cs`

### Phase 3 — Window & Platform Services
Logic-bearing files (full review):
- `Services/WindowService.cs` — hotkey, visibility, positioning
- `Services/WindowPositionManager.cs` — position persistence
- `Services/BackdropService.cs` — Mica/Acrylic effects
- `Services/NativeMethods.cs` — P/Invoke declarations
- `App.xaml.cs` — app lifecycle, service composition
- `MainWindow.xaml.cs` + `MainWindow.xaml` — composition, event wiring
- `SettingsWindow.xaml.cs` + `SettingsWindow.xaml` — settings UI

Platform wrappers (necessity check — thin WinUI wrappers, review for unnecessary complexity):
- `Services/BackdropWindowWrapper.cs`, `Services/WinUIDispatcher.cs`, `Services/WinUIFilePickerService.cs`, `Services/WinUIImageFactory.cs`, `Services/WinUIStartupService.cs`, `Services/ResourceStringProvider.cs`

Interfaces (necessity check):
- `Services/IBackdropService.cs`, `Services/IBackdropWindowWrapper.cs`, `Services/IDispatcher.cs`, `Services/IFilePickerService.cs`, `Services/IImageFactory.cs`, `Services/IStartupService.cs`, `Services/IWindowService.cs`

### Phase 4 — Helpers & Models
Files (full review):
- `Helpers/BulkObservableCollection.cs`
- `Helpers/ImageHeaderParser.cs`
- `Helpers/IconHelper.cs`
- `Helpers/Constants.cs`
- `Helpers/GridSize.cs`
- `Helpers/ListViewBaseExtensions.cs`
- `Helpers/Localization.cs`, `Helpers/LocalizedOption.cs`
- `Models/AppItem.cs`, `Models/AppItemGroup.cs`, `Models/ShortcutFolder.cs`, `Models/FolderViewMode.cs`
- All converter classes (BooleanToVisibilityConverter, CollapseChevronConverter, EmptyStringToCollapsedConverter)
- `Services/IconCacheEntry.cs` — cache entry model

### Phase 5 — Test Hygiene & Cleanup
- Flag and fix broken/false-positive test files (WinUI COM false positives)
- Scan for stray files that don't belong in the repo (scripts, temp projects, test scaffolds)
- Run full test suite to verify no regressions
- Run dotnet format to ensure style compliance

## Review Process Per File

1. **Claude reads the file** — identifies specific issues against all 4 criteria
2. **Dispatch Gemini and Codex in parallel** — each independently reviews the same file with the same criteria
3. **Synthesize per-finding** — each distinct issue identified by any model is evaluated individually:
   - If 2+ models independently flag the same issue — fix it
   - If only 1 model flags an issue — Claude evaluates the reasoning and decides; fix if the reasoning is sound, skip if speculative
   - Distinct valid findings from a single model are not dismissed — they are evaluated on merit
4. **Apply fixes** to the file
5. **Verify** — build + targeted tests after each phase; full suite after Phase 5

For **interfaces and thin wrappers** (necessity checks): Claude reviews alone. Only escalate to multi-model review if something looks suspicious (e.g., an interface with no mock in tests, or a wrapper that adds logic beyond delegation).

## Constraints

- **No feature additions** — this is a subtraction exercise
- **No new abstractions** — only remove or simplify existing ones
- **No refactoring for its own sake** — if code works correctly and isn't bloated, leave it
- **No public interface changes** unless something is clearly wrong
- **Preserve all security gates** — when in doubt, keep the security code
- **One commit per phase** with a clear summary of changes

## Verification

After each phase:
- `dotnet build Launchbox.csproj -p:Platform=x64` (catches XAML/binding errors)
- Targeted `dotnet test --filter` for affected test classes

After Phase 5 (final):
- `dotnet test Launchbox.Tests/Launchbox.Tests.csproj` (full suite)
- `dotnet format Launchbox.sln --verify-no-changes` (style compliance)
- `dotnet build Launchbox.csproj -c Release -p:Platform=x64` (release build)

## Success Criteria

- All files in phases 1-4 reviewed with findings documented per-phase
- No security regressions (all PathSecurity, WinUILauncher, ProcessStarter gates intact)
- No performance regressions (no new allocations on hot paths)
- Full test suite passes
- Build succeeds in both Debug and Release
- Code passes format check
