# Launchbox Codebase Audit — Design Spec
**Date:** 2026-04-11  
**Status:** Approved — ready for issue creation  
**Total issues:** 24 (10 bugs · 6 test gaps · 8 features)

---

## Overview

This spec captures the output of a comprehensive codebase audit of Launchbox. Three independent sources contributed findings:

- **Gemini** (bug & anti-pattern hunt) — scanned core files for WinUI-specific pitfalls
- **Codex** (test gap analysis) — identified missing high-risk test coverage
- **Gemini + Codex** (feature proposals) — independently proposed missing features; overlapping proposals noted
- **Lead Architect** (synthesis) — added feature gaps and consolidated duplicates

Each section maps directly to GitHub issues to be created. All issues use the label taxonomy defined below.

---

## Label Taxonomy

| Label | Color | Purpose |
|-------|-------|---------|
| `bug` | `#d73a4a` | Confirmed defect in production code |
| `test-gap` | `#e4e669` | Missing test for a high-risk code path |
| `enhancement` | `#a2eeef` | New capability or quality-of-life feature |
| `severity:high` | `#e99695` | Data loss, crash, or silent wrong state |
| `severity:medium` | `#f9d0c4` | User-visible issue, not data-losing |

---

## Section 1: Bugs (10 issues)

### BUG-01 · `bug` `severity:high`
**Title:** `SettingsViewModel`: disposing `SemaphoreSlim` while startup toggle is in-flight can crash the app

**Body:**
> **Repro path:** Close the Settings window while the "Run at Startup" toggle is mid-write (e.g., on a slow registry path).
>
> **What happens:** `Dispose()` calls `_startupToggleLock.Dispose()` while a `WaitAsync()` may still be pending. `SemaphoreSlim.Dispose()` is not thread-safe with concurrent waiters and can throw an uncatchable `ObjectDisposedException` on the background thread.
>
> **Fix direction:** Remove `_startupToggleLock.Dispose()`. `SemaphoreSlim` used asynchronously holds no unmanaged resources and is safe to leave to the GC.

---

### BUG-02 · `bug` `severity:high`
**Title:** `MainViewModel`: drag-and-drop reorder in Grouped view silently fails to persist

**Body:**
> **Repro path:** Switch to Grouped view, drag an item to a new position, restart the app.
>
> **What happens:** `PersistItemOrder()` reads the new order from `FilteredApps`. In Grouped view, WinUI mutates the `AppItemGroup`'s internal collection during drag — `FilteredApps` is untouched. The stale order is written to the settings store, discarding the user's reorder.
>
> **Fix direction:** When `IsGroupedMode` is true, flatten the order from `GroupedApps` instead of reading `FilteredApps`.

---

### BUG-03 · `bug` `severity:high`
**Title:** `SettingsViewModel`: startup toggle stays "On" when OS denies registration and `IsRunAtStartup` was already `false`

**Body:**
> **Repro path:** App is not set to run at startup. User enables the toggle. OS denies the registry write. Toggle gets stuck showing "On".
>
> **What happens:** `SetRunAtStartupSafeAsync` catches the exception and tries to sync `_pendingStartupValue` via `PropertyChanged`. But if `IsRunAtStartup` was already `false`, `SetProperty` detects no change and skips the event — the UI toggle never reverts.
>
> **Fix direction:** Unconditionally sync `_pendingStartupValue = _settingsService.IsRunAtStartup; OnPropertyChanged(nameof(RunAtStartup))` in the `catch` block rather than relying on `PropertyChanged` firing.

---

### BUG-04 · `bug` `severity:medium`
**Title:** `WindowService`: failed hotkey registration reverts in Win32 but persists to disk

**Body:**
> **Repro path:** Change the global hotkey to a key already registered by another app. The Win32 fallback restores the previous working hotkey, but the settings store retains the invalid new value. On next launch, Launchbox tries (and fails) to register the bad hotkey again.
>
> **Fix direction:** When `RegisterHotKey` fails and the fallback succeeds, write `_currentMod`/`_currentKey` back to `_settingsService.HotkeyModifiers`/`HotkeyKey` so the store stays in sync with actual Win32 state.

---

### BUG-05 · `bug` `severity:medium`
**Title:** `WindowService`: `DispatcherQueueTimer.Tick` anonymous lambda captures `this`, never unsubscribed — memory leak

**Body:**
> **Location:** `WindowService.Initialize()` — `_savePositionTimer.Tick += (_, _) => SaveWindowPosition()`
>
> **What happens:** The anonymous lambda is never removed in `Dispose()`. WinUI's `DispatcherQueueTimer` roots its handlers to the UI thread dispatcher, keeping the `WindowService` instance (and the window it holds) alive.
>
> **Fix direction:** Extract the handler to a named method and unsubscribe it in `Dispose()`.

---

### BUG-06 · `bug` `severity:medium`
**Title:** `MainWindow`: `_freshShow` race causes `SearchBox` to miss focus on Alt+S

**Body:**
> **Repro path:** Press Alt+S to show the window, immediately start typing. On some machines the search box doesn't receive the input.
>
> **What happens:** `_freshShow = true` is set inside `WindowService_VisibilityChanged`, but `Activated` — which consumes `_freshShow` — can fire synchronously before the `VisibilityChanged` event loop. `_freshShow` reads `false` and `SearchBox.Focus()` is skipped.
>
> **Fix direction:** Set `_freshShow = true` synchronously inside `ToggleVisibility()` immediately before `_appWindow.Show()`, removing dependence on event ordering.

---

### BUG-07 · `bug` `severity:medium`
**Title:** `MainViewModel.OpenShortcutsFolder`: creates directories without running path security checks

**Body:**
> **What happens:** `OpenShortcutsFolder` extracts `firstFolder.ExpandedPath` and calls `_fileSystem.CreateDirectory(shortcutFolder)` without passing through `PathSecurity.IsUnsafePath`. A manipulated environment variable could resolve to a UNC path, which Launchbox then actively creates before the launcher blocks opening it.
>
> **Fix direction:** Use `_settingsService.ShortcutsPath` (which already applies safety checks) or add an explicit `PathSecurity.IsUnsafePath` guard before the `CreateDirectory` call.

---

### BUG-08 · `bug` `severity:medium`
**Title:** `App_UnhandledException`: swallows most exceptions, leaving ViewModel/UI in corrupt state

**Body:**
> **What happens:** `IsRecoverable()` returns `true` for `NullReferenceException`, `InvalidOperationException`, `ArgumentException`, and most application-layer errors. These are silently consumed with `e.Handled = true`, leaving the ViewModel and UI event loop in undefined state. Future interactions then produce phantom, untraceable bugs.
>
> **Fix direction:** Narrow `IsRecoverable` to only exceptions that are genuinely safe to suppress (e.g., known transient COM errors). Let the existing critical-error `MessageBox` + `Environment.Exit` path handle everything else.

---

### BUG-09 · `bug` `severity:medium`
**Title:** `WindowService.ClampToWorkArea`: can produce negative window dimensions on small/headless displays

**Body:**
> **What happens:** `int maxHeight = workArea.Height - 40` produces zero or negative values on very small displays, RemoteApp sessions, or certain remote desktop configurations. Passing a negative height to `_appWindow.Resize()` throws an `ArgumentException`, permanently breaking the Alt+S toggle for that session.
>
> **Fix direction:** Clamp computed dimensions to at least `Constants.MIN_WINDOW_HEIGHT` / `Constants.MIN_WINDOW_WIDTH` before calling `Resize`.

---

### BUG-10 · `bug` `severity:medium`
**Title:** `ShortcutFolderManager`: drive root paths (e.g. `C:\`) produce blank group labels

**Body:**
> **What happens:** `Path.GetFileName(@"C:\")` returns `""`. The label fallback `label ??= Path.GetFileName(path) ?? path` treats the empty string as valid, saving `""` as the label. In the UI this renders as a blank group header.
>
> **Fix direction:** Use `string.IsNullOrEmpty` in the fallback check, or trim trailing directory separators before calling `GetFileName`, falling back to the full path if the result is still empty.

---

## Section 2: Test Gaps (6 issues)

### TEST-01 · `test-gap`
**Title:** Missing test: `WinUILauncher.Launch()` swallows resolver exceptions without crashing or starting a process

**Body:**
> **Gap:** No test covers the path where `IShortcutResolver.Resolve()` throws (e.g. COM failure on a malformed `.lnk`). The exception occurs before the existing `try/catch`, so one bad shortcut can crash the launcher rather than fail closed.
>
> **Test class:** `WinUILauncherExceptionTests`
>
> **Scenario:** Arrange a `ThrowingShortcutResolver` that throws `InvalidOperationException`. Assert no exception propagates to the caller and `IProcessStarter` was not invoked.

---

### TEST-02 · `test-gap`
**Title:** Missing test: file watcher callback triggers a real `LoadAppsAsync` reload

**Body:**
> **Gap:** There is no end-to-end test proving that a watcher registered by `LoadAppsAsync` actually causes a fresh reload when the monitored directory changes. If the watcher callback path breaks, the launcher silently shows stale shortcuts until restart.
>
> **Test class:** `MainViewModelTests`
>
> **Scenario:** Load once with one file. Add a second file to `MockFileSystem`, simulate a directory change event, await the reload, assert both items are present.

---

### TEST-03 · `test-gap`
**Title:** Missing test: superseded `LoadAppsAsync` call cannot overwrite newer UI state

**Body:**
> **Gap:** The debounce/cancellation path has no regression test for the race where an older load completes after a newer one. If this regresses, users see old shortcuts after a newer reload already succeeded — with no indication anything is wrong.
>
> **Test class:** `MainViewModelTests` (or a dedicated `MainViewModelConcurrencyTests`)
>
> **Scenario:** Use a blocking `IShortcutService` stub that gates the first call. Fire two loads, let the second complete first, then release the first. Assert `Apps` reflects only the second (newer) result.

---

### TEST-04 · `test-gap`
**Title:** Missing test: `ShortcutFolderManager.AddFolder` rejects env-var path that expands to UNC

**Body:**
> **Gap:** The env-var → UNC bypass is an explicit defense-in-depth check in production code, but has no test. A future refactor of the path validation could silently reopen this attack surface.
>
> **Test class:** `ShortcutFolderManagerTests`
>
> **Scenario:** Set a temp environment variable to `\\attacker\share`. Call `AddFolder` with `%VAR%\Apps`. Assert the call returns `false` and no folder is saved to the store.

---

### TEST-05 · `test-gap`
**Title:** Missing test: `SettingsViewModel.RunAtStartup` reverts pending state when startup service is unsupported

**Body:**
> **Gap:** When `IStartupService.IsSupported == false` the toggle currently leaves `_pendingStartupValue` at the user-requested value even though nothing changed on the service. This silent correctness bug in settings UX has no test.
>
> **Test class:** `SettingsViewModelTests`
>
> **Scenario:** Set `MockStartupService.IsSupported = false`. Set `vm.RunAtStartup = true`. After a short await, assert `service.IsRunAtStartup == false` and `vm.RunAtStartup == false`.

---

### TEST-06 · `test-gap`
**Title:** Missing test: `WindowService` needs an `IAppWindowAdapter` seam for unit testing

**Body:**
> **Gap:** `WindowService` has effectively zero direct unit coverage despite being the highest-risk integration point (hotkey registration, WndProc hook, position persistence, settings sync). The blocker is that `Microsoft.UI.Xaml.Window` and `AppWindow` have no testable seam today.
>
> **What's needed first:** Extract a narrow `IAppWindowAdapter` interface wrapping the three most-tested concerns — `Move/Resize/Show/Hide`, `IsVisible`, and `Position/Size`. This makes the core logic unit-testable without WinUI.
>
> **Test class:** `WindowServiceTests` (new, after the seam is introduced)
>
> **Priority scenarios once unblocked:** hotkey fallback-and-restore logic, `ClampToWorkArea` math, `_freshShow` sequencing, deactivation-triggered auto-hide.

---

## Section 3: Feature Enhancements (8 issues)

### FEAT-01 · `enhancement`
**Title:** Enhancement: rank search results by prefix match before substring match

**Body:**
> **Current behavior:** `FilterText` uses a single `Contains()` check with no ordering. Typing "ch" ranks "Microsoft Teams (chat)" equally with "Chrome".
>
> **Proposed behavior:** Items whose `Name` starts with the filter string appear before items that merely contain it. Within each tier, preserve the user's custom sort order.
>
> **Scope:**
> - `MainViewModel.RebuildFilteredApps()` — two-pass sort: prefix matches first, then remaining `Contains` matches
> - `ApplyGroupedFilter()` — same ranking within each `AppItemGroup`
> - No UI changes required
>
> **Success criteria:** Typing "ch" puts "Chrome" above "Microsoft Teams (chat)". Custom order is preserved within each tier.

---

### FEAT-02 · `enhancement`
**Title:** Enhancement: show a tray notification when an app fails to launch

**Body:**
> **Current behavior:** `MainViewModel.LaunchApp()` catches launch exceptions, logs to `Trace`, and silently hides the window. The user sees nothing.
>
> **Proposed behavior:** On a caught exception in `LaunchApp`, display a tray notification via a new `IWindowService.ShowNotification(title, message)` method with a localized message naming the failed app.
>
> **Scope:**
> - `MainViewModel.LaunchApp()` — call `_windowService.ShowNotification(...)` on exception
> - `IWindowService` + `MockWindowService` — add `ShowNotification(string title, string message)`
> - `Services/WindowService.cs` — implement via `TrayIcon.ShowNotification`
> - Localization: `Error_LaunchFailedTitle`, `Error_LaunchFailedMessage` in all 13 `.resw` files
>
> **Success criteria:** Launching a broken shortcut shows a tray balloon with the app name. Normal hide-on-launch is unchanged.

---

### FEAT-03 · `enhancement`
**Title:** Enhancement: add UIA automation names to `GridViewItem` app tiles for screen reader support

**Body:**
> **Current behavior:** Each app tile exposes no explicit `AutomationProperties.Name`. Screen readers announce "GridViewItem" or nothing useful.
>
> **Proposed behavior:** Each tile announces the app name to UIA-compatible screen readers (Narrator, NVDA).
>
> **Scope:**
> - `MainWindow.xaml` — add `AutomationProperties.Name="{x:Bind Name}"` to the `DataTemplate` root content element for `AppGrid` and `GroupedAppGrid`
> - Add `AutomationProperties.Name` to group header elements in Grouped view
>
> **Success criteria:** Narrator reads "Chrome, button" (or equivalent) when focused on a Chrome tile. No visual regression.

---

### FEAT-04 · `enhancement`
**Title:** Enhancement: keyboard-first grid navigation — arrow keys, auto-select, Tab focus flow

**Body:**
> **Current behavior:** The grid has `SelectionMode="None"`. Users must mouse-click a tile after searching. There is no way to move through results with arrow keys.
>
> **Proposed behavior:** Typing auto-highlights the top match. Arrow keys move focus through the grid. Tab moves focus between the search box and the grid. Pressing Enter from the search box launches the top visible result.
>
> **Scope:**
> - `MainWindow.xaml` — `SelectionMode="Single"` on `AppGrid`/`GroupedAppGrid`; arrow-key routing from `SearchBox`
> - `MainWindow.xaml.cs` — update `Grid_KeyDown` and `Grid_CharacterReceived` for Tab and search-box Enter
> - `ViewModels/MainViewModel.cs` — add `SelectedItem` property; auto-set to first `FilteredApps` item when filter changes
>
> **Success criteria:** Alt+S → type "ch" → Enter launches Chrome without touching the mouse.

---

### FEAT-05 · `enhancement`
**Title:** Enhancement: open Launchbox on the active monitor (cursor position) when hotkey fires

**Body:**
> **Current behavior:** `WindowService.ToggleVisibility()` restores the last saved position or centers on the primary display. On multi-monitor setups the window opens on whichever monitor it was last used on.
>
> **Proposed behavior:** When the hotkey fires to show the window, detect the monitor containing the mouse cursor via `GetCursorPos` + `DisplayArea.GetFromPoint` and center the window there.
>
> **Scope:**
> - `Services/WindowService.cs` — `ToggleVisibility()`: use `NativeMethods.GetCursorPos` to identify the active monitor and call `CenterOnCurrentDisplay` targeting it
> - `Services/SettingsService.cs` / `SettingsViewModel` — optional "Follow cursor monitor" toggle if opt-in behavior is preferred
>
> **Success criteria:** On a dual-monitor setup, pressing Alt+S while working on the right monitor opens Launchbox centered on that monitor.

---

### FEAT-06 · `enhancement`
**Title:** Enhancement: launch app as Administrator via Ctrl+click or right-click menu

**Body:**
> **Current behavior:** All apps launch with the user's current privileges. There is no way to elevate from within Launchbox.
>
> **Proposed behavior:** Holding Ctrl while clicking (or pressing Enter) launches the selected app elevated (`ProcessStartInfo.Verb = "runas"`, `UseShellExecute = true`). A right-click "Run as Administrator" context menu item provides the same action.
>
> **Scope:**
> - `Services/IAppLauncher.cs` — add `LaunchElevated(string path)`
> - `Services/WinUILauncher.cs` — implement elevated launch path
> - `ViewModels/MainViewModel.cs` — add `LaunchAppElevatedCommand`; detect Ctrl modifier in `LaunchApp`
> - `MainWindow.xaml` — Ctrl+click detection; right-click `ContextFlyout` with "Run as Administrator" item in the app tile `DataTemplate` (use `{Binding}`, not `{x:Bind}`)
> - Localization: `ContextMenu_RunAsAdmin` in all 13 `.resw` files
>
> **Success criteria:** Ctrl+clicking a shortcut opens a UAC prompt and launches elevated. Normal click is unchanged.

---

### FEAT-07 · `enhancement`
**Title:** Enhancement: right-click context menu on app tiles — "Open file location" and "Properties"

**Body:**
> **Current behavior:** App tiles have no right-click menu. Users must manually navigate to their shortcuts folder in Explorer to manage shortcut files.
>
> **Proposed behavior:** Right-clicking an app tile shows a flyout with:
> - **Open file location** — opens Explorer with the `.lnk`/`.url` file selected
> - **Properties** — opens the shell Properties dialog for the shortcut file
>
> **Scope:**
> - `ViewModels/MainViewModel.cs` — add `OpenFileLocationCommand` and `OpenPropertiesCommand` taking `AppItem`
> - `Services/IAppLauncher.cs` / `WinUILauncher.cs` — add `OpenFileLocation(string path)` and `ShowProperties(string path)`
> - `MainWindow.xaml` — add `ContextFlyout` to the `AppItemTemplate` DataTemplate (use `{Binding}`)
> - Localization: `ContextMenu_OpenFileLocation`, `ContextMenu_Properties` in all 13 `.resw` files
>
> **Success criteria:** Right-clicking "Chrome" shows a flyout. "Open file location" opens Explorer with the shortcut file selected.

---

### FEAT-08 · `enhancement`
**Title:** Enhancement: first-run guidance and empty-state onboarding

**Body:**
> **Current behavior:** A new user with no shortcuts folder configured sees an empty grid with no explanation or guidance. Key behaviors (search, drag-to-reorder, multiple folders) are invisible.
>
> **Proposed behavior:**
> - **Empty-state panel:** When `IsEmpty` is true and no filter is active, show a card: "Add a shortcuts folder in Settings, then drop `.lnk` files there."
> - **Dismissible hint footer:** A single line below the grid: "Type to search · Enter to launch · Drag to reorder" — dismissed state persisted via `SettingsService`
> - **First-run detection:** On first launch with no folders configured, open Settings automatically
>
> **Scope:**
> - `MainWindow.xaml` — empty-state panel (bound to `IsEmpty && IsFilterEmpty`), dismissible hint footer
> - `ViewModels/MainViewModel.cs` — expose `ShowEmptyState` computed property
> - `Services/SettingsService.cs` — add `HintsDismissed` and `IsFirstRun` bool settings
> - `ViewModels/SettingsViewModel.cs` — "Show tips again" reset option
> - Localization: `EmptyState_Title`, `EmptyState_Body`, `Hint_Footer` in all 13 `.resw` files
>
> **Success criteria:** Fresh install shows the empty-state card. Hint footer appears once and is gone after dismissal. First run with no folders opens Settings automatically.

---

## Full Issue Inventory

| ID | Labels | Title |
|----|--------|-------|
| BUG-01 | `bug` `severity:high` | `SettingsViewModel`: SemaphoreSlim disposed while in-flight |
| BUG-02 | `bug` `severity:high` | `MainViewModel`: Grouped view drag-and-drop not persisted |
| BUG-03 | `bug` `severity:high` | `SettingsViewModel`: startup toggle stuck "On" after OS denial |
| BUG-04 | `bug` `severity:medium` | `WindowService`: failed hotkey persists to disk |
| BUG-05 | `bug` `severity:medium` | `WindowService`: Timer Tick lambda memory leak |
| BUG-06 | `bug` `severity:medium` | `MainWindow`: `_freshShow` race, SearchBox misses focus |
| BUG-07 | `bug` `severity:medium` | `MainViewModel`: `CreateDirectory` without security check |
| BUG-08 | `bug` `severity:medium` | `App_UnhandledException` swallows too broadly |
| BUG-09 | `bug` `severity:medium` | `WindowService`: negative window dimensions on small displays |
| BUG-10 | `bug` `severity:medium` | `ShortcutFolderManager`: drive root produces blank group label |
| TEST-01 | `test-gap` | `WinUILauncher`: resolver exception not tested |
| TEST-02 | `test-gap` | File watcher reload not tested end-to-end |
| TEST-03 | `test-gap` | Superseded load stale-state regression |
| TEST-04 | `test-gap` | Env-var → UNC bypass not tested |
| TEST-05 | `test-gap` | `RunAtStartup` revert when startup service unsupported |
| TEST-06 | `test-gap` | `WindowService` needs `IAppWindowAdapter` seam |
| FEAT-01 | `enhancement` | Search result prefix ranking |
| FEAT-02 | `enhancement` | Tray notification on launch failure |
| FEAT-03 | `enhancement` | UIA automation names on app tiles |
| FEAT-04 | `enhancement` | Keyboard-first grid navigation |
| FEAT-05 | `enhancement` | Active monitor placement on hotkey |
| FEAT-06 | `enhancement` | Run as Administrator |
| FEAT-07 | `enhancement` | Right-click context menu (file location, properties) |
| FEAT-08 | `enhancement` | First-run guidance and empty-state onboarding |
