# Launchbox

Launchbox is a Windows desktop application launcher built with WinUI 3 and .NET 8. It provides a quick and convenient way to access your shortcuts from the system tray.

## Features

- **System Tray Integration**: Runs quietly in the background.
- **Global Hotkey**: Toggle the launcher visibility with `Alt+S`.
- **Shortcut Management**: Displays shortcuts from your `Desktop\Shortcuts` folder.
- **Customizable**: Built with modern Windows UI principles.

## Prerequisites

- Windows 10 or 11 (x64, ARM64)
- .NET 8.0 SDK
- Windows App SDK 1.8 Runtime

## Getting Started

1.  **Clone the repository:**
    ```bash
    git clone https://github.com/yourusername/Launchbox.git
    cd Launchbox
    ```

2.  **Build the project:**
    ```bash
    dotnet build Launchbox.csproj -p:Platform=x64
    ```

3.  **Run the application:**
    ```bash
    dotnet run --project Launchbox.csproj
    ```

## Usage

1.  Create a folder named `Shortcuts` on your Desktop.
2.  Add your application shortcuts (`.lnk`) or internet shortcuts (`.url`) to this folder.
3.  Run the application.
4.  Press `Alt+S` to toggle the launcher visibility.
5.  The launcher icon will appear in the system tray.

## Project Structure

- `Launchbox/`: Main application project.
- `Launchbox.Tests/`: Unit tests.
- `Assets/`: Application icons and assets.
- `Services/`: Core services and logic.
- `ViewModels/`: MVVM ViewModels.
