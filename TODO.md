# Launchbox TODO

## Critical

- [x] SettingsViewModel memory leak: subscribes to SettingsService.PropertyChanged but never unsubscribes / missing IDisposable (SettingsViewModel.cs:30)
- [x] async void lambda via SimpleCommand(Action) in MainViewModel creates unobservable crash risk (MainViewModel.cs:69) -- create AsyncSimpleCommand accepting Func<Task>

## High

- [x] DPI-unaware window dragging: delta in DIPs applied to physical-pixel position causes lag on >100% DPI (MainWindow.xaml.cs:128-141)
- [x] No reentrancy protection on LoadAppsAsync -- concurrent calls race on Apps collection (MainViewModel.cs:80,84) -- add CancellationTokenSource or SemaphoreSlim
- [x] Missing null guards on all 8 MainViewModel constructor parameters, inconsistent with SettingsViewModel pattern (MainViewModel.cs:48-65)
- [x] async void BrowseFolderAsync has no try/catch -- COM failure would crash app (SettingsViewModel.cs:39-48)
- [x] Fire-and-forget tasks with no error handling: InitializeAsync and SetRunAtStartupAsync failures silently lost (SettingsViewModel.cs:33,76)
- [x] Missing SetLastError=true on ALL 13 P/Invoke declarations -- Win32 error codes unreliable (NativeMethods.cs)
- [ ] IWindowService too narrow: Initialize() and OnActivated() not on interface; MainWindow uses concrete WindowService type, defeating abstraction (IWindowService.cs, WindowService.cs:29,274)
- [ ] RegisterHotKey thread affinity not enforced -- PropertyChanged from background thread would silently fail (WindowService.cs:57-69)
- [ ] No .editorconfig file -- dotnet format in CI enforces invisible rules; AGENTS.md formatting rules unenforceable
- [ ] Test packages severely outdated: xunit 2.6.3->2.9.x, xunit.runner.visualstudio 2.5.5->3.x, Microsoft.NET.Test.Sdk 17.8->17.13 (Tests.csproj:14-17)
- [x] No tests for ImageHeaderParser (binary parsing with zero coverage) and BooleanToVisibilityConverter (not even linked in test project)
- [ ] Certificate thumbprint hardcoded in source (Launchbox.csproj:59) -- should be parameterized

## Medium

### Reliability & Error Handling
- [ ] Hotkey change is not atomic: old hotkey unregistered before new one succeeds, leaving app with no hotkey on failure (WindowService.cs:72-88)
- [ ] No user-facing feedback when RegisterHotKey fails -- only Trace.WriteLine (WindowService.cs:80-83)
- [ ] OpenShortcutsFolder and LaunchApp have no try/catch -- exceptions propagate unhandled (MainViewModel.cs:150-167)
- [ ] ShortcutService.GetShortcutFiles has no error handling for UnauthorizedAccessException (ShortcutService.cs:16-27)
- [ ] SettingsService.InitializeAsync has no try-catch; callers use fire-and-forget (SettingsService.cs:101-107)
- [ ] Parallel.ForEachAsync without bounded parallelism -- could exhaust GDI handles on many-core systems (MainViewModel.cs:124)
- [ ] CancellationToken from Parallel.ForEachAsync never forwarded to inner async operations (MainViewModel.cs:124)

### Resource Leaks & Lifecycle
- [ ] SettingsWindow not closed on app exit -- orphaned window remains (MainWindow.xaml.cs:189-194)
- [ ] Missing PointerCaptureLost handler: _isDraggingWindow stays true if capture lost unexpectedly (MainWindow.xaml.cs:69-71)
- [ ] WindowService.Dispose() missing _disposed guard and finalizer despite managing unmanaged resources (WindowService.cs:177-214)
- [ ] IWindowService does not extend IDisposable; Cleanup() duplicates Dispose() (IWindowService.cs, WindowService.cs:172)

### Security
- [ ] PathSecurity.IsUnsafePath: catch blocks return false (safe) on parse failure -- should default to true (unsafe) (PathSecurity.cs:39-52)
- [ ] FileSystem performs no path validation -- defense-in-depth gap (FileSystem.cs)
- [ ] pull_request_target trigger on labeler workflow without fork safety guard (labeler.yml:4)
- [ ] PublishTrimmed enabled without TrimMode or SuppressTrimAnalysisWarnings -- WinUI reflection may break (Launchbox.csproj:62-66)

### Architecture & Code Quality
- [ ] Missing IIconService and IShortcutService interfaces -- breaks consistent abstraction pattern (IconService.cs, ShortcutService.cs)
- [ ] Magic strings for modifier keys duplicated in 3 places -- use a dictionary (SettingsViewModel.cs:22,86-96)
- [ ] Constants.ALLOWED_EXTENSIONS array is mutable at runtime -- use IReadOnlyList<string> (Constants.cs:25)
- [ ] AppItem.Name/Path don't raise PropertyChanged -- should be { get; init; } to enforce set-once intent (AppItem.cs:10-11)
- [ ] PrivateExtractIcons has obfuscated param names (l, n, cx, p) and incorrect types for general use (NativeMethods.cs:12)
- [ ] BackdropService silent catch blocks and Debug.WriteLine (should be Trace.WriteLine per coding standards) (BackdropService.cs:43-46,69)

### UI/UX
- [ ] Empty-state StackPanel and GridView overlap -- no mutual exclusion in XAML (MainWindow.xaml:34-97)
- [ ] Double-click on tray icon fires show-then-hide (single+double click both toggle) (MainWindow.xaml:22-23)
- [ ] Missing accessibility labels on Settings form controls (SettingsWindow.xaml) and main grid (MainWindow.xaml:57)

### Build & CI
- [ ] ARM64 excluded from MSIX bundle despite being a declared target platform (dotnet-desktop.yml:155)
- [ ] global.json listed in .gitignore -- future SDK pin changes won't be tracked (.gitignore:395)
- [ ] Missing ImplicitUsings in main project but enabled in test project -- file-linked sources may behave differently
- [ ] Solution AnyCPU maps silently to x86 (Launchbox.sln:19-22)
- [ ] No code coverage collection in CI despite coverlet.collector being a dependency
- [ ] No Directory.Build.props for centralized project configuration (nullable, TFM, warnings)

### Tests
- [ ] SettingsViewModelTests use fragile async polling with DateTime timeout -- should use event-driven waiting
- [ ] Mock classes (MockSettingsStore, MockImageFactory, etc.) scattered inside unrelated test files -- extract to own files
- [ ] MockFileSystem has no error simulation capability unlike other mocks (MockStartupService has ShouldFail)

## Low

- [ ] Icon size mismatch: extracted at 96px, displayed at 56 DIPs -- blurry on high-DPI (Constants.cs:17, MainWindow.xaml:85)
- [ ] No TreatWarningsAsErrors in either project -- nullable warnings pass CI silently
- [ ] Tray context menu 'Show' label is static -- should toggle to 'Hide' when visible (MainWindow.xaml:26)
- [ ] SettingsWindow has no explicit size -- may render poorly on some displays
- [ ] No AppItem.ToString() override for debugging/logging (AppItem.cs)
- [ ] Missing test coverage: AppItem PropertyChanged, MainViewModel.Dispose, LaunchApp with invalid params, SettingsService.SettingsChanged event
- [ ] No [Trait] categorization on tests -- performance/security tests can't be filtered (PerformanceBenchmarkTests, IconServiceSecurityTests)
- [ ] Duplicated CreatePng/CreateIco helpers in IconServiceTests and PerformanceBenchmarkTests -- extract to shared TestDataHelpers
- [ ] BooleanToVisibilityConverter.ConvertBack throws NotImplementedException -- should return DependencyProperty.UnsetValue
- [ ] ImageHeaderParser: no IHDR chunk validation for PNG, no upper bound on ICO entry count, silent exception swallowing
- [ ] No version auto-increment in CI -- every build is 1.0.0.0 (Package.appxmanifest:14)
- [ ] In-tree XAML elements using {Binding} instead of {x:Bind} contrary to project conventions (MainWindow.xaml:37,51)
