# Localization Implementation Plan

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development (if subagents available) or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Localize all ~60 user-facing strings into 8 locales using WinUI 3 native `.resw` + `x:Uid`, with automatic Windows language detection.

**Architecture:** English `.resw` resource files provide the fallback. XAML elements use `x:Uid` for in-tree strings; out-of-tree elements (tray icon, flyout) use ViewModel properties backed by `ResourceLoader`. A static `Localization` helper with `IStringProvider` seam enables testability. Settings store English keys; display uses localized `LocalizedOption` records.

**Tech Stack:** WinUI 3 `.resw` resources, `ResourceLoader` (Windows App SDK), `x:Uid` XAML attributes, xUnit tests.

**Spec:** `docs/superpowers/specs/2026-03-18-localization-design.md`

---

## File Map

### New Files

| File | Responsibility |
|------|---------------|
| `Services/IStringProvider.cs` | Interface for string resource access |
| `Services/ResourceStringProvider.cs` | Production `ResourceLoader`-backed implementation |
| `Helpers/Localization.cs` | Static accessor with swappable `IStringProvider` |
| `Helpers/LocalizedOption.cs` | Record pairing English storage key with localized display name |
| `Strings/en-US/Resources.resw` | English resource file (fallback, ~60 keys) |
| `Strings/es/Resources.resw` | Spanish translations |
| `Strings/fr/Resources.resw` | French translations |
| `Strings/de/Resources.resw` | German translations |
| `Strings/ja/Resources.resw` | Japanese translations |
| `Strings/zh-Hans/Resources.resw` | Simplified Chinese translations |
| `Strings/ko/Resources.resw` | Korean translations |
| `Strings/pt-BR/Resources.resw` | Brazilian Portuguese translations |
| `Launchbox.Tests/MockStringProvider.cs` | Test mock for `IStringProvider` |
| `Launchbox.Tests/LocalizationTests.cs` | Tests for `Localization` helper |
| `Launchbox.Tests/LocalizedOptionTests.cs` | Tests for `LocalizedOption` record |

### Modified Files

| File | Changes |
|------|---------|
| `Launchbox.csproj` | Add `<DefaultLanguage>en-US</DefaultLanguage>` |
| `MainWindow.xaml` | Replace hardcoded strings with `x:Uid`; bind tray flyout text to ViewModel properties |
| `MainWindow.xaml.cs` | Set tray icon automation name and notification title from `Localization` |
| `SettingsWindow.xaml` | Replace hardcoded strings with `x:Uid` |
| `SettingsWindow.xaml.cs` | Set `Title` from `Localization` in code-behind |
| `ViewModels/MainViewModel.cs` | Add `SettingsMenuText`, `ExitMenuText` properties; localize `ToggleWindowText`, `TrayToolTipText` |
| `ViewModels/SettingsViewModel.cs` | Switch `Modifiers` and `GridSizeOptions` to `LocalizedOption`; update `SelectedModifier`/`SelectedGridSize` |
| `App.xaml.cs` | Localize critical error strings with English fallback |
| `Launchbox.Tests/Launchbox.Tests.csproj` | Add `<Compile Include>` entries for new files |
| `Launchbox.Tests/MainViewModelTests.cs` | Add setup for `MockStringProvider`; add tests for localized properties |
| `Launchbox.Tests/SettingsViewModelTests.cs` | Add setup for `MockStringProvider`; update tests for `LocalizedOption` binding |

---

## Task 1: IStringProvider Interface and Localization Helper

**Files:**
- Create: `Services/IStringProvider.cs`
- Create: `Helpers/Localization.cs`
- Create: `Launchbox.Tests/MockStringProvider.cs`
- Create: `Launchbox.Tests/LocalizationTests.cs`
- Modify: `Launchbox.Tests/Launchbox.Tests.csproj`

- [ ] **Step 1: Write failing tests for Localization helper**

Create `Launchbox.Tests/LocalizationTests.cs`:

```csharp
using Launchbox.Helpers;
using Launchbox.Services;
using Xunit;

namespace Launchbox.Tests;

[Collection("Localization")]
public class LocalizationTests
{
    [Fact]
    public void GetString_ReturnsValueFromProvider()
    {
        var mock = new MockStringProvider(new()
        {
            { "TestKey", "TestValue" }
        });
        Localization.SetProvider(mock);

        var result = Localization.GetString("TestKey");

        Assert.Equal("TestValue", result);
    }

    [Fact]
    public void SetProvider_ReplacesActiveProvider()
    {
        var first = new MockStringProvider(new() { { "Key", "First" } });
        var second = new MockStringProvider(new() { { "Key", "Second" } });

        Localization.SetProvider(first);
        Assert.Equal("First", Localization.GetString("Key"));

        Localization.SetProvider(second);
        Assert.Equal("Second", Localization.GetString("Key"));
    }
}
```

Create `Launchbox.Tests/MockStringProvider.cs`:

```csharp
using Launchbox.Services;
using System.Collections.Generic;

namespace Launchbox.Tests;

public class MockStringProvider : IStringProvider
{
    private readonly Dictionary<string, string> _strings;

    public MockStringProvider(Dictionary<string, string> strings)
    {
        _strings = strings;
    }

    public string GetString(string key) =>
        _strings.TryGetValue(key, out var value) ? value : key;
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test Launchbox.Tests/Launchbox.Tests.csproj --filter "FullyQualifiedName~LocalizationTests" -v n`
Expected: FAIL — `IStringProvider` and `Localization` don't exist yet.

- [ ] **Step 3: Create IStringProvider interface**

Create `Services/IStringProvider.cs`:

```csharp
namespace Launchbox.Services;

internal interface IStringProvider
{
    string GetString(string key);
}
```

- [ ] **Step 4: Create Localization static helper**

Create `Helpers/Localization.cs`:

```csharp
using Launchbox.Services;

namespace Launchbox.Helpers;

internal static class Localization
{
    private static IStringProvider _provider = new DefaultStringProvider();

    internal static void SetProvider(IStringProvider provider) => _provider = provider;

    public static string GetString(string key) => _provider.GetString(key);

    // Temporary default that returns the key itself; replaced by ResourceStringProvider
    // once the .resw files exist (Task 3).
    private sealed class DefaultStringProvider : IStringProvider
    {
        public string GetString(string key) => key;
    }
}
```

- [ ] **Step 5: Add file links to test project**

Add to `Launchbox.Tests/Launchbox.Tests.csproj` inside the existing `<ItemGroup>` with `<Compile Include>` entries:

```xml
<Compile Include="..\Services\IStringProvider.cs" Link="Services\IStringProvider.cs" />
<Compile Include="..\Helpers\Localization.cs" Link="Helpers\Localization.cs" />
```

- [ ] **Step 6: Run tests to verify they pass**

Run: `dotnet test Launchbox.Tests/Launchbox.Tests.csproj --filter "FullyQualifiedName~LocalizationTests" -v n`
Expected: PASS — both tests green.

- [ ] **Step 7: Commit**

```bash
git add Services/IStringProvider.cs Helpers/Localization.cs Launchbox.Tests/MockStringProvider.cs Launchbox.Tests/LocalizationTests.cs Launchbox.Tests/Launchbox.Tests.csproj
git commit -m "feat: add IStringProvider interface and Localization helper with tests"
```

---

## Task 2: LocalizedOption Record

**Files:**
- Create: `Helpers/LocalizedOption.cs`
- Create: `Launchbox.Tests/LocalizedOptionTests.cs`
- Modify: `Launchbox.Tests/Launchbox.Tests.csproj`

- [ ] **Step 1: Write failing tests for LocalizedOption**

Create `Launchbox.Tests/LocalizedOptionTests.cs`:

```csharp
using Launchbox.Helpers;
using Xunit;

namespace Launchbox.Tests;

public class LocalizedOptionTests
{
    [Fact]
    public void ToString_ReturnsDisplayName()
    {
        var option = new LocalizedOption("Small", "Pequeño");

        Assert.Equal("Pequeño", option.ToString());
    }

    [Fact]
    public void Value_StoresStorageKey()
    {
        var option = new LocalizedOption("Alt", "Alternativa");

        Assert.Equal("Alt", option.Value);
        Assert.Equal("Alternativa", option.DisplayName);
    }

    [Fact]
    public void Equality_BasedOnValueAndDisplayName()
    {
        var a = new LocalizedOption("Small", "Small");
        var b = new LocalizedOption("Small", "Small");

        Assert.Equal(a, b);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test Launchbox.Tests/Launchbox.Tests.csproj --filter "FullyQualifiedName~LocalizedOptionTests" -v n`
Expected: FAIL — `LocalizedOption` doesn't exist yet.

- [ ] **Step 3: Create LocalizedOption record**

Create `Helpers/LocalizedOption.cs`:

```csharp
namespace Launchbox.Helpers;

internal sealed record LocalizedOption(string Value, string DisplayName)
{
    public override string ToString() => DisplayName;
}
```

- [ ] **Step 4: Add file link to test project**

Add to `Launchbox.Tests/Launchbox.Tests.csproj`:

```xml
<Compile Include="..\Helpers\LocalizedOption.cs" Link="Helpers\LocalizedOption.cs" />
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test Launchbox.Tests/Launchbox.Tests.csproj --filter "FullyQualifiedName~LocalizedOptionTests" -v n`
Expected: PASS — all three tests green.

- [ ] **Step 6: Commit**

```bash
git add Helpers/LocalizedOption.cs Launchbox.Tests/LocalizedOptionTests.cs Launchbox.Tests/Launchbox.Tests.csproj
git commit -m "feat: add LocalizedOption record for display/storage separation"
```

---

## Task 3: English Resource File and Project Configuration

**Files:**
- Create: `Strings/en-US/Resources.resw`
- Create: `Services/ResourceStringProvider.cs`
- Modify: `Launchbox.csproj`
- Modify: `Helpers/Localization.cs`

- [ ] **Step 1: Add DefaultLanguage to project file**

In `Launchbox.csproj`, add inside the first `<PropertyGroup>` (after line 13):

```xml
<DefaultLanguage>en-US</DefaultLanguage>
```

- [ ] **Step 2: Create English resource file**

Create `Strings/en-US/Resources.resw` with all resource keys. This is a standard `.resw` XML file:

```xml
<?xml version="1.0" encoding="utf-8"?>
<root>
  <xsd:schema id="root" xmlns="" xmlns:xsd="http://www.w3.org/2001/XMLSchema" xmlns:msdata="urn:schemas-microsoft-com:xml-msdata">
    <xsd:element name="root" msdata:IsDataSet="true">
      <xsd:complexType>
        <xsd:choice maxOccurs="unbounded">
          <xsd:element name="data">
            <xsd:complexType>
              <xsd:sequence>
                <xsd:element name="value" type="xsd:string" minOccurs="0" msdata:Ordinal="1" />
                <xsd:element name="comment" type="xsd:string" minOccurs="0" msdata:Ordinal="2" />
              </xsd:sequence>
              <xsd:attribute name="name" type="xsd:string" use="required" />
              <xsd:attribute name="type" type="xsd:string" />
              <xsd:attribute name="mimetype" type="xsd:string" />
            </xsd:complexType>
          </xsd:element>
          <xsd:element name="resheader">
            <xsd:complexType>
              <xsd:sequence>
                <xsd:element name="value" type="xsd:string" minOccurs="0" msdata:Ordinal="1" />
              </xsd:sequence>
              <xsd:attribute name="name" type="xsd:string" use="required" />
            </xsd:complexType>
          </xsd:element>
        </xsd:choice>
      </xsd:complexType>
    </xsd:element>
  </xsd:schema>
  <resheader name="resmimetype">
    <value>text/microsoft-resx</value>
  </resheader>
  <resheader name="version">
    <value>1.3</value>
  </resheader>
  <resheader name="reader">
    <value>System.Resources.ResXResourceReader, System.Windows.Forms, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089</value>
  </resheader>
  <resheader name="writer">
    <value>System.Resources.ResXResourceWriter, System.Windows.Forms, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089</value>
  </resheader>

  <!-- MainWindow.xaml — In-Tree Elements -->
  <data name="MainWindow_NoShortcuts.Text" xml:space="preserve">
    <value>No shortcuts found</value>
  </data>
  <data name="MainWindow_AddShortcuts.Text" xml:space="preserve">
    <value>Add shortcuts to your Desktop/Shortcuts folder</value>
  </data>
  <data name="MainWindow_OpenFolder.[using:Microsoft.UI.Xaml.Controls]ToolTipService.ToolTip" xml:space="preserve">
    <value>Open your configured shortcuts folder</value>
  </data>
  <data name="MainWindow_OpenFolder.AutomationProperties.Name" xml:space="preserve">
    <value>Open Shortcuts Folder</value>
  </data>
  <data name="MainWindow_OpenFolderText.Text" xml:space="preserve">
    <value>Open Shortcuts Folder</value>
  </data>
  <data name="MainWindow_SearchBox.PlaceholderText" xml:space="preserve">
    <value>Search...</value>
  </data>
  <data name="MainWindow_SearchBox.[using:Microsoft.UI.Xaml.Controls]ToolTipService.ToolTip" xml:space="preserve">
    <value>Search shortcuts (Esc to clear)</value>
  </data>
  <data name="MainWindow_SearchBox.AutomationProperties.Name" xml:space="preserve">
    <value>Search shortcuts</value>
  </data>
  <data name="MainWindow_NoMatches.Text" xml:space="preserve">
    <value>No matches</value>
  </data>
  <data name="MainWindow_ShortcutsGrid.AutomationProperties.Name" xml:space="preserve">
    <value>Shortcuts Grid</value>
  </data>

  <!-- SettingsWindow.xaml — In-Tree Elements -->
  <data name="Settings_TitleText.Text" xml:space="preserve">
    <value>Launchbox Settings</value>
  </data>
  <data name="Settings_GeneralHeader.Text" xml:space="preserve">
    <value>General</value>
  </data>
  <data name="Settings_StartupToggle.Header" xml:space="preserve">
    <value>Run at Startup</value>
  </data>
  <data name="Settings_StartupToggle.[using:Microsoft.UI.Xaml.Controls]ToolTipService.ToolTip" xml:space="preserve">
    <value>Automatically launch Launchbox when you sign in to Windows</value>
  </data>
  <data name="Settings_StartupToggle.AutomationProperties.Name" xml:space="preserve">
    <value>Run at Startup</value>
  </data>
  <data name="Settings_ShortcutsHeader.Text" xml:space="preserve">
    <value>Shortcuts Folder</value>
  </data>
  <data name="Settings_BrowseButton.Content" xml:space="preserve">
    <value>Browse...</value>
  </data>
  <data name="Settings_BrowseButton.[using:Microsoft.UI.Xaml.Controls]ToolTipService.ToolTip" xml:space="preserve">
    <value>Select a new folder for shortcuts</value>
  </data>
  <data name="Settings_BrowseButton.AutomationProperties.Name" xml:space="preserve">
    <value>Browse for shortcuts folder</value>
  </data>
  <data name="Settings_ChangesNote.Text" xml:space="preserve">
    <value>Changes will be applied immediately.</value>
  </data>
  <data name="Settings_HotkeyHeader.Text" xml:space="preserve">
    <value>Global Hotkey</value>
  </data>
  <data name="Settings_HotkeyModifier.[using:Microsoft.UI.Xaml.Controls]ToolTipService.ToolTip" xml:space="preserve">
    <value>Select a modifier key (e.g., Alt)</value>
  </data>
  <data name="Settings_HotkeyModifier.AutomationProperties.Name" xml:space="preserve">
    <value>Hotkey modifier</value>
  </data>
  <data name="Settings_HotkeyKey.PlaceholderText" xml:space="preserve">
    <value>Key</value>
  </data>
  <data name="Settings_HotkeyKey.[using:Microsoft.UI.Xaml.Controls]ToolTipService.ToolTip" xml:space="preserve">
    <value>Enter a key (e.g., S, F1, Home)</value>
  </data>
  <data name="Settings_HotkeyKey.AutomationProperties.Name" xml:space="preserve">
    <value>Hotkey key</value>
  </data>
  <data name="Settings_AppearanceHeader.Text" xml:space="preserve">
    <value>Appearance</value>
  </data>
  <data name="Settings_IconSizeLabel.Text" xml:space="preserve">
    <value>Icon size</value>
  </data>
  <data name="Settings_IconSizeCombo.[using:Microsoft.UI.Xaml.Controls]ToolTipService.ToolTip" xml:space="preserve">
    <value>Set the size of icons in the launcher grid</value>
  </data>
  <data name="Settings_IconSizeCombo.AutomationProperties.Name" xml:space="preserve">
    <value>Icon size</value>
  </data>
  <data name="Settings_WindowHeader.Text" xml:space="preserve">
    <value>Window Management</value>
  </data>
  <data name="Settings_ResetPosition.Content" xml:space="preserve">
    <value>Reset Window Position</value>
  </data>
  <data name="Settings_ResetPosition.[using:Microsoft.UI.Xaml.Controls]ToolTipService.ToolTip" xml:space="preserve">
    <value>Moves the main window to the center of the screen</value>
  </data>
  <data name="Settings_ResetPosition.AutomationProperties.Name" xml:space="preserve">
    <value>Reset Window Position</value>
  </data>
  <data name="Settings_ShortcutsPath.AutomationProperties.Name" xml:space="preserve">
    <value>Shortcuts folder path</value>
  </data>

  <!-- C# Code Strings -->
  <data name="Tray_TooltipFormat" xml:space="preserve">
    <value>Launchbox ({0})</value>
    <comment>{0} = hotkey combination like "Alt+S"</comment>
  </data>
  <data name="TrayMenu_Hide" xml:space="preserve">
    <value>Hide</value>
  </data>
  <data name="TrayMenu_Show" xml:space="preserve">
    <value>Show</value>
  </data>
  <data name="TrayMenu_Settings" xml:space="preserve">
    <value>Settings</value>
  </data>
  <data name="TrayMenu_Exit" xml:space="preserve">
    <value>Exit</value>
  </data>
  <data name="Tray_AutomationName" xml:space="preserve">
    <value>Launchbox System Tray Icon</value>
  </data>
  <data name="Modifier_Alt" xml:space="preserve">
    <value>Alt</value>
  </data>
  <data name="Modifier_Ctrl" xml:space="preserve">
    <value>Ctrl</value>
  </data>
  <data name="Modifier_Shift" xml:space="preserve">
    <value>Shift</value>
  </data>
  <data name="Modifier_Win" xml:space="preserve">
    <value>Win</value>
  </data>
  <data name="GridSize_Small" xml:space="preserve">
    <value>Small</value>
  </data>
  <data name="GridSize_Medium" xml:space="preserve">
    <value>Medium</value>
  </data>
  <data name="GridSize_Large" xml:space="preserve">
    <value>Large</value>
  </data>
  <data name="Error_CriticalMessage" xml:space="preserve">
    <value>Launchbox encountered a critical error and needs to close.</value>
  </data>
  <data name="Error_CriticalTitle" xml:space="preserve">
    <value>Launchbox</value>
  </data>
  <data name="Error_NotificationTitle" xml:space="preserve">
    <value>Launchbox Error</value>
  </data>
  <data name="SettingsWindow_Title" xml:space="preserve">
    <value>Launchbox Settings</value>
  </data>
</root>
```

- [ ] **Step 3: Create ResourceStringProvider**

Create `Services/ResourceStringProvider.cs`:

```csharp
using Microsoft.Windows.ApplicationModel.Resources;

namespace Launchbox.Services;

internal sealed class ResourceStringProvider : IStringProvider
{
    private readonly ResourceLoader _loader = new();

    public string GetString(string key) => _loader.GetString(key);
}
```

- [ ] **Step 4: Keep DefaultStringProvider as static default in Localization.cs**

Do NOT change `Localization.cs` to default to `ResourceStringProvider`. The `ResourceLoader` constructor throws `COMException` (0x80073D54: "The process has no package identity") when called from an unpackaged test runner. Since the test project file-links `Localization.cs`, the static field initializer runs in the test context and would cause a fatal `TypeInitializationException`.

Instead, keep `Localization.cs` exactly as created in Task 1 (with `DefaultStringProvider` that returns the key itself). The production app will initialize the real provider explicitly at startup.

- [ ] **Step 5: Initialize ResourceStringProvider in App.xaml.cs**

In `App.xaml.cs`, add to the `App()` constructor (after `this.InitializeComponent();`):

```csharp
Localization.SetProvider(new ResourceStringProvider());
```

Add these imports:
```csharp
using Launchbox.Helpers;
using Launchbox.Services;
```

This ensures the production app uses `ResourceLoader` while tests use `DefaultStringProvider` (or `MockStringProvider` when explicitly set).

- [ ] **Step 6: Build the app to verify resource file is picked up**

Run: `dotnet build Launchbox.csproj -p:Platform=x64`
Expected: Build succeeds with no errors.

- [ ] **Step 7: Run existing tests (they should still pass)**

Run: `dotnet test Launchbox.Tests/Launchbox.Tests.csproj -v n`
Expected: All existing tests pass. `LocalizationTests` still pass because they inject `MockStringProvider`.

- [ ] **Step 8: Commit**

```bash
git add Strings/en-US/Resources.resw Services/ResourceStringProvider.cs App.xaml.cs Launchbox.csproj
git commit -m "feat: add English resource file and ResourceStringProvider"
```

---

## Task 4: Localize MainViewModel (C# Code Strings)

**Files:**
- Modify: `ViewModels/MainViewModel.cs`
- Modify: `Launchbox.Tests/MainViewModelTests.cs`

- [ ] **Step 1: Write failing tests for localized MainViewModel properties**

Add to `Launchbox.Tests/MainViewModelTests.cs`. First, add `[Collection("Localization")]` attribute to the class (this prevents parallel execution with other test classes that mutate the static `Localization` provider):

```csharp
[Collection("Localization")]
public class MainViewModelTests
```

Then add a setup call in the constructor to inject `MockStringProvider`:

```csharp
// Add to constructor, after existing setup:
Localization.SetProvider(new MockStringProvider(new()
{
    { "TrayMenu_Hide", "Hide" },
    { "TrayMenu_Show", "Show" },
    { "TrayMenu_Settings", "Settings" },
    { "TrayMenu_Exit", "Exit" },
    { "Tray_TooltipFormat", "Launchbox ({0})" },
    { "Tray_AutomationName", "Launchbox System Tray Icon" },
    { "Error_NotificationTitle", "Launchbox Error" },
    { "Modifier_Alt", "Alt" },
    { "Modifier_Ctrl", "Ctrl" },
    { "Modifier_Shift", "Shift" },
    { "Modifier_Win", "Win" },
}));
```

Add new test methods:

```csharp
[Fact]
public void ToggleWindowText_ReturnsLocalizedHide_WhenVisible()
{
    var vm = CreateViewModel();
    _windowService.RaiseVisibilityChanged(true);

    Assert.Equal("Hide", vm.ToggleWindowText);
}

[Fact]
public void ToggleWindowText_ReturnsLocalizedShow_WhenHidden()
{
    var vm = CreateViewModel();
    _windowService.RaiseVisibilityChanged(false);

    Assert.Equal("Show", vm.ToggleWindowText);
}

[Fact]
public void SettingsMenuText_ReturnsLocalizedString()
{
    var vm = CreateViewModel();
    Assert.Equal("Settings", vm.SettingsMenuText);
}

[Fact]
public void ExitMenuText_ReturnsLocalizedString()
{
    var vm = CreateViewModel();
    Assert.Equal("Exit", vm.ExitMenuText);
}

[Fact]
public void TrayToolTipText_UsesLocalizedFormat()
{
    var vm = CreateViewModel();
    // Default hotkey is Alt+S
    Assert.StartsWith("Launchbox (", vm.TrayToolTipText);
    Assert.EndsWith(")", vm.TrayToolTipText);
}
```

- [ ] **Step 2: Run tests to verify new tests fail**

Run: `dotnet test Launchbox.Tests/Launchbox.Tests.csproj --filter "FullyQualifiedName~MainViewModelTests" -v n`
Expected: New tests for `SettingsMenuText` and `ExitMenuText` fail (properties don't exist yet). Existing tests should still pass.

- [ ] **Step 3: Update MainViewModel with localized properties**

In `ViewModels/MainViewModel.cs`:

1. Change `ToggleWindowText` (line 85):
```csharp
public string ToggleWindowText => _windowService.IsVisible
    ? Localization.GetString("TrayMenu_Hide")
    : Localization.GetString("TrayMenu_Show");
```

2. Add new properties after `ToggleWindowText`:
```csharp
public string SettingsMenuText => Localization.GetString("TrayMenu_Settings");

public string ExitMenuText => Localization.GetString("TrayMenu_Exit");
```

3. Change `TrayToolTipText` getter (lines 87-108). Replace the hardcoded format string `$"Launchbox ({string.Join("+", parts)})"` with:
```csharp
return string.Format(Localization.GetString("Tray_TooltipFormat"), string.Join("+", parts));
```

4. Localize modifier key names in `TrayToolTipText`. Replace the hardcoded `parts.Add("Ctrl")` etc. with:
```csharp
if ((mod & Constants.MOD_CONTROL) != 0) parts.Add(Localization.GetString("Modifier_Ctrl"));
if ((mod & Constants.MOD_ALT) != 0) parts.Add(Localization.GetString("Modifier_Alt"));
if ((mod & Constants.MOD_SHIFT) != 0) parts.Add(Localization.GetString("Modifier_Shift"));
if ((mod & Constants.MOD_WIN) != 0) parts.Add(Localization.GetString("Modifier_Win"));
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test Launchbox.Tests/Launchbox.Tests.csproj --filter "FullyQualifiedName~MainViewModelTests" -v n`
Expected: All tests pass, including the new localization tests.

- [ ] **Step 5: Commit**

```bash
git add ViewModels/MainViewModel.cs Launchbox.Tests/MainViewModelTests.cs
git commit -m "feat: localize MainViewModel tray menu and tooltip strings"
```

---

## Task 5: Localize SettingsViewModel with LocalizedOption

**Files:**
- Modify: `ViewModels/SettingsViewModel.cs`
- Modify: `Launchbox.Tests/SettingsViewModelTests.cs`

- [ ] **Step 1: Write failing tests for localized SettingsViewModel**

Add to `Launchbox.Tests/SettingsViewModelTests.cs`. First, add `[Collection("Localization")]` attribute to the class:

```csharp
[Collection("Localization")]
public class SettingsViewModelTests
```

Add `MockStringProvider` setup at the top of the `CreateViewModel` helper (insert before the existing `var settingsStore` line):

```csharp
private (SettingsService, MockStartupService, MockFilePickerService, SettingsViewModel) CreateViewModel()
{
    Localization.SetProvider(new MockStringProvider(new()
    {
        { "Modifier_Alt", "Alt" },
        { "Modifier_Ctrl", "Ctrl" },
        { "Modifier_Shift", "Shift" },
        { "Modifier_Win", "Win" },
        { "GridSize_Small", "Small" },
        { "GridSize_Medium", "Medium" },
        { "GridSize_Large", "Large" },
    }));

    var settingsStore = new MockSettingsStore();
    // ... rest unchanged
}
```

**Important:** Also add `Localization.SetProvider(...)` at the top of existing tests that create `SettingsViewModel` directly without calling `CreateViewModel()` — specifically `SelectedGridSize_Default_IsMedium` (line 165), `SelectedGridSize_WhenSet_UpdatesSettingsService` (line 174), `SelectedGridSize_WhenServiceChanges_RaisesPropertyChanged` (line 184), and `GridSizeOptions_ContainsThreeEntries` (line 198).

Add new tests:

```csharp
[Fact]
public void GridSizeOptions_AreLocalizedOptions()
{
    var (_, _, _, vm) = CreateViewModel();

    Assert.Equal("Small", vm.GridSizeOptions[0].Value);
    Assert.Equal("Small", vm.GridSizeOptions[0].DisplayName);
}

[Fact]
public void SelectedGridSize_PersistsEnglishValue()
{
    var (service, _, _, vm) = CreateViewModel();

    vm.SelectedGridSize = vm.GridSizeOptions.First(o => o.Value == "Medium");

    Assert.Equal(GridSize.Medium, service.GridSize);
}

[Fact]
public void Modifiers_AreLocalizedOptions()
{
    var (_, _, _, vm) = CreateViewModel();

    Assert.Equal("Alt", vm.Modifiers[0].Value);
}

[Fact]
public void SelectedModifier_PersistsEnglishValue()
{
    var (service, _, _, vm) = CreateViewModel();

    var ctrlOption = vm.Modifiers.First(o => o.Value == "Ctrl");
    vm.SelectedModifier = ctrlOption;

    Assert.Equal(Constants.MOD_CONTROL, service.HotkeyModifiers);
}
```

- [ ] **Step 2: Run tests to verify new tests fail**

Run: `dotnet test Launchbox.Tests/Launchbox.Tests.csproj --filter "FullyQualifiedName~SettingsViewModelTests" -v n`
Expected: New tests fail — `GridSizeOptions` returns strings, not `LocalizedOption`.

- [ ] **Step 3: Update SettingsViewModel to use LocalizedOption**

In `ViewModels/SettingsViewModel.cs`:

1. Replace `Modifiers` property (line 31):
```csharp
public IReadOnlyList<LocalizedOption> Modifiers { get; } =
[
    new("Alt", Localization.GetString("Modifier_Alt")),
    new("Ctrl", Localization.GetString("Modifier_Ctrl")),
    new("Shift", Localization.GetString("Modifier_Shift")),
    new("Win", Localization.GetString("Modifier_Win")),
];
```

2. Replace `GridSizeOptions` property (line 33):
```csharp
public IReadOnlyList<LocalizedOption> GridSizeOptions { get; } =
[
    new("Small", Localization.GetString("GridSize_Small")),
    new("Medium", Localization.GetString("GridSize_Medium")),
    new("Large", Localization.GetString("GridSize_Large")),
];
```

3. Update `SelectedGridSize` property (lines 35-43):
```csharp
public LocalizedOption SelectedGridSize
{
    get => GridSizeOptions.FirstOrDefault(o => o.Value == _settingsService.GridSize.ToString())
        ?? GridSizeOptions[1]; // Default to Medium
    set
    {
        if (Enum.TryParse<GridSize>(value.Value, out var g))
            _settingsService.GridSize = g;
    }
}
```

4. Update `SelectedModifier` property (lines 123-131):
```csharp
public LocalizedOption SelectedModifier
{
    get
    {
        var key = MODIFIER_MAP.FirstOrDefault(kv => kv.Value == _settingsService.HotkeyModifiers).Key ?? "Alt";
        return Modifiers.FirstOrDefault(o => o.Value == key) ?? Modifiers[0];
    }
    set
    {
        if (MODIFIER_MAP.TryGetValue(value.Value, out var modifier))
            _settingsService.HotkeyModifiers = modifier;
    }
}
```

- [ ] **Step 4: Update existing tests that reference old string types**

These existing tests break because `SelectedGridSize` and `SelectedModifier` changed from `string` to `LocalizedOption`. Update each one:

**`SelectedModifier_ConvertsToConstants` (line 70-82):**
```csharp
[Fact]
public void SelectedModifier_ConvertsToConstants()
{
    var (service, _, _, vm) = CreateViewModel();

    vm.SelectedModifier = vm.Modifiers.First(o => o.Value == "Ctrl");
    Assert.Equal(Constants.MOD_CONTROL, service.HotkeyModifiers);

    vm.SelectedModifier = vm.Modifiers.First(o => o.Value == "Win");
    Assert.Equal(Constants.MOD_WIN, service.HotkeyModifiers);

    vm.SelectedModifier = vm.Modifiers.First(o => o.Value == "Alt");
    Assert.Equal(Constants.MOD_ALT, service.HotkeyModifiers);
}
```

**`SelectedGridSize_Default_IsMedium` (line 164-171):** Add `Localization.SetProvider(...)` at the top and change the assertion:
```csharp
[Fact]
public void SelectedGridSize_Default_IsMedium()
{
    Localization.SetProvider(new MockStringProvider(new()
    {
        { "GridSize_Small", "Small" }, { "GridSize_Medium", "Medium" }, { "GridSize_Large", "Large" },
        { "Modifier_Alt", "Alt" }, { "Modifier_Ctrl", "Ctrl" }, { "Modifier_Shift", "Shift" }, { "Modifier_Win", "Win" },
    }));
    var store = new MockSettingsStore();
    var settingsService = new SettingsService(store, new MockStartupService());
    var vm = new SettingsViewModel(settingsService, new MockWindowService(), new MockFilePickerService());
    Assert.Equal("Medium", vm.SelectedGridSize.Value);
}
```

**`SelectedGridSize_WhenSet_UpdatesSettingsService` (line 173-181):** Same pattern:
```csharp
[Fact]
public void SelectedGridSize_WhenSet_UpdatesSettingsService()
{
    Localization.SetProvider(new MockStringProvider(new()
    {
        { "GridSize_Small", "Small" }, { "GridSize_Medium", "Medium" }, { "GridSize_Large", "Large" },
        { "Modifier_Alt", "Alt" }, { "Modifier_Ctrl", "Ctrl" }, { "Modifier_Shift", "Shift" }, { "Modifier_Win", "Win" },
    }));
    var store = new MockSettingsStore();
    var settingsService = new SettingsService(store, new MockStartupService());
    var vm = new SettingsViewModel(settingsService, new MockWindowService(), new MockFilePickerService());
    vm.SelectedGridSize = vm.GridSizeOptions.First(o => o.Value == "Large");
    Assert.Equal(GridSize.Large, settingsService.GridSize);
}
```

**`SelectedGridSize_WhenServiceChanges_RaisesPropertyChanged` (line 183-195):** Add same provider setup.

**`GridSizeOptions_ContainsThreeEntries` (line 197-204):**
```csharp
[Fact]
public void GridSizeOptions_ContainsThreeEntries()
{
    Localization.SetProvider(new MockStringProvider(new()
    {
        { "GridSize_Small", "Small" }, { "GridSize_Medium", "Medium" }, { "GridSize_Large", "Large" },
        { "Modifier_Alt", "Alt" }, { "Modifier_Ctrl", "Ctrl" }, { "Modifier_Shift", "Shift" }, { "Modifier_Win", "Win" },
    }));
    var store = new MockSettingsStore();
    var settingsService = new SettingsService(store, new MockStartupService());
    var vm = new SettingsViewModel(settingsService, new MockWindowService(), new MockFilePickerService());
    Assert.Equal(3, vm.GridSizeOptions.Count);
    Assert.Equal("Small", vm.GridSizeOptions[0].Value);
    Assert.Equal("Medium", vm.GridSizeOptions[1].Value);
    Assert.Equal("Large", vm.GridSizeOptions[2].Value);
}
```

- [ ] **Step 5: Run tests to verify all pass**

Run: `dotnet test Launchbox.Tests/Launchbox.Tests.csproj --filter "FullyQualifiedName~SettingsViewModelTests" -v n`
Expected: All tests pass.

- [ ] **Step 6: Commit**

```bash
git add ViewModels/SettingsViewModel.cs Launchbox.Tests/SettingsViewModelTests.cs
git commit -m "feat: localize SettingsViewModel with LocalizedOption for display/storage separation"
```

---

## Task 6: Localize MainWindow.xaml (x:Uid for In-Tree Elements)

**Files:**
- Modify: `MainWindow.xaml`

- [ ] **Step 1: Replace hardcoded strings with x:Uid on in-tree elements**

In `MainWindow.xaml`, make these changes:

1. Line 69 — "No shortcuts found" TextBlock:
```xml
<!-- Before -->
<TextBlock Text="No shortcuts found" FontSize="16" FontWeight="SemiBold" HorizontalAlignment="Center"/>
<!-- After -->
<TextBlock x:Uid="MainWindow_NoShortcuts" FontSize="16" FontWeight="SemiBold" HorizontalAlignment="Center"/>
```

2. Lines 73-75 — "Add shortcuts..." TextBlock:
```xml
<!-- Before -->
<TextBlock Text="Add shortcuts to your Desktop/Shortcuts folder" Foreground="{ThemeResource TextFillColorSecondaryBrush}" HorizontalAlignment="Center"/>
<!-- After -->
<TextBlock x:Uid="MainWindow_AddShortcuts" Foreground="{ThemeResource TextFillColorSecondaryBrush}" HorizontalAlignment="Center"/>
```

3. Lines 76-86 — "Open Shortcuts Folder" Button. Remove `ToolTipService.ToolTip` and `AutomationProperties.Name` attributes (these will come from `.resw`). Add `x:Uid="MainWindow_OpenFolder"` to the Button — this sets `.ToolTipService.ToolTip` and `.AutomationProperties.Name` from the `.resw` file. Do NOT add a `.Content` key in `.resw` because the Button has inline child content (StackPanel). Use a separate `x:Uid="MainWindow_OpenFolderText"` on the inner TextBlock for the visible text:
```xml
<!-- Before -->
<Button Command="{x:Bind ViewModel.OpenShortcutsFolderCommand}"
        HorizontalAlignment="Center"
        Style="{ThemeResource AccentButtonStyle}"
        Margin="0,8,0,0"
        ToolTipService.ToolTip="Open your configured shortcuts folder"
        AutomationProperties.Name="Open Shortcuts Folder">
    <StackPanel Orientation="Horizontal" Spacing="8">
        <SymbolIcon Symbol="Folder" />
        <TextBlock Text="Open Shortcuts Folder" />
    </StackPanel>
</Button>
<!-- After -->
<Button x:Uid="MainWindow_OpenFolder"
        Command="{x:Bind ViewModel.OpenShortcutsFolderCommand}"
        HorizontalAlignment="Center"
        Style="{ThemeResource AccentButtonStyle}"
        Margin="0,8,0,0">
    <StackPanel Orientation="Horizontal" Spacing="8">
        <SymbolIcon Symbol="Folder" />
        <TextBlock x:Uid="MainWindow_OpenFolderText" />
    </StackPanel>
</Button>
```

4. Lines 143-157 — SearchBox TextBox. Remove `PlaceholderText`, `AutomationProperties.Name`, `ToolTipService.ToolTip` attributes:
```xml
<!-- Before -->
<TextBox x:Name="SearchBox"
         Grid.Row="1"
         Text="{x:Bind ViewModel.FilterText, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}"
         PlaceholderText="Search..."
         AutomationProperties.Name="Search shortcuts"
         ToolTipService.ToolTip="Search shortcuts (Esc to clear)"
         ...>
<!-- After -->
<TextBox x:Uid="MainWindow_SearchBox"
         x:Name="SearchBox"
         Grid.Row="1"
         Text="{x:Bind ViewModel.FilterText, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}"
         ...>
```

5. Line 92 — GridView AutomationProperties.Name:
```xml
<!-- Before -->
<GridView x:Name="AppGrid" Grid.Row="0" AutomationProperties.Name="Shortcuts Grid" ...>
<!-- After -->
<GridView x:Uid="MainWindow_ShortcutsGrid" x:Name="AppGrid" Grid.Row="0" ...>
```

6. Lines 159-166 — "No matches" TextBlock:
```xml
<!-- Before -->
<TextBlock ... Text="No matches" .../>
<!-- After -->
<TextBlock x:Uid="MainWindow_NoMatches" ... />
```
Remove the `Text="No matches"` attribute since `x:Uid` will set it.

- [ ] **Step 2: Update tray flyout items to use ViewModel binding**

Lines 38-39 — Settings MenuFlyoutItem:
```xml
<!-- Before -->
<MenuFlyoutItem Text="Settings"
                AutomationProperties.Name="Settings"
                Command="{Binding ViewModel.OpenSettingsCommand}">
<!-- After -->
<MenuFlyoutItem Text="{Binding ViewModel.SettingsMenuText, Mode=OneWay}"
                AutomationProperties.Name="{Binding ViewModel.SettingsMenuText, Mode=OneWay}"
                Command="{Binding ViewModel.OpenSettingsCommand}">
```

Lines 46-48 — Exit MenuFlyoutItem:
```xml
<!-- Before -->
<MenuFlyoutItem Text="Exit"
                AutomationProperties.Name="Exit"
                Command="{Binding ViewModel.ExitCommand}">
<!-- After -->
<MenuFlyoutItem Text="{Binding ViewModel.ExitMenuText, Mode=OneWay}"
                AutomationProperties.Name="{Binding ViewModel.ExitMenuText, Mode=OneWay}"
                Command="{Binding ViewModel.ExitCommand}">
```

- [ ] **Step 3: Verify .resw file has the OpenFolderText entry**

Confirm `Strings/en-US/Resources.resw` contains the `MainWindow_OpenFolderText.Text` entry (already added in Task 3 Step 2).

- [ ] **Step 4: Build the app**

Run: `dotnet build Launchbox.csproj -p:Platform=x64`
Expected: Build succeeds. This catches any XAML binding or `x:Uid` compilation issues.

- [ ] **Step 5: Run all tests**

Run: `dotnet test Launchbox.Tests/Launchbox.Tests.csproj -v n`
Expected: All tests pass.

- [ ] **Step 6: Commit**

```bash
git add MainWindow.xaml Strings/en-US/Resources.resw
git commit -m "feat: localize MainWindow.xaml with x:Uid and tray menu bindings"
```

---

## Task 7: Localize SettingsWindow.xaml (x:Uid for In-Tree Elements)

**Files:**
- Modify: `SettingsWindow.xaml`
- Modify: `SettingsWindow.xaml.cs`

- [ ] **Step 1: Replace hardcoded strings with x:Uid in SettingsWindow.xaml**

Apply `x:Uid` to all in-tree elements with hardcoded strings. Remove the corresponding hardcoded `Text`, `Header`, `Content`, `ToolTipService.ToolTip`, `AutomationProperties.Name`, and `PlaceholderText` attributes from each element — the `.resw` file provides them.

Key changes:

1. Line 7 — Remove `Title="Launchbox Settings"` from `<Window>` tag (set from code-behind).

2. Lines 20-22 — Title TextBlock:
```xml
<!-- Before -->
<TextBlock Text="Launchbox Settings" .../>
<!-- After -->
<TextBlock x:Uid="Settings_TitleText" .../>
```

3. Line 30 — "General" header:
```xml
<TextBlock x:Uid="Settings_GeneralHeader" Style="{ThemeResource SubtitleTextBlockStyle}"/>
```

4. Lines 31-33 — Startup ToggleSwitch:
```xml
<ToggleSwitch x:Uid="Settings_StartupToggle" IsOn="{x:Bind ViewModel.RunAtStartup, Mode=TwoWay}" />
```

5. Line 38 — "Shortcuts Folder" header:
```xml
<TextBlock x:Uid="Settings_ShortcutsHeader" Style="{ThemeResource SubtitleTextBlockStyle}"/>
```

6. Lines 40-42 — Shortcuts path TextBox:
```xml
<TextBox x:Uid="Settings_ShortcutsPath" Text="{x:Bind ViewModel.ShortcutsPath, Mode=OneWay}" IsReadOnly="True" Width="300"
         ToolTipService.ToolTip="{x:Bind ViewModel.ShortcutsPath, Mode=OneWay}" />
```

7. Lines 43-45 — Browse button:
```xml
<Button x:Uid="Settings_BrowseButton" Grid.Column="1" Command="{x:Bind ViewModel.BrowseFolderCommand}" />
```

8. Lines 47-49 — Changes note:
```xml
<TextBlock x:Uid="Settings_ChangesNote" Style="{ThemeResource CaptionTextBlockStyle}"
           Foreground="{ThemeResource TextFillColorSecondaryBrush}"/>
```

9. Line 54 — "Global Hotkey" header:
```xml
<TextBlock x:Uid="Settings_HotkeyHeader" Style="{ThemeResource SubtitleTextBlockStyle}"/>
```

10. Lines 56-58 — Modifier ComboBox:
```xml
<ComboBox x:Uid="Settings_HotkeyModifier" ItemsSource="{x:Bind ViewModel.Modifiers}" SelectedItem="{x:Bind ViewModel.SelectedModifier, Mode=TwoWay}" Width="100" />
```

11. Lines 60-63 — Hotkey TextBox:
```xml
<TextBox x:Uid="Settings_HotkeyKey" Text="{x:Bind ViewModel.HotkeyKeyString, Mode=TwoWay, UpdateSourceTrigger=LostFocus}" Width="100" HorizontalContentAlignment="Center" />
```

12. Line 69 — "Appearance" header:
```xml
<TextBlock x:Uid="Settings_AppearanceHeader" Style="{ThemeResource SubtitleTextBlockStyle}"/>
```

13. Line 71 — "Icon size" label:
```xml
<TextBlock x:Uid="Settings_IconSizeLabel" VerticalAlignment="Center"/>
```

14. Lines 72-76 — Icon size ComboBox:
```xml
<ComboBox x:Uid="Settings_IconSizeCombo" ItemsSource="{x:Bind ViewModel.GridSizeOptions}"
          SelectedItem="{x:Bind ViewModel.SelectedGridSize, Mode=TwoWay}"
          Width="120" />
```

15. Line 82 — "Window Management" header:
```xml
<TextBlock x:Uid="Settings_WindowHeader" Style="{ThemeResource SubtitleTextBlockStyle}"/>
```

16. Lines 83-85 — Reset position button:
```xml
<Button x:Uid="Settings_ResetPosition" Command="{x:Bind ViewModel.ResetPositionCommand}" />
```

- [ ] **Step 2: Set Window.Title from code-behind**

In `SettingsWindow.xaml.cs`, change line 20 from:
```csharp
this.Title = "Launchbox Settings";
```
to:
```csharp
this.Title = Localization.GetString("SettingsWindow_Title");
```

Add `using Launchbox.Helpers;` to the imports.

- [ ] **Step 3: Build the app**

Run: `dotnet build Launchbox.csproj -p:Platform=x64`
Expected: Build succeeds.

- [ ] **Step 4: Run all tests**

Run: `dotnet test Launchbox.Tests/Launchbox.Tests.csproj -v n`
Expected: All tests pass.

- [ ] **Step 5: Commit**

```bash
git add SettingsWindow.xaml SettingsWindow.xaml.cs
git commit -m "feat: localize SettingsWindow.xaml with x:Uid and code-behind Title"
```

---

## Task 8: Localize MainWindow.xaml.cs and App.xaml.cs (Code-Behind Strings)

**Files:**
- Modify: `MainWindow.xaml.cs`
- Modify: `App.xaml.cs`

- [ ] **Step 1: Localize tray icon automation name in MainWindow.xaml.cs**

In `MainWindow.xaml.cs`, after `InitializeComponent()` (around line 54), add:

```csharp
// Set tray icon accessibility name from localized resources
TrayIcon.SetValue(
    Microsoft.UI.Xaml.Automation.AutomationProperties.NameProperty,
    Localization.GetString("Tray_AutomationName"));
```

Remove `AutomationProperties.Name="Launchbox System Tray Icon"` from `MainWindow.xaml` line 24.

- [ ] **Step 2: Localize notification title in MainWindow.xaml.cs**

Change line 170 from:
```csharp
TrayIcon?.ShowNotification("Launchbox Error", e);
```
to:
```csharp
TrayIcon?.ShowNotification(Localization.GetString("Error_NotificationTitle"), e);
```

- [ ] **Step 3: Localize critical error handler in App.xaml.cs**

`App.xaml.cs` already has `using Launchbox.Helpers;` from Task 3 Step 5. Change lines 35-39 from:
```csharp
NativeMethods.MessageBox(
    IntPtr.Zero,
    "Launchbox encountered a critical error and needs to close.",
    "Launchbox",
    NativeMethods.MB_OK | NativeMethods.MB_ICONERROR);
```
to:
```csharp
string title, message;
try
{
    title = Localization.GetString("Error_CriticalTitle");
    message = Localization.GetString("Error_CriticalMessage");
}
catch
{
    // ResourceLoader may not be functional during critical failures
    title = "Launchbox";
    message = "Launchbox encountered a critical error and needs to close.";
}
NativeMethods.MessageBox(
    IntPtr.Zero,
    message,
    title,
    NativeMethods.MB_OK | NativeMethods.MB_ICONERROR);
```

- [ ] **Step 4: Build the app**

Run: `dotnet build Launchbox.csproj -p:Platform=x64`
Expected: Build succeeds.

- [ ] **Step 5: Run all tests**

Run: `dotnet test Launchbox.Tests/Launchbox.Tests.csproj -v n`
Expected: All tests pass.

- [ ] **Step 6: Commit**

```bash
git add MainWindow.xaml MainWindow.xaml.cs App.xaml.cs
git commit -m "feat: localize code-behind strings in MainWindow and App"
```

---

## Task 9: Generate Translations (7 Locales)

**Files:**
- Create: `Strings/es/Resources.resw`
- Create: `Strings/fr/Resources.resw`
- Create: `Strings/de/Resources.resw`
- Create: `Strings/ja/Resources.resw`
- Create: `Strings/zh-Hans/Resources.resw`
- Create: `Strings/ko/Resources.resw`
- Create: `Strings/pt-BR/Resources.resw`

- [ ] **Step 1: Generate translations using Gemini and Codex**

For each target locale, send the complete `en-US/Resources.resw` to both Gemini and Codex with this context:

> Translate the following WinUI 3 `.resw` resource file to [LANGUAGE]. Context:
> - "Launchbox" is a brand name — do NOT translate it
> - This is a Windows desktop app launcher that lives in the system tray
> - Modifier key names (Alt, Ctrl, Shift, Win) should use the locale's conventional keyboard terminology
> - Keep translations concise — button labels and menu items have limited space
> - Preserve the exact XML structure, `name` attributes, and `xml:space="preserve"` attributes
> - Only translate the `<value>` content

Process each locale independently. Where Gemini and Codex agree, use that translation. Where they disagree, pick the better one based on natural phrasing and brevity.

- [ ] **Step 2: Create resource files for each locale**

Save each translated `.resw` file to its locale directory:
- `Strings/es/Resources.resw`
- `Strings/fr/Resources.resw`
- `Strings/de/Resources.resw`
- `Strings/ja/Resources.resw`
- `Strings/zh-Hans/Resources.resw`
- `Strings/ko/Resources.resw`
- `Strings/pt-BR/Resources.resw`

- [ ] **Step 3: Validate key parity**

Verify every locale has exactly the same `name` attributes as `en-US`. No missing or extra keys.

Run a quick validation (check that all files have the same count of `<data name=` entries):
```bash
for dir in Strings/*/; do echo "$dir: $(grep -c 'data name=' "$dir/Resources.resw")"; done
```
Expected: All directories show the same count. (Note: the shell is bash on Windows per the environment config.)

- [ ] **Step 4: Build the app**

Run: `dotnet build Launchbox.csproj -p:Platform=x64`
Expected: Build succeeds with all locale resource files included.

- [ ] **Step 5: Run all tests**

Run: `dotnet test Launchbox.Tests/Launchbox.Tests.csproj -v n`
Expected: All tests pass.

- [ ] **Step 6: Commit**

```bash
git add Strings/
git commit -m "feat: add translations for es, fr, de, ja, zh-Hans, ko, pt-BR"
```

---

## Task 10: Format Check and Final Verification

**Files:**
- All modified files

- [ ] **Step 1: Run dotnet format**

Run: `dotnet format Launchbox.sln`
Expected: Fixes any formatting issues introduced during localization.

- [ ] **Step 2: Build the full app**

Run: `dotnet build Launchbox.csproj -p:Platform=x64`
Expected: Clean build with no warnings related to localization.

- [ ] **Step 3: Run full test suite**

Run: `dotnet test Launchbox.Tests/Launchbox.Tests.csproj -v n`
Expected: All tests pass (existing + new localization tests).

- [ ] **Step 4: Commit any format fixes**

```bash
git add -A
git commit -m "style: apply dotnet format"
```

- [ ] **Step 5: Verify resource key completeness**

Check that every `x:Uid` used in XAML has a corresponding entry in `en-US/Resources.resw`:
```bash
# Extract all x:Uid values from XAML
grep -oP 'x:Uid="([^"]+)"' MainWindow.xaml SettingsWindow.xaml | sort -u
# Compare with resource keys
grep -oP 'name="([^"]+)"' Strings/en-US/Resources.resw | sort -u
```

Ensure every `x:Uid` value from XAML appears as a prefix in at least one resource key.
