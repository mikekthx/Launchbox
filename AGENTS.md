# AGENTS.md - Launchbox

Guidance for AI coding agents working on this codebase.

## Project Overview

**Launchbox** is a Windows desktop app launcher built with:
- **Framework:** WinUI 3 (Windows App SDK 1.8)
- **Language:** C# on .NET 8.0
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
- Project: `Launchbox.Tests` (net8.0)
- Run: `dotnet test Launchbox.Tests/Launchbox.Tests.csproj` (cross-platform)
- Single test: `dotnet test --filter "FullyQualifiedName~TestMethodName"`

Note: `dotnet test Launchbox.sln` works on Windows but may fail on Linux due to WinUI dependencies in the main project.

## Project Structure

```
Launchbox/
├── README.md                   # Project documentation
├── App.xaml(.cs)               # Application entry point
├── MainWindow.xaml(.cs)        # Main window UI and window management logic
├── Launchbox.csproj            # Project configuration
├── Constants.cs                # Global constants
├── Services/                   # Platform-agnostic interfaces and implementations
│   ├── IAppLauncher.cs
│   ├── IDispatcher.cs
│   └── IImageFactory.cs
├── ViewModels/                 # MVVM ViewModels
│   └── MainViewModel.cs        # Core application logic (loading/launching apps)
├── Assets/                     # Application icons
└── Properties/                 # Launch/publish profiles
```

## Code Style

### Formatting
- 4-space indentation, Allman brace style
- ~120 char line length
- File-scoped namespaces preferred
- One class per file (helper classes inline OK)

### Naming Conventions

| Element | Convention | Example |
|---------|------------|---------|
| Classes/Methods/Properties | PascalCase | `MainWindow`, `LoadAppsAsync` |
| Private fields | _camelCase | `_isDraggingWindow` |
| Constants | UPPER_SNAKE_CASE | `HOTKEY_ID`, `MOD_ALT` |
| Parameters/Locals | camelCase | `sender`, `displayArea` |

### Imports Order
1. `Microsoft.UI.*` namespaces
2. `System.*` namespaces
3. `Windows.*` namespaces
4. Third-party, then project namespaces

Use type aliases for disambiguation:
```csharp
using WinIcon = System.Drawing.Icon;
```

### Type System
- **Nullable reference types ENABLED** - use `?` suffix: `BitmapImage?`
- Use `string.Empty` not `""`
- Use `var` when type is obvious
- Use modern syntax: `new()`, `[]`

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
Place in dedicated section, single-line format:
```csharp
[DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr hWnd);
```

### Code Organization
Use section comments in large files:
```csharp
// --- WINDOW SETUP ---
// --- WIN32 IMPORTS ---
// --- HELPER CLASSES ---
```

### XAML
- 4-space indentation
- Multi-line attributes for complex elements
- Use `x:Bind` (compiled bindings) for simple properties. For dynamic types (e.g. `AppItem.Icon`), use `x:Bind` with a cast: `{x:Bind (media:ImageSource)Icon}`.
- Semantic names: `RootGrid`, `AppGrid`, `TrayIcon`

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

| Package | Purpose |
|---------|---------|
| Microsoft.WindowsAppSDK 1.8 | WinUI 3 framework |
| H.NotifyIcon.WinUI | System tray support |
| System.Drawing.Common | Icon extraction |

## Architecture Notes

### MVVM Pattern
The application follows the Model-View-ViewModel (MVVM) pattern:
- **View (`MainWindow.xaml`):** Handles UI layout, window management, hotkeys, and tray icon. Binds to `MainViewModel`.
- **ViewModel (`MainViewModel.cs`):** Encapsulates business logic (scanning shortcuts, filtering extensions, launching apps). It is platform-agnostic and uses interfaces for platform services.
- **Model (`AppItem.cs`):** Represents an application shortcut. Uses `object` for the Icon property to avoid dependency on WinUI types, allowing for easier testing.

### Service Abstraction
Platform-specific operations are abstracted behind interfaces in `Launchbox/Services/` to enable unit testing:
- `IImageFactory`: Creates UI images (e.g., `BitmapImage`) from raw bytes.
- `IAppLauncher`: Handles process launching (`Process.Start`).
- `IDispatcher`: Abstracts thread dispatching (`DispatcherQueue`).

- App starts **hidden off-screen**, positions on first Alt+S press
- Window **auto-hides on deactivation** (focus loss)
- Position persists via LocalSettings
- System tray icon required for operation
- Global hotkey Alt+S via Win32 RegisterHotKey

## Common Tasks

### Add app item property
1. Add to `AppItem` class in `AppItem.cs`
2. Populate in `MainViewModel.LoadAppsAsync()`
3. Update XAML DataTemplate if needed

### Modify window behavior
1. Event handlers in MainWindow constructor
2. State via `this.AppWindow` methods
3. Advanced: Win32 interop in `NewWndProc()`

### Change hotkey
1. Modify `MOD_ALT`/`VK_S` constants in `Constants.cs`
2. Update `ToolTipText` in `MainWindow.xaml:18`

### Add tray menu item
1. Add `MenuFlyoutItem` in `MainWindow.xaml:22-28`
2. Add command property and handler in MainWindow class (or bind to ViewModel command)
