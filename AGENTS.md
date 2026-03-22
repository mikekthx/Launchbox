# Launchbox — Agent Guide

Guidance for AI coding agents working on this codebase. This is the single source of truth — CLAUDE.md and GEMINI.md are symlinks to this file.

## Project Overview

**Launchbox** is a Windows desktop app launcher built with:
- **Framework:** WinUI 3 (Windows App SDK 1.8)
- **Language:** C# on .NET 10.0
- **Platform:** Windows 10/11 (x86, x64, ARM64)

The app displays shortcuts from `Desktop\Shortcuts` in a grid, lives in the system tray, and toggles visibility via **Alt+S** global hotkey.

## Build Commands

```bash
# Build (Debug, x64)
dotnet build Launchbox.csproj -p:Platform=x64

# Run
dotnet run --project Launchbox.csproj

# Run with hot reload
dotnet watch run --project Launchbox.csproj

# Publish (Release, self-contained)
dotnet publish Launchbox.csproj -c Release -p:Platform=x64

# Clean
dotnet clean Launchbox.csproj
```

## Testing

**Test framework configured: xUnit.**
- Project: `Launchbox.Tests` (net10.0-windows10.0.19041.0)
- Run: `dotnet test Launchbox.Tests/Launchbox.Tests.csproj`
- Single test: `dotnet test --filter "FullyQualifiedName~TestMethodName"`

Note: Both the main project and test project have WinUI dependencies. `dotnet test` requires Windows.

The test project uses **file-linking** (`<Compile Include="..\ClassName.cs" Link="..." />`) instead of a `<ProjectReference>` to include production code. This avoids pulling in the WinUI application host while testing real production classes. When adding a new testable class, add a corresponding `<Compile Include>` entry to `Launchbox.Tests/Launchbox.Tests.csproj`.

### CI/CD

CI runs on push/PR to `main` (`.github/workflows/dotnet-desktop.yml`):
1. **Code format** — `dotnet format --verify-no-changes` (run `dotnet format Launchbox.sln` locally before committing)
2. **App build** — `dotnet build` of the full WinUI app to catch XAML/binding compile errors
3. **Unit tests** — `dotnet test` on both Debug and Release configurations
4. **CodeQL** — Security scanning for C# vulnerabilities
5. **MSIX packaging** — Signed package build (push to `main` only)
6. **Artifact attestation** — Build provenance for published packages

## Project Structure

```
Launchbox/
├── README.md                   # Project documentation
├── App.xaml(.cs)               # Application entry point
├── MainWindow.xaml(.cs)        # Main window UI and window management logic
├── SettingsWindow.xaml(.cs)    # Settings dialog UI
├── Launchbox.csproj            # Project configuration
├── Helpers/                    # Utility classes and constants
│   ├── BooleanToVisibilityConverter.cs
│   ├── BulkObservableCollection.cs # ObservableCollection with batch-update support
│   ├── Constants.cs            # Global constants (hotkey, window size, etc.)
│   ├── GridSize.cs             # Small/Medium/Large grid size enum
│   ├── IconHelper.cs           # Icon extraction helpers
│   ├── ImageHeaderParser.cs    # Image format detection
│   ├── ListViewBaseExtensions.cs # Attached property for ItemClick → ICommand binding
│   ├── Localization.cs         # Static accessor for localized strings (IStringProvider seam)
│   ├── LocalizedOption.cs      # Record pairing English storage key with localized display name
│   └── PathSecurity.cs         # Path validation and sanitization
│   # CommunityToolkit.Mvvm provides ObservableObject base class,
│   # [RelayCommand] and [ObservableProperty] source generators
├── Models/                     # Data models
│   └── AppItem.cs              # Application shortcut model
├── Services/                   # Platform-agnostic interfaces and implementations
│   ├── IAppLauncher.cs         # Process launching abstraction
│   ├── IBackdropService.cs     # Window backdrop management
│   ├── IDispatcher.cs          # UI thread dispatch abstraction
│   ├── IFilePickerService.cs   # File/folder picker abstraction
│   ├── IFileSystem.cs          # File system operations abstraction
│   ├── IImageFactory.cs        # Image creation from bytes
│   ├── ISettingsStore.cs       # Settings persistence abstraction
│   ├── IStartupService.cs      # Startup registration abstraction
│   ├── IStringProvider.cs      # Localized string access abstraction
│   ├── IWindowService.cs       # Window management abstraction
│   ├── ResourceStringProvider.cs # Production IStringProvider using ResourceLoader
│   └── ...                     # WinUI implementations (WinUI*.cs)
├── ViewModels/                 # MVVM ViewModels (use CommunityToolkit.Mvvm source generators)
│   ├── MainViewModel.cs        # Core application logic (loading/launching apps)
│   └── SettingsViewModel.cs    # Settings page logic
├── Strings/                    # Localization resources (.resw)
│   ├── en-US/Resources.resw    # English (fallback)
│   ├── es/Resources.resw       # Spanish
│   ├── fr/Resources.resw       # French
│   ├── de/Resources.resw       # German
│   ├── it/Resources.resw       # Italian
│   ├── ja/Resources.resw       # Japanese
│   ├── ko/Resources.resw       # Korean
│   ├── pl/Resources.resw       # Polish
│   ├── pt-BR/Resources.resw    # Brazilian Portuguese
│   ├── ru/Resources.resw       # Russian
│   ├── tr/Resources.resw       # Turkish
│   ├── zh-Hans/Resources.resw  # Simplified Chinese
│   └── zh-Hant/Resources.resw  # Traditional Chinese
├── Launchbox.Tests/            # xUnit test project (file-linked)
├── Assets/                     # Application icons
└── Properties/                 # Launch/publish profiles
```

## Start Here

When starting a new task, inspect these files first:
- `MainWindow.xaml.cs` - app composition, window lifecycle, tray integration, hotkey wiring
- `ViewModels/MainViewModel.cs` - core launcher behavior, app loading, search/filter, command flow
- `Services/WindowService.cs` - visibility toggling, hotkey registration, persisted window position
- `Services/SettingsService.cs` - settings coordination and change notifications
- `Services/IconService.cs` - icon extraction, caching, `.icons` override behavior

For settings-related tasks, also inspect:
- `SettingsWindow.xaml.cs`
- `ViewModels/SettingsViewModel.cs`

## Code Style

### Formatting
- 4-space indentation for C# (2-space for XAML/XML/csproj per `.editorconfig`), Allman brace style
- ~120 char line length
- File-scoped namespaces preferred
- One class per file (helper classes inline OK)

### Naming Conventions

| Element                    | Convention       | Example                       |
| -------------------------- | ---------------- | ----------------------------- |
| Classes/Methods/Properties | PascalCase       | `MainWindow`, `LoadAppsAsync` |
| Private fields             | _camelCase       | `_isDraggingWindow`           |
| Constants                  | UPPER_SNAKE_CASE | `HOTKEY_ID`, `MOD_ALT`        |
| Parameters/Locals          | camelCase        | `sender`, `displayArea`       |

### Imports Order

Alphabetical by namespace (enforced by `dotnet format`):

1. `Launchbox.*` (project namespaces)
2. `Microsoft.UI.*`
3. `System.*`
4. `Windows.*`

Use type aliases for disambiguation:
```csharp
using WinIcon = System.Drawing.Icon;
```

### Type System
- **Nullable reference types ENABLED** - use `?` suffix: `BitmapImage?`
- Use `string.Empty` not `""`
- Use `var` when type is obvious
- Use modern syntax: `new()`, `[]`

### Modern .NET 10 / C# 12+ Coding Standards
Always prefer modern language features and idioms throughout the codebase:
- **Collection expressions** (C# 12): Use `[]` for empty collections and `[item1, item2]` for initialized collections instead of `new List<T>()` or `new T[] { ... }`
- **Primary constructors** (C# 12): Prefer primary constructors for simple types where appropriate
- **`nameof`**: Use `nameof(...)` instead of hardcoded string literals for member names
- **Pattern matching**: Prefer `is` patterns, switch expressions, and property patterns over `is`/`as` casts
- **`using` declarations**: Use `using var` (C# 8+) instead of `using () { }` blocks where the scope is the enclosing method
- **Target-typed `new`**: Use `new()` when the type is already apparent from context
- **File-scoped namespaces**: Always use file-scoped `namespace Foo;` declarations (one per file)
- **Raw string literals**: Use `"""..."""` for multiline or embedded-quote strings
- Avoid legacy constructs such as `ArrayList`, non-generic collections, or old-style array initializers (`new int[] { 1, 2, 3 }` → `[1, 2, 3]`)

### Async/Await
- Suffix with `Async`: `LoadAppsAsync()`
- Fire-and-forget: `_ = LoadAppsAsync();`
- `ConfigureAwait(false)` in library code only

### Error Handling
```csharp
try
{
    // operation
}
catch (Exception ex)
{
    System.Diagnostics.Trace.WriteLine($"Failed to {action}: {ex.Message}");
}
```
- Always log errors with context (use `Trace.WriteLine` for production visibility)
- Never swallow exceptions silently
- Use `finally` for cleanup

### P/Invoke
All P/Invoke declarations are centralized in `Services/NativeMethods.cs`. All declarations must include `SetLastError = true`:
```csharp
[DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
public static extern bool SetForegroundWindow(IntPtr hWnd);
```

### Code Organization
Use section comments in large files:
```csharp
// --- WINDOW SETUP ---
// --- WIN32 IMPORTS ---
// --- HELPER CLASSES ---
```

### XAML
- 2-space indentation (per `.editorconfig`)
- Multi-line attributes for complex elements
- Use `x:Bind` (compiled bindings) for simple properties. For dynamic types (e.g. `AppItem.Icon`), use `x:Bind` with a cast: `{x:Bind (media:ImageSource)Icon}`.
- When adding commands or variables to out-of-tree UI elements (such as TaskbarIcon, ContextFlyout, or MenuFlyout), always use standard {Binding} instead of {x:Bind} to avoid CS1503 casting errors during compilation. Ensure `RootGrid.DataContext = this;` is set in the code-behind.
- Semantic names: `RootGrid`, `AppGrid`, `TrayIcon`

### Comment Style

**Principle:** Comments explain *why*, not *what*. Code should be readable enough that restating it in English adds no value.

**When to add a comment:**
- The reason for a decision is non-obvious (e.g., a workaround, a threading constraint, a Win32 quirk)
- Omitting a step would look like a bug to a future reader
- A security or correctness invariant must be preserved

**When NOT to add a comment:**
- The code already names the operation clearly (`LoadAppsAsync`, `IsUnsafePath`)
- The comment would just repeat the method/property name in prose

**Formats used in this codebase:**

`//` line comments — for inline rationale, section labels, and non-obvious decisions:
```csharp
// RegisterHotKey is thread-affine: must be called on the window's creating thread.
// Suppress: prevents default maximize on title-bar double-click (Launchbox has no title bar).
// Security: limit file size to prevent DoS via large files.
// Defense-in-depth: validate shortcut target even though the launcher already checked it.
```

`///` XML doc comments — only for public or internal members whose contract is not obvious from the name and signature alone:
```csharp
/// <summary>
/// Loads shortcuts from the configured folder and concurrently extracts their icons.
/// Cancels any previous in-flight load (debounce behavior).
/// </summary>
```

**No `/* */` block comments.** Use `//` for multi-line explanations instead.

**Section labels** in large constructors or files (sparingly):
```csharp
// 1. WINDOW SETUP
// 2. SERVICES
// 3. EVENT HOOKS
```

**Security annotations** — always label security-relevant decisions:
```csharp
// Security: prevent symlink redirection attacks on INI files.
// (Defense-in-depth) validate even though caller already checked.
```

**No boilerplate/template placeholder comments** — remove Visual Studio template comments like `<!-- Other merged dictionaries here -->` when they add no information.

## WinUI 3 Patterns

```csharp
// Window handle access
IntPtr hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hWnd);
var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);

// UI thread dispatch
this.DispatcherQueue.TryEnqueue(() => { /* UI work */ });

// Local settings
var settings = Windows.Storage.ApplicationData.Current.LocalSettings.Values;
settings["Key"] = value;
```

## Dependencies

| Package                          | Purpose                 |
| -------------------------------- | ----------------------- |
| Microsoft.WindowsAppSDK 1.8      | WinUI 3 framework       |
| Microsoft.Windows.SDK.BuildTools | Windows SDK build tools |
| CommunityToolkit.Mvvm 8.4.x      | MVVM source generators  |
| H.NotifyIcon.WinUI               | System tray support     |
| System.Drawing.Common            | Icon extraction         |

## Architecture Notes

### MVVM Pattern
The application follows the Model-View-ViewModel (MVVM) pattern:
- **View (`MainWindow.xaml`):** Handles UI layout, window management, hotkeys, and tray icon. Binds to `MainViewModel`.
- **ViewModel (`MainViewModel.cs`):** Encapsulates business logic (scanning shortcuts, filtering extensions, launching apps). It is platform-agnostic and uses interfaces for platform services. Commands use `[RelayCommand]` source generators from CommunityToolkit.Mvvm.
- **Model (`Models/AppItem.cs`):** Represents an application shortcut. Uses `object` for the Icon property to avoid dependency on WinUI types, allowing for easier testing.

### Service Abstraction
Platform-specific operations are abstracted behind interfaces in `Services/` to enable unit testing:
- `IAppLauncher`: Handles process launching (`Process.Start`).
- `IBackdropService`: Manages window backdrop effects (Mica, Acrylic).
- `IDispatcher`: Abstracts thread dispatching (`DispatcherQueue`).
- `IFilePickerService`: Abstracts file/folder picker dialogs.
- `IFileSystem`: Abstracts file system operations (directory enumeration, file reads).
- `IImageFactory`: Creates UI images (e.g., `BitmapImage`) from raw bytes.
- `ISettingsStore`: Abstracts settings persistence (LocalSettings).
- `IStartupService`: Manages app startup registration.
- `IWindowService`: Abstracts window management operations.

Additional services:
- `SettingsService`: Central settings coordinator. Raises `PropertyChanged` events that trigger app reloads and hotkey re-registration.
- `IconService` (`IIconService`): Icon extraction pipeline with caching, custom icon support (`.icons/` directory), and resolution comparison.
- `ShortcutService` (`IShortcutService`): Discovers and filters shortcut files by allowed extensions.
- `ProcessStarter` (`IProcessStarter`): Wraps `Process.Start` with `ProcessStartInfo`-level validation (unsafe FileName, Arguments, WorkingDirectory). Does NOT resolve shortcuts — that's `WinUILauncher`'s job.
- `ProcessService` (`IProcessService`): Higher-level process operations.
- `WinUILauncher` (`IAppLauncher`): Sole owner of shortcut security validation. Resolves `.lnk`/`.url` metadata via `IShortcutResolver`, validates target/args/workingDir, then delegates to `IProcessStarter`.
- `WindowsShortcutResolver` (`IShortcutResolver`): Resolves `.lnk` shortcut targets via ShellLink COM interop and `.url` files via INI parsing.
- `WindowPositionManager`: Manages window position persistence via `ISettingsStore`.
- `NativeMethods`: Centralized P/Invoke declarations (user32, kernel32). All declarations must have `SetLastError = true`.

### Window Behavior
- App starts **hidden off-screen**, positions on first Alt+S press
- Window **auto-hides on deactivation** (focus loss)
- Position persists via LocalSettings
- System tray icon required for operation
- Global hotkey Alt+S via Win32 RegisterHotKey

### Preserve These Behaviors
- The tray icon is operationally required; avoid changes that allow the app to start without it
- Initial off-screen startup is intentional; do not replace it with a visible initial show without explicit product intent
- Auto-hide on deactivation is core launcher behavior, not incidental UI polish
- Hotkey registration and re-registration are thread-affine; keep registration work on the window-owning/UI thread
- Window position persistence must remain compatible with existing LocalSettings-based storage

### Localization
The app uses WinUI 3 native `.resw` resource files with automatic Windows language detection across 13 locales (en-US, es, fr, de, it, ja, ko, pl, pt-BR, ru, tr, zh-Hans, zh-Hant).

- **XAML strings:** Use `x:Uid` attributes on in-tree elements. The `.resw` keys follow the pattern `ElementName.Property` (e.g., `MainWindow_SearchBox.PlaceholderText`).
- **Out-of-tree strings** (tray menu, flyout): ViewModel properties backed by `Localization.GetString()`, bound via `{Binding}`.
- **Code-behind strings:** Use `Localization.GetString("KeyName")` directly.
- **Settings storage:** Uses `LocalizedOption` records that pair an English storage key (`Value`) with a localized display name (`DisplayName`). Settings always persist the English key; display uses the localized name.
- **Test seam:** `Localization` defaults to a `DefaultStringProvider` that returns the key itself. Tests inject `MockStringProvider` via `Localization.SetProvider()`. The production app initializes `ResourceStringProvider` in `App.xaml.cs`. This avoids `COMException` from `ResourceLoader` in unpackaged test contexts.
- **Adding a new localized string:** Add the key to all 13 `Strings/*/Resources.resw` files. For XAML, add `x:Uid` to the element. For C#, call `Localization.GetString("KeyName")`.

### Dependency Composition
All dependencies are composed **manually** in the `MainWindow` constructor (no DI container). `SettingsService`, `WindowService`, and `LocalSettingsStore` are shared singleton instances passed to both `MainViewModel` and `SettingsViewModel`. When adding a new service, wire it up in the `MainWindow` constructor.

## Common Tasks

### Add app item property
1. Add to `AppItem` class in `Models/AppItem.cs`
2. Populate in `MainViewModel.LoadAppsAsync()`
3. Update XAML DataTemplate if needed

### Modify window behavior
1. Event handlers in MainWindow constructor
2. State via `this.AppWindow` methods
3. Advanced: Win32 interop in `NewWndProc()`

### Change hotkey
1. Modify `MOD_ALT`/`VK_S` constants in `Helpers/Constants.cs`
2. Update `ToolTipText` in `MainWindow.xaml` (search for `ToolTipText`)

### Add tray menu item
1. Add `MenuFlyoutItem` in `MainWindow.xaml` (search for `ContextFlyout`)
2. Add command property and handler in MainWindow class (or bind to ViewModel command)

### Add a new test
1. Add a `<Compile Include="..\Path\To\Class.cs" Link="Path\To\Class.cs" />` entry in `Launchbox.Tests/Launchbox.Tests.csproj`
2. Create a test file in `Launchbox.Tests/` following the `ClassNameTests.cs` naming convention
3. Use existing mock classes (`MockFileSystem`, `MockSettingsStore`, etc.) or create new ones implementing the relevant interface
4. Run: `dotnet test Launchbox.Tests/Launchbox.Tests.csproj`

## Verification Guidance

Choose verification based on the type of change:
- UI/XAML-only change: run `dotnet build Launchbox.csproj -p:Platform=x64`
- ViewModel or service logic change: run targeted tests first, then broaden to `dotnet test Launchbox.Tests/Launchbox.Tests.csproj` if impact is wider
- Binding, converter, or markup contract change: run both `dotnet build Launchbox.csproj -p:Platform=x64` and relevant tests
- Non-trivial C# or XAML edits before commit: run `dotnet format Launchbox.sln`

Prefer the smallest verification set that meaningfully exercises the affected behavior, but do not skip the app build for changes that can break WinUI compilation.

## Known Sharp Edges

- The test project uses file-linking, not `<ProjectReference>`; new production files under test must be linked manually in `Launchbox.Tests/Launchbox.Tests.csproj`
- `AppItem.Icon` intentionally uses `object`; replacing it with a WinUI-specific type will make testing harder and spread UI coupling
- Out-of-tree XAML elements such as tray and flyout content must use `{Binding}` rather than `{x:Bind}` to avoid compile-time binding/casting failures
- Service wiring is manual in `MainWindow`; adding a new service in only one code path will create inconsistent runtime behavior
- `NativeMethods.cs` is the central location for P/Invoke declarations; avoid scattering Win32 imports into feature files
- Hotkey, tray, and auto-hide behavior span both window code and services; regressions often come from changing one side without tracing the full event flow
- `WinUILauncher` is the sole owner of shortcut security validation (target, args, workingDir via COM). `ProcessStarter` only validates `ProcessStartInfo` fields as defense-in-depth — do not add shortcut resolution back to `ProcessStarter`
- Localized strings used in tests require `Localization.SetProvider(new MockStringProvider(...))` setup; without it, `Localization.GetString()` returns the key itself (via `DefaultStringProvider`). Test classes that mutate the static provider must use `[Collection("Localization")]` to prevent parallel execution conflicts
- `.gitattributes` normalizes line endings to LF in the repo; without it, `core.autocrlf=true` on Windows causes phantom "modified" files in `git status`

## Date Awareness
When creating or updating files that require the current date (e.g., `.jules/scribe.md`, log files), **ALWAYS** verify the actual system date first by running `date +%Y-%m-%d` in the terminal. Do not guess or rely on pre-trained defaults.
