# Launchbox Project Context

Launchbox is a modern, lightweight Windows desktop application launcher built with **WinUI 3** and **.NET 10**. It lives in the system tray and provides instant access to shortcuts via a global hotkey (default `Alt+S`).

## Project Overview

- **Main Technologies**: C#, .NET 10, WinUI 3 (Windows App SDK), CommunityToolkit.Mvvm, xUnit.
- **Key Features**:
  - System tray integration with context menu.
  - Global hotkey registration for instant toggle.
  - Modern WinUI 3 UI with acrylic/mica backdrop.
  - Automatic shortcut discovery and custom icon support.
  - Persistent window positioning and startup behavior.
- **Architecture**: Strict MVVM pattern with platform-specific logic abstracted behind interfaces in `Services/` to enable unit testing without the WinUI host.

## Getting Started

### Prerequisites
- Windows 10 (1809+) or Windows 11
- .NET 10.0 SDK
- Visual Studio 2022 (17.8+) with .NET Desktop Development workload

### Core Commands
| Task | Command |
| :--- | :--- |
| **Build** | `dotnet build Launchbox.csproj -p:Platform=x64` |
| **Run** | `dotnet run --project Launchbox.csproj` |
| **Watch** | `dotnet watch run --project Launchbox.csproj` |
| **Test** | `dotnet test Launchbox.Tests/Launchbox.Tests.csproj` |
| **Format** | `dotnet format Launchbox.sln` |
| **Publish** | `dotnet publish Launchbox.csproj -c Release -p:Platform=x64` |

*Note: WinUI 3 requires an explicit platform (x64, x86, or ARM64). `AnyCPU` is not supported.*

## Project Structure

- `Models/`: Data contracts (e.g., `AppItem.cs`).
- `ViewModels/`: UI logic. `MainViewModel` for the launcher, `SettingsViewModel` for configuration.
- `Services/`: Core business logic and platform abstractions.
  - `I*.cs`: Interfaces for all services.
  - `WinUI*.cs` or `*.cs`: Implementations (e.g., `IconService`, `WindowService`).
- `Helpers/`: Shared utilities, constants, and value converters.
- `Assets/`: Images, icons, and splash screens.
- `Launchbox.Tests/`: xUnit tests. Uses file-linking to production code to bypass WinUI host requirements.

## Development Conventions

### Coding Style
- **Braces**: Allman style (new line for `{`).
- **Indentation**: 4 spaces.
- **Naming**:
  - Constants: `UPPER_SNAKE_CASE`.
  - Private fields: `_camelCase`.
  - Async methods: `*Async` suffix.
- **Modern C#**: Use file-scoped namespaces, nullable reference types (`?`), and collection expressions `[]`.

### MVVM & Services
- **No DI Container**: Dependencies are wired manually in the `MainWindow` constructor.
- **Commands**: Use `[RelayCommand]` from CommunityToolkit.Mvvm.
- **Properties**: Use `[ObservableProperty]` for boilerplate-free change notification.
- **Platform Logic**: Always place platform-specific or I/O-heavy logic behind an interface in `Services/`.

### Testing Pattern
When adding a new class to be tested:
1. Implement an interface for the class.
2. Add a `<Compile Include="..\ClassName.cs" Link="..." />` entry in `Launchbox.Tests.csproj`.
3. Create a test file in `Launchbox.Tests/` using existing mocks or creating new ones.

### Safety & Reliability
- **Path Security**: Use `PathSecurity.cs` for validating file paths.
- **Error Handling**: Use `System.Diagnostics.Trace.WriteLine(...)` for logging; do not swallow exceptions silently.
- **P/Invoke**: Centralize in `NativeMethods.cs` with `SetLastError = true`.

## Key Files
- `MainWindow.xaml`: Main launcher UI and tray icon definition.
- `Services/WindowService.cs`: Manages hotkeys, window visibility, and positioning.
- `Services/IconService.cs`: Extracts and caches icons from various file types.
- `Helpers/Constants.cs`: Global configuration (hotkeys, dimensions, allowed extensions).
- `CLAUDE.md`: Detailed technical guidance and common tasks.
- `TODO.md`: Current project roadmap and identified issues.
