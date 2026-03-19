# Localization Design Spec

**Date:** 2026-03-18
**Status:** Approved
**Scope:** Full localization of Launchbox to 8 locales using WinUI 3 native resource system

## Overview

Launchbox currently has ~60 hardcoded English user-facing strings across XAML and C# code with zero localization infrastructure. This spec covers implementing full localization using the WinUI 3 native `.resw` + `x:Uid` pattern, with automatic Windows language detection and AI-generated translations.

## Target Locales

| Locale    | Language              |
|-----------|-----------------------|
| `en-US`   | English (fallback)    |
| `es`      | Spanish               |
| `fr`      | French                |
| `de`      | German                |
| `ja`      | Japanese              |
| `zh-Hans` | Simplified Chinese    |
| `ko`      | Korean                |
| `pt-BR`   | Brazilian Portuguese  |

## Resource File Structure

```
Launchbox/
└── Strings/
    ├── en-US/Resources.resw
    ├── es/Resources.resw
    ├── fr/Resources.resw
    ├── de/Resources.resw
    ├── ja/Resources.resw
    ├── zh-Hans/Resources.resw
    ├── ko/Resources.resw
    └── pt-BR/Resources.resw
```

- `en-US` is the fallback — always complete
- Windows runtime picks the best match from the user's preferred language list automatically
- `.resw` files are XML key-value pairs, one per localizable property

### Resource Key Naming Convention

- XAML-bound strings: `{Scope}_{ElementName}.{Property}`
- Code-accessed strings: `{Scope}_{Description}`

Examples:
```
MainWindow_SearchBox.PlaceholderText = "Search..."
MainWindow_NoShortcuts.Text = "No shortcuts found"
SettingsWindow_Title = "Launchbox Settings"
TrayMenu_Settings.Text = "Settings"
Error_CriticalTitle = "Launchbox"
```

## XAML Localization

All hardcoded strings in XAML get replaced with `x:Uid` references. The WinUI resource system automatically maps `x:Uid` + property suffix to resource keys.

### Before/After

```xml
<!-- Before -->
<MenuFlyoutItem Text="Settings" />
<TextBlock Text="No shortcuts found" />
<TextBox PlaceholderText="Search..." />

<!-- After -->
<MenuFlyoutItem x:Uid="TrayMenu_Settings" />
<TextBlock x:Uid="MainWindow_NoShortcuts" />
<TextBox x:Uid="MainWindow_SearchBox" />
```

### Multi-Property Elements

A single `x:Uid` can set multiple properties:
```
MainWindow_SearchBox.PlaceholderText = "Search..."
MainWindow_SearchBox.[using:Windows.UI.Xaml.Controls]ToolTipService.ToolTip = "Search shortcuts (Esc to clear)"
MainWindow_SearchBox.AutomationProperties.Name = "Search shortcuts"
```

### Tray Icon and Flyout Exception

Tray icon and flyout elements use `{Binding}` due to out-of-tree XAML constraints (documented in CLAUDE.md). These strings will be set from code-behind via `ResourceLoader` instead of `x:Uid`, preserving the existing binding pattern.

## C# Code Localization

### Static Accessor

```csharp
// Helpers/Localization.cs
using Microsoft.Windows.ApplicationModel.Resources;

namespace Launchbox.Helpers;

internal static class Localization
{
    private static readonly ResourceLoader _loader = new();

    public static string GetString(string key) => _loader.GetString(key);
}
```

### Dynamic Strings

**Tray tooltip (MainViewModel):**
```csharp
// Resource: Tray_TooltipFormat = "Launchbox ({0})"
var format = Localization.GetString("Tray_TooltipFormat");
TrayToolTipText = string.Format(format, hotkeyDisplay);
```

**Modifier key names:** Use OS-provided localized key names via the Windows input system where available, falling back to English.

**Error messages (App.xaml.cs, MainWindow.xaml.cs):**
```
Error_CriticalMessage = "Launchbox encountered a critical error and needs to close."
Error_NotificationTitle = "Launchbox Error"
```

### Display vs. Storage Separation

Settings values that are both displayed and persisted must separate the two concerns:

- **Grid sizes:** Store enum value (`"Small"`, `"Medium"`, `"Large"` — always English). Display via `Localization.GetString("GridSize_Small")` etc.
- **Modifier keys:** Store English name (`"Alt"` — always English). Display localized name from OS or resources.

Storage keys never change regardless of locale. This ensures settings don't break when switching Windows language.

## Testability

### IStringProvider Interface

`ResourceLoader` requires WinUI resource infrastructure at runtime, which isn't available in the file-linked test project.

```csharp
// Services/IStringProvider.cs
internal interface IStringProvider
{
    string GetString(string key);
}
```

- **Production:** `ResourceStringProvider` backed by `ResourceLoader`
- **Tests:** `MockStringProvider` backed by a dictionary returning English strings
- `Localization.cs` uses `IStringProvider` internally, defaulting to the `ResourceLoader` implementation

## Project Configuration

### Launchbox.csproj

```xml
<DefaultLanguage>en-US</DefaultLanguage>
```

No extra NuGet packages needed — `ResourceLoader` is part of the Windows App SDK already referenced. The WinUI 3 build system automatically discovers `Strings/{locale}/Resources.resw` folders.

### CI/CD

No changes needed — `dotnet build` and `dotnet test` pick up `.resw` files automatically. `dotnet format` doesn't lint `.resw` XML.

## Translation Generation

### Process

1. Build complete `en-US/Resources.resw` with all ~60 strings
2. Send full English resource file to both Gemini and Codex independently per locale
3. Compare outputs — use agreed translations, pick the better one where they disagree
4. Validate every locale has identical key set to `en-US`

### Translation Context

The AI translation prompt will include:
- App purpose: desktop app launcher, system tray, hotkey-driven
- "Launchbox" is a brand name — never translate
- Modifier key names should use locale-conventional keyboard terminology
- Max string length hints for space-constrained UI elements (buttons, menus)

## String Inventory

### MainWindow.xaml (~16 strings)

| Key | Property | English Value |
|-----|----------|---------------|
| `TrayMenu_Settings` | `.Text` | Settings |
| `TrayMenu_Exit` | `.Text` | Exit |
| `MainWindow_NoShortcuts` | `.Text` | No shortcuts found |
| `MainWindow_AddShortcuts` | `.Text` | Add shortcuts to your Desktop/Shortcuts folder |
| `MainWindow_OpenFolder` | `.Content` | Open Shortcuts Folder |
| `MainWindow_OpenFolder` | `.ToolTipService.ToolTip` | Open your configured shortcuts folder |
| `MainWindow_OpenFolder` | `.AutomationProperties.Name` | Open Shortcuts Folder |
| `MainWindow_SearchBox` | `.PlaceholderText` | Search... |
| `MainWindow_SearchBox` | `.ToolTipService.ToolTip` | Search shortcuts (Esc to clear) |
| `MainWindow_SearchBox` | `.AutomationProperties.Name` | Search shortcuts |
| `MainWindow_NoMatches` | `.Text` | No matches |
| `MainWindow_TrayIcon` | `.AutomationProperties.Name` | Launchbox System Tray Icon |
| `MainWindow_ShortcutsGrid` | `.AutomationProperties.Name` | Shortcuts Grid |
| `TrayMenu_Settings` | `.AutomationProperties.Name` | Settings |
| `TrayMenu_Exit` | `.AutomationProperties.Name` | Exit |

### SettingsWindow.xaml (~25 strings)

| Key | Property | English Value |
|-----|----------|---------------|
| `SettingsWindow` | `Title` | Launchbox Settings |
| `Settings_TitleText` | `.Text` | Launchbox Settings |
| `Settings_GeneralHeader` | `.Text` | General |
| `Settings_StartupToggle` | `.Header` | Run at Startup |
| `Settings_StartupToggle` | `.ToolTipService.ToolTip` | Automatically launch Launchbox when you sign in to Windows |
| `Settings_StartupToggle` | `.AutomationProperties.Name` | Run at Startup |
| `Settings_ShortcutsHeader` | `.Text` | Shortcuts Folder |
| `Settings_BrowseButton` | `.Content` | Browse... |
| `Settings_BrowseButton` | `.ToolTipService.ToolTip` | Select a new folder for shortcuts |
| `Settings_ChangesNote` | `.Text` | Changes will be applied immediately. |
| `Settings_HotkeyHeader` | `.Text` | Global Hotkey |
| `Settings_HotkeyModifier` | `.ToolTipService.ToolTip` | Select a modifier key (e.g., Alt) |
| `Settings_HotkeyModifier` | `.AutomationProperties.Name` | Hotkey modifier |
| `Settings_HotkeySeparator` | `.Text` | + |
| `Settings_HotkeyKey` | `.PlaceholderText` | Key |
| `Settings_HotkeyKey` | `.ToolTipService.ToolTip` | Enter a key (e.g., S, F1, Home) |
| `Settings_HotkeyKey` | `.AutomationProperties.Name` | Hotkey key |
| `Settings_AppearanceHeader` | `.Text` | Appearance |
| `Settings_IconSizeLabel` | `.Text` | Icon size |
| `Settings_IconSizeCombo` | `.ToolTipService.ToolTip` | Set the size of icons in the launcher grid |
| `Settings_IconSizeCombo` | `.AutomationProperties.Name` | Icon size |
| `Settings_WindowHeader` | `.Text` | Window Management |
| `Settings_ResetPosition` | `.Content` | Reset Window Position |
| `Settings_ResetPosition` | `.ToolTipService.ToolTip` | Moves the main window to the center of the screen |
| `Settings_ResetPosition` | `.AutomationProperties.Name` | Reset Window Position |
| `Settings_ShortcutsPath` | `.AutomationProperties.Name` | Shortcuts folder path |

### C# Code Strings (~15 keys)

| Key | English Value | Location |
|-----|---------------|----------|
| `Tray_TooltipFormat` | Launchbox ({0}) | MainViewModel.cs |
| `TrayMenu_Hide` | Hide | MainViewModel.cs |
| `TrayMenu_Show` | Show | MainViewModel.cs |
| `Modifier_Alt` | Alt | SettingsViewModel.cs |
| `Modifier_Ctrl` | Ctrl | SettingsViewModel.cs |
| `Modifier_Shift` | Shift | SettingsViewModel.cs |
| `Modifier_Win` | Win | SettingsViewModel.cs |
| `GridSize_Small` | Small | SettingsViewModel.cs |
| `GridSize_Medium` | Medium | SettingsViewModel.cs |
| `GridSize_Large` | Large | SettingsViewModel.cs |
| `Error_CriticalMessage` | Launchbox encountered a critical error and needs to close. | App.xaml.cs |
| `Error_CriticalTitle` | Launchbox | App.xaml.cs |
| `Error_NotificationTitle` | Launchbox Error | MainWindow.xaml.cs |

## Out of Scope

- RTL layout support (none of the target languages are RTL)
- Runtime language switching (follows Windows setting, requires app restart)
- Language picker in settings UI
- Translation of "Launchbox" brand name

## Design Decisions

1. **`.resw` + `x:Uid` over custom resource system:** Native WinUI 3 pattern, zero extra dependencies, automatic Windows language matching
2. **Static `Localization` helper over DI-injected resource loader:** Keeps resource access simple for the ~15 C# strings; testability handled via `IStringProvider` seam
3. **AI-generated translations with cross-validation:** Practical for a small string set (~60 keys); Gemini and Codex independently translate, disagreements are resolved by comparison
4. **Display/storage separation:** Settings persist English keys always; display values are localized. Prevents settings corruption on language switch
5. **Tray/flyout strings via code-behind:** Preserves existing `{Binding}` constraint for out-of-tree XAML elements
