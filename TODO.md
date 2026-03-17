# Launchbox TODO

## Critical

- [x] SettingsViewModel memory leak: subscribes to SettingsService.PropertyChanged but never unsubscribes / missing IDisposable (SettingsViewModel.cs:30)
- [x] async void lambda via SimpleCommand(Action) in MainViewModel creates unobservable crash risk (MainViewModel.cs:69) -- create AsyncSimpleCommand accepting Func<Task>
- [x] `.url` shortcut security bypass: WinUILauncher falls through to `Process.Start(UseShellExecute=true)` when `ResolveTarget` returns null for unsafe schemes -- crafted `.url` files using `file:`, `ms-settings:`, or custom URI handlers bypass validation entirely (WinUILauncher.cs:45-58) [Gemini+Codex consensus]

## High

- [x] App.xaml.cs swallows all UI-thread exceptions: `e.Handled = true` suppresses every crash, allowing the process to continue in a corrupted state with missing windows or broken event wiring -- should only handle known-recoverable exceptions or fail fast (App.xaml.cs:20-23) [Codex]
- [x] BackdropService retry in catch block: `SetDesktopAcrylicBackdrop()` is retried inside its own catch -- if the backdrop API threw, it re-throws as an unobserved task fault on unsupported systems (BackdropService.cs:69-76) [Codex]
- [x] Startup toggle race condition: rapid user toggles fire-and-forget `SetRunAtStartupSafeAsync` concurrently -- whichever call finishes last wins, not necessarily the user's final choice (SettingsViewModel.cs:101-108) [Codex]
- [x] DPI-unaware window dragging: delta in DIPs applied to physical-pixel position causes lag on >100% DPI (MainWindow.xaml.cs:128-141)
- [x] No reentrancy protection on LoadAppsAsync -- concurrent calls race on Apps collection (MainViewModel.cs:80,84) -- add CancellationTokenSource or SemaphoreSlim
- [x] Missing null guards on all 8 MainViewModel constructor parameters, inconsistent with SettingsViewModel pattern (MainViewModel.cs:48-65)
- [x] async void BrowseFolderAsync has no try/catch -- COM failure would crash app (SettingsViewModel.cs:39-48)
- [x] Fire-and-forget tasks with no error handling: InitializeAsync and SetRunAtStartupAsync failures silently lost (SettingsViewModel.cs:33,76)
- [x] Missing SetLastError=true on ALL 13 P/Invoke declarations -- Win32 error codes unreliable (NativeMethods.cs)
- [x] IWindowService too narrow: Initialize() and OnActivated() not on interface; MainWindow uses concrete WindowService type, defeating abstraction (IWindowService.cs, WindowService.cs:29,274)
- [x] RegisterHotKey thread affinity not enforced -- PropertyChanged from background thread would silently fail (WindowService.cs:57-69)
- [x] No .editorconfig file -- dotnet format in CI enforces invisible rules; AGENTS.md formatting rules unenforceable
- [x] Test packages severely outdated: xunit 2.6.3->2.9.x, xunit.runner.visualstudio 2.5.5->3.x, Microsoft.NET.Test.Sdk 17.8->17.13 (Tests.csproj:14-17)
- [x] No tests for ImageHeaderParser (binary parsing with zero coverage) and BooleanToVisibilityConverter (not even linked in test project)
- [x] Certificate thumbprint hardcoded in source (Launchbox.csproj:59) -- should be parameterized

## Medium

### Reliability & Error Handling
- [x] Hotkey change is not atomic: old hotkey unregistered before new one succeeds, leaving app with no hotkey on failure (WindowService.cs:72-83)
- [x] No user-facing feedback when RegisterHotKey fails -- only Trace.WriteLine (WindowService.cs:80-83)
- [x] OpenShortcutsFolder and LaunchApp have no try/catch -- exceptions propagate unhandled (MainViewModel.cs:171-188)
- [x] ShortcutService.GetShortcutFiles has no error handling for UnauthorizedAccessException (ShortcutService.cs:16-27)
- [x] SettingsService.InitializeAsync has no try-catch; callers use fire-and-forget (SettingsService.cs:101-107)
- [x] Parallel.ForEachAsync without bounded parallelism -- could exhaust GDI handles on many-core systems (MainViewModel.cs:137)
- [x] CancellationToken from Parallel.ForEachAsync never forwarded to inner async operations (MainViewModel.cs:137-159)
- [x] WindowService constructor has no null guards on parameters -- inconsistent with all other services (WindowService.cs:20-24)

### Resource Leaks & Lifecycle
- [x] SettingsWindow not closed on app exit -- orphaned window remains (MainWindow.xaml.cs:190-195)
- [x] Missing PointerCaptureLost handler: _isDraggingWindow stays true if capture lost unexpectedly (MainWindow.xaml.cs:69-71)
- [x] WindowService.Dispose() missing _disposed guard and finalizer despite managing unmanaged resources (WindowService.cs:172-206)
- [x] IWindowService does not extend IDisposable; Cleanup() duplicates Dispose() (IWindowService.cs, WindowService.cs:167)

### Security
- [x] PathSecurity.IsUnsafePath: catch blocks return false (safe) on parse failure -- should default to true (unsafe) (PathSecurity.cs:39-52)
- [x] FileSystem performs no path validation -- defense-in-depth gap (FileSystem.cs)
- [x] PublishTrimmed enabled without TrimMode or SuppressTrimAnalysisWarnings -- WinUI reflection may break (Launchbox.csproj:62-66)

### Architecture & Code Quality
- [x] Missing IIconService and IShortcutService interfaces -- breaks consistent abstraction pattern (IconService.cs, ShortcutService.cs)
- [x] Magic strings for modifier keys duplicated in 3 places -- use a dictionary (SettingsViewModel.cs:24,118-131)
- [x] Constants.ALLOWED_EXTENSIONS array is mutable at runtime -- use IReadOnlyList<string> (Constants.cs:25)
- [x] AppItem.Name/Path don't raise PropertyChanged -- should be { get; init; } to enforce set-once intent (AppItem.cs:10-11)
- [x] PrivateExtractIcons has obfuscated param names (l, n, cx, p) and incorrect types for general use (NativeMethods.cs:12)
- [x] Debug.WriteLine used instead of Trace.WriteLine per coding standards (MainWindow.xaml.cs:82,84)

### UI/UX
- [x] Empty-state StackPanel and GridView overlap -- no mutual exclusion in XAML (MainWindow.xaml:34-97)
- [x] Double-click on tray icon fires show-then-hide (single+double click both toggle) (MainWindow.xaml:22-23)
- [x] Missing accessibility labels on Settings form controls (SettingsWindow.xaml) and main grid (MainWindow.xaml:57)

### Build & CI
- [x] PR CI only builds test project, not the full WinUI app: XAML/binding/WinUI compile regressions can merge undetected since the test project links only a subset of production files (dotnet-desktop.yml:112) [Codex]
- [x] ARM64 excluded from MSIX bundle despite being a declared target platform (dotnet-desktop.yml:155)
- [x] Missing ImplicitUsings in main project but enabled in test project -- file-linked sources may behave differently
- [x] Solution AnyCPU maps silently to x86 (Launchbox.sln:19-22)
- [x] No code coverage collection in CI despite coverlet.collector being a dependency
- [x] No Directory.Build.props for centralized project configuration (nullable, TFM, warnings)

### Tests
- [x] No launcher-level test for `.url` execution bypass: resolver tests verify scheme rejection, but no test asserts that WinUILauncher blocks `.url` files when resolution returns null (WinUILauncherSecurityTests.cs) [Codex]
- [x] SettingsViewModelTests use fragile async polling with DateTime timeout -- should use event-driven waiting
- [x] Mock classes (MockSettingsStore, MockImageFactory, etc.) scattered inside unrelated test files -- extract to own files
- [x] MockFileSystem has no error simulation capability unlike other mocks (MockStartupService has ShouldFail)

## Medium (New)

### Performance
- [x] FilteredApps double enumeration: HasNoMatches calls FilteredApps.Any() which re-runs the LINQ Where on every keystroke -- cache as materialized list (MainViewModel.cs:53-59)

### UI/UX
- [ ] SearchBox has no visual polish: missing Background, Padding, Margin, CornerRadius -- looks like an unstyled floating field against the backdrop (MainWindow.xaml:143-148)
- [ ] Window height not clamped to work area: WINDOW_HEIGHT=700 fixed constant can exceed display on small screens like Surface Pro at 150% scaling (Constants.cs, WindowService.cs)

### Performance
- [x] Window position saved on every drag frame: `AppWindow_Changed` calls `SaveWindowPosition()` on every position/size change with no debounce -- causes heavy disk I/O during drag operations (WindowService.cs:131-136) [Gemini]

### Architecture & Code Quality
- [x] ProcessStarter hardcodes `new WindowsShortcutResolver(new FileSystem())` inline, violating DI and bypassing mocks in tests (ProcessStarter.cs:20) [Gemini]

### Reliability
- [x] Hotkey values not validated from storage: corrupt or hostile local settings can persist invalid modifier/key codes, causing repeated registration failures every startup with no recovery path (SettingsService.cs:58, WindowService.cs:95) [Codex]
- [x] Window hides before launch attempt: MainViewModel hides the window before `Launch()` -- if the shortcut is broken or blocked, the UI is already gone and failures are invisible (MainViewModel.cs:242) [Codex]
- [ ] Settings writes fail silently: `LocalSettingsStore.SetValue` only logs on exception but callers raise `PropertyChanged` and proceed as if persistence succeeded -- settings appear saved in-session but are lost after restart (LocalSettingsStore.cs:35, SettingsService.cs:50) [Codex]
- [x] VisualTree 5-file abstraction stack (VisualTreeFinder, IVisualTreeService, WinUIVisualTreeService, IVisualTreeHelperWrapper, VisualTreeHelperWrapper) is unused in production -- MainWindow uses VisualTreeHelper.GetParent directly. Remove or consolidate.
- [ ] Double call to SettingsService.InitializeAsync() -- called from MainWindow constructor and again from SettingsViewModel constructor when Settings window opens. Harmless but redundant with asymmetric error handling.
- [ ] _loadCts race condition with AllowConcurrentExecutions=true is theoretically unsafe -- all current callers are on the UI thread, but the attribute signals concurrent safety that doesn't exist. Document or fix with Interlocked.Exchange.

### Features
- [ ] Keyboard navigation: arrow keys to move through the grid, Enter to launch -- essential for a keyboard-first launcher
- [ ] Window height auto-sizing: detect work area and clamp/resize on activation
- [ ] Custom icon support UI: the .icons/ directory feature exists but there's no UI to set custom icons (users must manually place files)
- [ ] Multi-folder shortcut sources: support multiple folders instead of just one

## Low

- [ ] Tray tooltip hardcoded to "Alt+S" despite configurable hotkey -- becomes misleading once the user changes the shortcut (MainWindow.xaml:25) [Codex]
- [x] .editorconfig enforces 2-space XAML/XML indentation but CLAUDE.md documents 4-space -- contributors and formatting tools receive conflicting guidance (.editorconfig:20, CLAUDE.md) [Codex]
- [x] CLAUDE.md references non-existent `ViewModels/ViewModelBase.cs` -- stale after CommunityToolkit.Mvvm migration removed the file (CLAUDE.md:40) [Codex]
- [x] Icon size mismatch: extracted at 96px, displayed at 56 DIPs -- blurry on high-DPI (Constants.cs:17, MainWindow.xaml:85)
- [x] No TreatWarningsAsErrors in either project -- nullable warnings pass CI silently
- [x] Tray context menu 'Show' label is static -- should toggle to 'Hide' when visible (MainWindow.xaml:26)
- [x] SettingsWindow has no explicit size -- may render poorly on some displays
- [x] No AppItem.ToString() override for debugging/logging (AppItem.cs)
- [x] Missing test coverage: AppItem PropertyChanged, MainViewModel.Dispose, LaunchApp with invalid params
- [x] No [Trait] categorization on tests -- performance/security tests can't be filtered (PerformanceBenchmarkTests)
- [x] Duplicated CreatePng/CreateIco helpers in IconServiceTests and PerformanceBenchmarkTests -- extract to shared TestDataHelpers
- [x] BooleanToVisibilityConverter.ConvertBack throws NotImplementedException -- should return DependencyProperty.UnsetValue
- [x] ImageHeaderParser: no IHDR chunk validation for PNG, no upper bound on ICO entry count
- [x] No version auto-increment in CI -- every build is 1.0.0.0 (Package.appxmanifest:14)
- [x] Launchbox.Tests.csproj has inconsistent indentation -- mix of tabs and spaces (Tests.csproj:6,10)
- [x] Implement .NET 10 collection expressions globally where arrays or lists are initialized.
