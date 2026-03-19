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
Error_CriticalTitle = "Launchbox"
```

## XAML Localization

Hardcoded strings in XAML are replaced with `x:Uid` references where the element is in the visual tree. The WinUI resource system automatically maps `x:Uid` + property suffix to resource keys.

### Before/After (In-Tree Elements)

```xml
<!-- Before -->
<TextBlock Text="No shortcuts found" />
<TextBox PlaceholderText="Search..." />

<!-- After -->
<TextBlock x:Uid="MainWindow_NoShortcuts" />
<TextBox x:Uid="MainWindow_SearchBox" />
```

### Multi-Property Elements

A single `x:Uid` can set multiple properties:
```
MainWindow_SearchBox.PlaceholderText = "Search..."
MainWindow_SearchBox.AutomationProperties.Name = "Search shortcuts"
```

**ToolTipService attached properties** use the WinUI 3 namespace in `.resw` keys:
```
MainWindow_SearchBox.[using:Microsoft.UI.Xaml.Controls]ToolTipService.ToolTip = "Search shortcuts (Esc to clear)"
```

### Elements That Cannot Use `x:Uid`

The following elements must be localized from code-behind via `ResourceLoader` (not `x:Uid`):

1. **`Window.Title`** — `Window` is not a `FrameworkElement` in WinUI 3, so `x:Uid` does not resolve on it. Set `Title` from code-behind after `InitializeComponent()`.

2. **Tray icon (`TaskbarIcon`)** — Third-party control (H.NotifyIcon.WinUI) that sits outside the normal visual tree. `AutomationProperties.Name` and `ToolTipText` must be set from code-behind.

3. **Tray context flyout items (`MenuFlyoutItem` for Settings, Exit)** — These are out-of-tree elements that use `{Binding}` (documented constraint in CLAUDE.md). Localized text is provided via ViewModel properties bound with `{Binding Text}`, following the existing `ToggleWindowText` pattern.

### Tray Flyout Localization Pattern

The existing `ToggleWindowText` property in `MainViewModel` already demonstrates the pattern for tray flyout strings — it returns `"Hide"` or `"Show"` based on state. For localization:

```csharp
// MainViewModel.cs
// Existing pattern — ToggleWindowText already uses a property:
public string ToggleWindowText => IsWindowVisible
    ? Localization.GetString("TrayMenu_Hide")
    : Localization.GetString("TrayMenu_Show");

// New properties for other tray menu items:
public string SettingsMenuText => Localization.GetString("TrayMenu_Settings");
public string ExitMenuText => Localization.GetString("TrayMenu_Exit");
```

XAML continues using `{Binding}`:
```xml
<MenuFlyoutItem Text="{Binding SettingsMenuText}" />
<MenuFlyoutItem Text="{Binding ExitMenuText}" />
```

## C# Code Localization

### Localization Helper with Testability Seam

```csharp
// Services/IStringProvider.cs
internal interface IStringProvider
{
    string GetString(string key);
}

// Services/ResourceStringProvider.cs
using Microsoft.Windows.ApplicationModel.Resources;

internal sealed class ResourceStringProvider : IStringProvider
{
    private readonly ResourceLoader _loader = new();
    public string GetString(string key) => _loader.GetString(key);
}

// Helpers/Localization.cs
namespace Launchbox.Helpers;

internal static class Localization
{
    private static IStringProvider _provider = new ResourceStringProvider();

    /// <summary>
    /// Replaces the string provider (used by tests to inject a mock).
    /// </summary>
    internal static void SetProvider(IStringProvider provider) => _provider = provider;

    public static string GetString(string key) => _provider.GetString(key);
}
```

- **Production:** `ResourceStringProvider` backed by `ResourceLoader`
- **Tests:** `MockStringProvider` backed by a dictionary returning English strings, injected via `Localization.SetProvider()`

### Dynamic Strings

**Tray tooltip (MainViewModel):**
```csharp
// Resource: Tray_TooltipFormat = "Launchbox ({0})"
var format = Localization.GetString("Tray_TooltipFormat");
TrayToolTipText = string.Format(format, hotkeyDisplay);
```

**ToggleWindowText** uses `Localization.GetString("TrayMenu_Hide")` / `Localization.GetString("TrayMenu_Show")` as shown in the tray flyout section above.

**Modifier key names:** Use OS-provided localized key names via the Windows input system where available, falling back to English.

**Error messages (App.xaml.cs):** The critical error handler (`NativeMethods.MessageBox`) runs during unhandled exceptions when the WinUI rendering thread may be dead. Attempt `ResourceLoader` but fall back to hardcoded English if it throws:
```csharp
string title, message;
try
{
    title = Localization.GetString("Error_CriticalTitle");
    message = Localization.GetString("Error_CriticalMessage");
}
catch
{
    title = "Launchbox";
    message = "Launchbox encountered a critical error and needs to close.";
}
NativeMethods.MessageBox(IntPtr.Zero, message, title, 0x10);
```

### Display vs. Storage Separation

Settings values that are both displayed and persisted must separate the two concerns.

**ComboBox binding pattern:** Use a display model class so `SelectedItem` returns a typed object with both the display name and the storage value:

```csharp
// Helpers/LocalizedOption.cs
internal sealed record LocalizedOption(string Value, string DisplayName)
{
    public override string ToString() => DisplayName;
}
```

**Grid sizes:**
```csharp
// SettingsViewModel.cs
public List<LocalizedOption> GridSizeOptions { get; } =
[
    new("Small", Localization.GetString("GridSize_Small")),
    new("Medium", Localization.GetString("GridSize_Medium")),
    new("Large", Localization.GetString("GridSize_Large")),
];

// On selection change, persist Value (English), display DisplayName (localized)
// SelectedGridSize binding uses SelectedItem, reads .Value for storage
```

**Modifier keys:** Same pattern — `LocalizedOption` with English `Value` for storage, localized `DisplayName` for UI.

Storage keys never change regardless of locale. This ensures settings don't break when switching Windows language.

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

### MainWindow.xaml — In-Tree Elements (x:Uid)

| Key | Property | English Value |
|-----|----------|---------------|
| `MainWindow_NoShortcuts` | `.Text` | No shortcuts found |
| `MainWindow_AddShortcuts` | `.Text` | Add shortcuts to your Desktop/Shortcuts folder |
| `MainWindow_OpenFolder` | `.Content` | Open Shortcuts Folder |
| `MainWindow_OpenFolder` | `.ToolTipService.ToolTip` | Open your configured shortcuts folder |
| `MainWindow_OpenFolder` | `.AutomationProperties.Name` | Open Shortcuts Folder |
| `MainWindow_SearchBox` | `.PlaceholderText` | Search... |
| `MainWindow_SearchBox` | `.ToolTipService.ToolTip` | Search shortcuts (Esc to clear) |
| `MainWindow_SearchBox` | `.AutomationProperties.Name` | Search shortcuts |
| `MainWindow_NoMatches` | `.Text` | No matches |
| `MainWindow_ShortcutsGrid` | `.AutomationProperties.Name` | Shortcuts Grid |

### MainWindow.xaml — Out-of-Tree / Code-Behind Elements

| Key | English Value | Mechanism |
|-----|---------------|-----------|
| `TrayMenu_Settings` | Settings | ViewModel property via `{Binding}` |
| `TrayMenu_Exit` | Exit | ViewModel property via `{Binding}` |
| `TrayMenu_Hide` | Hide | Existing `ToggleWindowText` property |
| `TrayMenu_Show` | Show | Existing `ToggleWindowText` property |
| `Tray_AutomationName` | Launchbox System Tray Icon | Code-behind assignment |

### SettingsWindow.xaml — In-Tree Elements (x:Uid)

| Key | Property | English Value |
|-----|----------|---------------|
| `Settings_TitleText` | `.Text` | Launchbox Settings |
| `Settings_GeneralHeader` | `.Text` | General |
| `Settings_StartupToggle` | `.Header` | Run at Startup |
| `Settings_StartupToggle` | `.ToolTipService.ToolTip` | Automatically launch Launchbox when you sign in to Windows |
| `Settings_StartupToggle` | `.AutomationProperties.Name` | Run at Startup |
| `Settings_ShortcutsHeader` | `.Text` | Shortcuts Folder |
| `Settings_BrowseButton` | `.Content` | Browse... |
| `Settings_BrowseButton` | `.ToolTipService.ToolTip` | Select a new folder for shortcuts |
| `Settings_BrowseButton` | `.AutomationProperties.Name` | Browse for shortcuts folder |
| `Settings_ChangesNote` | `.Text` | Changes will be applied immediately. |
| `Settings_HotkeyHeader` | `.Text` | Global Hotkey |
| `Settings_HotkeyModifier` | `.ToolTipService.ToolTip` | Select a modifier key (e.g., Alt) |
| `Settings_HotkeyModifier` | `.AutomationProperties.Name` | Hotkey modifier |
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

### SettingsWindow.xaml — Code-Behind Elements

| Key | English Value | Mechanism |
|-----|---------------|-----------|
| `SettingsWindow_Title` | Launchbox Settings | Code-behind `Title =` after `InitializeComponent()` |

### Hardcoded Strings NOT Localized

| String | Reason |
|--------|--------|
| `+` (hotkey separator) | Universal keyboard notation, not language-dependent |
| `Launchbox` (brand name) | Brand identity — never translated |

### C# Code Strings

| Key | English Value | Location | Notes |
|-----|---------------|----------|-------|
| `Tray_TooltipFormat` | Launchbox ({0}) | MainViewModel.cs | Format string for tray tooltip |
| `TrayMenu_Hide` | Hide | MainViewModel.cs | Used by `ToggleWindowText` |
| `TrayMenu_Show` | Show | MainViewModel.cs | Used by `ToggleWindowText` |
| `TrayMenu_Settings` | Settings | MainViewModel.cs | New property for tray menu |
| `TrayMenu_Exit` | Exit | MainViewModel.cs | New property for tray menu |
| `Tray_AutomationName` | Launchbox System Tray Icon | MainWindow.xaml.cs | Code-behind assignment |
| `Modifier_Alt` | Alt | SettingsViewModel.cs | Display in `LocalizedOption` |
| `Modifier_Ctrl` | Ctrl | SettingsViewModel.cs | Display in `LocalizedOption` |
| `Modifier_Shift` | Shift | SettingsViewModel.cs | Display in `LocalizedOption` |
| `Modifier_Win` | Win | SettingsViewModel.cs | Display in `LocalizedOption` |
| `GridSize_Small` | Small | SettingsViewModel.cs | Display in `LocalizedOption` |
| `GridSize_Medium` | Medium | SettingsViewModel.cs | Display in `LocalizedOption` |
| `GridSize_Large` | Large | SettingsViewModel.cs | Display in `LocalizedOption` |
| `Error_CriticalMessage` | Launchbox encountered a critical error and needs to close. | App.xaml.cs | With English fallback |
| `Error_CriticalTitle` | Launchbox | App.xaml.cs | With English fallback |
| `Error_NotificationTitle` | Launchbox Error | MainWindow.xaml.cs | Tray notification |

## Out of Scope

- RTL layout support (none of the target languages are RTL)
- Runtime language switching (follows Windows setting, requires app restart)
- Language picker in settings UI
- Translation of "Launchbox" brand name

## Design Decisions

1. **`.resw` + `x:Uid` over custom resource system:** Native WinUI 3 pattern, zero extra dependencies, automatic Windows language matching
2. **`Localization` static helper with `IStringProvider` seam:** Simple access for ~15 C# strings; testable via `SetProvider()` without DI container
3. **AI-generated translations with cross-validation:** Practical for a small string set (~60 keys); Gemini and Codex independently translate, disagreements are resolved by comparison
4. **Display/storage separation via `LocalizedOption`:** Settings persist English keys always; `LocalizedOption` record pairs `Value` (storage) with `DisplayName` (localized UI). Prevents settings corruption on language switch and solves the ComboBox `SelectedItem` binding problem
5. **Tray/flyout strings via ViewModel properties:** Preserves existing `{Binding}` constraint for out-of-tree XAML elements, following the established `ToggleWindowText` pattern
6. **Critical error handler with English fallback:** `ResourceLoader` may not be functional during unhandled exceptions; attempt localized string but catch and fall back to hardcoded English
7. **"+" hotkey separator kept hardcoded:** Universal keyboard notation that does not vary by language
