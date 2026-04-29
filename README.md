# Launchbox

Launchbox is a modern, lightweight Windows desktop application launcher built with **WinUI 3** and **.NET 10**. It integrates seamlessly with your system tray, providing instant access to your favorite shortcuts from one or more folders with a global hotkey.

[![Build](https://img.shields.io/github/actions/workflow/status/mikekthx/Launchbox/dotnet-desktop.yml?style=for-the-badge&label=Build)](https://github.com/mikekthx/Launchbox/actions/workflows/dotnet-desktop.yml)
[![CodeQL](https://img.shields.io/badge/CodeQL-Passing-brightgreen?style=for-the-badge)](https://github.com/mikekthx/Launchbox/security/code-scanning)
[![OpenSSF Scorecard](https://img.shields.io/ossf-scorecard/github.com/mikekthx/Launchbox?style=for-the-badge&label=Scorecard)](https://securityscorecards.dev/viewer/?uri=github.com/mikekthx/Launchbox)
[![.NET](https://img.shields.io/badge/.NET-10.0-512bd4?style=for-the-badge)](https://dotnet.microsoft.com/)
[![WinUI](https://img.shields.io/badge/WinUI-3-0078d4?style=for-the-badge)](https://learn.microsoft.com/en-us/windows/apps/winui/)
[![License](https://img.shields.io/github/license/mikekthx/Launchbox?style=for-the-badge)](https://github.com/mikekthx/Launchbox/blob/main/LICENSE)

## Features

- **⚡ Fast Access**: Instantly toggle the launcher with a configurable global hotkey (default `Alt+S`).
- **🖥️ System Tray Integration**: Runs quietly in the background, accessible via a tray icon.
- **📂 Multi-Folder Sources**: Configure multiple shortcut folders with add, remove, reorder, and rename support in Settings.
- **⌨️ Keyboard Navigation**: Type anywhere to search, Enter to launch the focused shortcut. Drag-and-drop to reorder shortcuts; reordering is disabled while a search filter is active.
- **🗂️ Flexible View Modes**: Display shortcuts merged alphabetically or grouped by source folder, with collapsible groups for better organization.
- **🎨 Modern UI**: Built with WinUI 3 with an acrylic backdrop for a native Windows 11 look and feel.
- **🌍 Localization**: Automatically uses your Windows language, with support for 13 languages (English, Spanish, French, German, Italian, Japanese, Korean, Polish, Brazilian Portuguese, Russian, Turkish, Simplified Chinese, and Traditional Chinese).
- **🔗 Support for Various Shortcuts**: Handles standard application shortcuts (`.lnk`) and Internet shortcuts (`.url`), including web URLs and app-protocol shortcuts for Steam, Discord, Spotify, Xbox, and more.
- **🖼️ Custom Icons**: Override any shortcut's icon by placing a `.png` or `.ico` file in the `.icons` directory alongside your shortcuts.
- **⚙️ Settings**: Dedicated settings window for hotkey, shortcut folders, view mode, startup behavior, and window management.
- **🚀 Run at Startup**: Optionally launch Launchbox on Windows startup via MSIX StartupTask.
- **↔️ Resizable Window**: Freely resize the launcher with minimum size enforcement and work-area clamping. Enable **Keep Centered** to snap the window to the center of your active display each time it opens.
- **📌 Draggable Window**: Reposition the launcher window by dragging; position persists across sessions.

## Installation

Launchbox is distributed as a packaged **MSIX** installer for **x64** and **ARM64** Windows systems.

1.  Download the latest release from the [Releases](https://github.com/mikekthx/Launchbox/releases) page.
2.  Double-click the `.msix` file to install.
3.  Once installed, Launchbox will start automatically.

## Development Setup

To build and run Launchbox locally, follow these steps:

### Prerequisites

*   **Windows 10 (version 1809 or later) or Windows 11**
*   **Visual Studio 2022** (17.8 or later) with the following workloads:
    *   .NET Desktop Development
    *   Universal Windows Platform development (optional, but recommended for WinUI templates)
*   **.NET 10.0 SDK**
*   **Supported architectures:** x64 and ARM64

### Building from Source

1.  **Clone the repository:**
    ```bash
    git clone https://github.com/mikekthx/Launchbox.git
    cd Launchbox
    ```

2.  **Restore dependencies:**
    ```bash
    dotnet restore Launchbox.sln
    ```

3.  **Build the project:**
    Note: You must specify a supported platform (`x64` or `ARM64`) as WinUI 3 does not support `AnyCPU`.
    ```bash
    dotnet build Launchbox.csproj -p:Platform=x64
    ```

4.  **Run the application:**
    ```bash
    dotnet run --project Launchbox.csproj
    ```

## Testing

The project uses **xUnit** for unit testing. Run tests with:

```bash
dotnet test Launchbox.Tests/Launchbox.Tests.csproj
```

## Contributing

I welcome any contributions! Please follow these guidelines:

1.  **Fork** the repository and create a new branch for your feature or bugfix.
2.  Ensure your code adheres to the project's coding standards. We enforce `dotnet format` in our CI pipeline.
    *   Run `dotnet format` locally before committing to catch style issues.
3.  Submit a **Pull Request** with a clear description of your changes.
