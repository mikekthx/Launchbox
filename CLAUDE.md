# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build Commands

```bash
# Build (WinUI 3 requires explicit platform — AnyCPU is unsupported)
dotnet build Launchbox.csproj -p:Platform=x64

# Run
dotnet run --project Launchbox.csproj

# Run with hot reload
dotnet watch run --project Launchbox.csproj

# Publish (Release, self-contained)
dotnet publish Launchbox.csproj -c Release -p:Platform=x64

# Format (CI enforces this — run before every commit)
dotnet format Launchbox.sln

# Run all tests
dotnet test Launchbox.Tests/Launchbox.Tests.csproj

# Run a single test
dotnet test Launchbox.Tests/Launchbox.Tests.csproj --filter "FullyQualifiedName~TestMethodName"
```

## Architecture

**Launchbox** is a WinUI 3 / .NET 8 Windows desktop app launcher. It lives in the system tray and toggles a shortcut grid via a global hotkey (default `Alt+S`).

### MVVM Pattern

- **`MainWindow.xaml(.cs)`** — View: window management, global hotkey (Win32 `RegisterHotKey`), tray icon, auto-hide on deactivation, dragging. Binds to `MainViewModel`.
- **`SettingsWindow.xaml(.cs)`** — Secondary window for settings. Instantiates its own `SettingsViewModel`; disposes on close.
- **`ViewModels/MainViewModel.cs`** — Business logic: scanning shortcuts, filtering extensions, launching apps. Uses only interfaces, no WinUI types.
- **`ViewModels/SettingsViewModel.cs`** — Settings page logic: shortcuts path, hotkey configuration, startup toggle.
- **`Models/AppItem.cs`** — Shortcut model. Uses `object` for Icon to stay WinUI-free (enables testing).

### Service Abstraction

All platform-specific operations are behind interfaces in `Services/` to enable unit testing. Implementations are prefixed `WinUI*` or are named directly (e.g., `FileSystem`, `LocalSettingsStore`).

Key services:
- **`WindowService`** (`IWindowService`) — Window positioning, hotkey registration via `RegisterHotKey`/`WndProc`, toggle visibility, auto-hide on deactivation.
- **`SettingsService`** — Central settings coordinator; raises `PropertyChanged` to trigger reloads and hotkey re-registration.
- **`IconService`** — Icon extraction pipeline with caching (`IconCacheEntry`) and `.icons/` directory support.
- **`ShortcutService`** — Discovers shortcut files by allowed extensions.
- **`BackdropService`** (`IBackdropService`) — Handles DWM Blur Glass backdrop effects.
- **`WindowPositionManager`** — Manages window position persistence via `ISettingsStore`.
- **`VisualTreeFinder`** (`IVisualTreeService`) — Visual tree traversal utility.
- **`NativeMethods.cs`** — All P/Invoke declarations centralized here. All declarations must have `SetLastError = true`.

### Helpers

`Helpers/` contains shared utilities:
- **`Constants.cs`** — Global constants: window dimensions, hotkey modifiers (`MOD_ALT`, `MOD_CONTROL`, `MOD_SHIFT`, `MOD_WIN`), key codes, icon sizes, allowed extensions.
- **`SimpleCommand` / `AsyncSimpleCommand`** — `ICommand` implementations for MVVM binding.
- **`PathSecurity`** — Path traversal validation.
- **`IconHelper`** — Icon extraction helpers.
- **`ImageHeaderParser`** — Image format detection from file headers.
- **`BooleanToVisibilityConverter`** — XAML value converter.

### Dependency Composition

**No DI container.** All dependencies are wired manually in the `MainWindow` constructor. `SettingsService`, `WindowService`, and `LocalSettingsStore` are shared singleton instances passed to both ViewModels. Add new service wiring there.

### Window Behavior

- App starts hidden off-screen (`-10000, -10000`); positions itself on the first `Alt+S` press.
- Auto-hides on deactivation (focus loss).
- Position persists via `WindowPositionManager` → `LocalSettingsStore` → `ApplicationData.Current.LocalSettings`.

### Testing Pattern

`Launchbox.Tests` uses **file-linking** (`<Compile Include="..\ClassName.cs" Link="..." />`) instead of a `<ProjectReference>` to include production code without pulling in the WinUI application host. When adding a new testable class, add a `<Compile Include>` entry to `Launchbox.Tests/Launchbox.Tests.csproj`. Use existing mocks (`MockFileSystem`, `MockSettingsStore`, `MockWindowService`, `MockStartupService`, `MockFilePickerService`) or create new ones implementing the relevant interface.

### CI Pipeline

CI runs on push/PR to `main` (`.github/workflows/dotnet-desktop.yml`):
1. **Code format** — `dotnet format --verify-no-changes` (run `dotnet format Launchbox.sln` locally before committing)
2. **Unit tests** — `dotnet test` on both Debug and Release configurations
3. **CodeQL** — Security scanning for C# vulnerabilities
4. **MSIX packaging** — Signed package build (push to `main` only)
5. **Artifact attestation** — Build provenance for published packages

## Code Style

- 4-space indentation, **Allman** brace style, ~120 char line width
- File-scoped namespaces, one class per file
- **Nullable reference types enabled** — use `?` suffix everywhere
- `string.Empty` not `""`, `var` when type is obvious, modern syntax (`new()`, `[]`)
- Async methods suffixed `Async`; fire-and-forget: `_ = LoadAppsAsync();`
- Errors: `System.Diagnostics.Trace.WriteLine(...)` — never swallow silently
- Constants: `UPPER_SNAKE_CASE`; private fields: `_camelCase`

### Import Order

Alphabetical by namespace (enforced by `dotnet format`):

1. `Launchbox.*` (project namespaces)
2. `Microsoft.UI.*`
3. `System.*`
4. `Windows.*`

Use type aliases when disambiguating: `using WinIcon = System.Drawing.Icon;`

### XAML

- Use `{x:Bind}` (compiled bindings) for in-tree elements. For dynamic types use a cast: `{x:Bind (media:ImageSource)Icon}`.
- For out-of-tree elements (e.g., `TaskbarIcon`, `ContextFlyout`, `MenuFlyout`) use `{Binding}` to avoid CS1503 errors; ensure `RootGrid.DataContext = this;` is set in code-behind.
- Semantic element names: `RootGrid`, `AppGrid`, `TrayIcon`.

### WinUI 3 Patterns

```csharp
// Window handle
IntPtr hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hWnd);
var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);

// UI thread dispatch
this.DispatcherQueue.TryEnqueue(() => { /* UI work */ });
```

## Common Tasks

- **Add shortcut property:** `Models/AppItem.cs` → populate in `MainViewModel.LoadAppsAsync()` → update XAML DataTemplate.
- **Change hotkey:** `Helpers/Constants.cs` (`MOD_ALT`/`VK_S`) + `ToolTipText` in `MainWindow.xaml`.
- **Add tray menu item:** `ContextFlyout` in `MainWindow.xaml` + command/handler in `MainWindow` class.
- **Add a new service:** Create interface in `Services/I*.cs` → implementation in `Services/*.cs` (or `WinUI*.cs` for platform-specific) → wire in `MainWindow` constructor → pass to ViewModel if needed.
- **Add a new test:** Add `<Compile Include>` to test `.csproj` for all production classes under test, create `ClassNameTests.cs`.
