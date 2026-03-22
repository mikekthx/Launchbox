# Launchbox

Launchbox is a modern, lightweight Windows desktop application launcher built with **WinUI 3** and **.NET 10**. It integrates seamlessly with your system tray, providing instant access to your favorite shortcuts from one or more folders with a global hotkey.

[![Build Status](https://github.com/mikekthx/Launchbox/actions/workflows/dotnet-desktop.yml/badge.svg)](https://github.com/mikekthx/Launchbox/actions/workflows/dotnet-desktop.yml)
[![CodeQL](https://img.shields.io/badge/CodeQL-Passing-brightgreen)](https://github.com/mikekthx/Launchbox/security/code-scanning)
[![Dependabot](https://img.shields.io/badge/Dependabot-Active-blue)](https://github.com/mikekthx/Launchbox/network/dependencies)

## Features

- **⚡ Fast Access**: Instantly toggle the launcher with a configurable global hotkey (default `Alt+S`).
- **🖥️ System Tray Integration**: Runs quietly in the background, accessible via a tray icon.
- **📂 Multi-Folder Sources**: Configure multiple shortcut folders with add, remove, reorder, and rename support in Settings.
- **🗂️ Flexible View Modes**: Display shortcuts merged alphabetically or grouped by source folder, with collapsible groups for better organization.
- **🎨 Modern UI**: Built with WinUI 3 with an acrylic backdrop for a native Windows 11 look and feel.
- **🌍 Localization**: Automatically uses your Windows language, with support for 8 languages (English, Spanish, French, German, Japanese, Simplified Chinese, Korean, and Brazilian Portuguese).
- **🔗 Support for Various Shortcuts**: Handles standard application shortcuts (`.lnk`) and Internet shortcuts (`.url`).
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
