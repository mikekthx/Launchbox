# Window Auto-Sizing & Multi-Folder Sources Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make the Launchbox window freely resizable with work-area clamping, and support multiple shortcut folders with merged/grouped display modes.

**Architecture:** Window auto-sizing extends `WindowService` with `WM_GETMINMAXINFO` handling and width clamping (preserving `_suppressSave` so clamp-induced resizes don't overwrite user-preferred size). Multi-folder support introduces `ShortcutFolderManager` (thread-safe with `lock`) for folder CRUD with JSON persistence, extends `MainViewModel` with grouped data structures, and adds a folder list editor to Settings. Two `GridView` instances in `MainWindow.xaml` swap visibility for merged/grouped modes. Grouping uses `FolderPath` (unique) as identity, `FolderLabel` for display. Collapse/filter uses in-place `ObservableCollection` mutation (not new group instances) to preserve scroll position.

**Tech Stack:** WinUI 3 / Windows App SDK 1.8, .NET 10, C# 12+, CommunityToolkit.Mvvm, System.Text.Json, xUnit v3

**Spec:** `docs/superpowers/specs/2026-03-21-window-autosizing-multifolders-design.md`

---

## File Map

### New files
| File | Responsibility |
|------|---------------|
| `Models/ShortcutFolder.cs` | Immutable record for folder config (Path, Label, Order) |
| `Models/FolderViewMode.cs` | Enum: Merged, Grouped |
| `Models/AppItemGroup.cs` | Grouped collection for `CollectionViewSource` with bindable `IsCollapsed` |
| `Services/ShortcutFolderManager.cs` | Folder CRUD, JSON serialization, migration, validation, thread-safe cache |
| `Helpers/CollapseChevronConverter.cs` | IValueConverter: bool → chevron glyph (down/right) |
| `Helpers/EmptyStringToCollapsedConverter.cs` | IValueConverter: hides placeholder items in collapsed groups |
| `Launchbox.Tests/ShortcutFolderManagerTests.cs` | Tests for folder manager |

### Modified files
| File | Changes |
|------|---------|
| `Helpers/Constants.cs` | Add `MIN_WINDOW_WIDTH`, `MIN_WINDOW_HEIGHT` |
| `Services/NativeMethods.cs` | Add `MINMAXINFO` struct, `WM_GETMINMAXINFO` constant |
| `Services/WindowService.cs` | Extend `ClampToWorkArea` with width clamping (preserve `_suppressSave`), add `WM_GETMINMAXINFO` handler, respect `KeepCentered` |
| `Services/SettingsService.cs` | Replace `ShortcutsPath` with `ShortcutFolderManager` delegation, add `FolderViewMode`, `CollapsibleGroups`, `KeepCentered` |
| `Models/AppItem.cs` | Add `FolderLabel` property |
| `ViewModels/MainViewModel.cs` | Multi-folder loading, `GroupedApps`, in-place filter/collapse, update `OpenShortcutsFolder` |
| `ViewModels/SettingsViewModel.cs` | Folder list + commands, view mode, collapsible toggle, keep centered |
| `SettingsWindow.xaml` | Folder list UI, view mode controls, keep centered toggle |
| `MainWindow.xaml` | Two GridViews (merged/grouped), shared `AppItemContainerStyle`, `CollectionViewSource`, group header template |
| `MainWindow.xaml.cs` | Wire `ShortcutFolderManager`, group header Tapped handler |
| `SettingsWindow.xaml.cs` | Set `SettingsContent.DataContext = this` for DataTemplate bindings |
| `Launchbox.Tests/Launchbox.Tests.csproj` | File-link new production files |
| `Launchbox.Tests/MainViewModelTests.cs` | Update setup for multi-folder, add grouped/filter tests |
| `Strings/*/Resources.resw` (8 files) | Add localized strings |

---

## Phase 1: Window Auto-Sizing

### Task 1: Add minimum window dimension constants

**Files:**
- Modify: `Helpers/Constants.cs:7-8`
- Test: `Launchbox.Tests/ConstantsTests.cs` (if exists, otherwise skip — trivial constants)

- [ ] **Step 1: Add constants**

In `Helpers/Constants.cs`, after `WINDOW_HEIGHT` (line 8), add:

```csharp
public const int MIN_WINDOW_WIDTH = 300;
public const int MIN_WINDOW_HEIGHT = 200;
```

- [ ] **Step 2: Build to verify**

Run: `dotnet build Launchbox.csproj -p:Platform=x64`
Expected: Build succeeded

- [ ] **Step 3: Commit**

```bash
git add Helpers/Constants.cs
git commit -m "feat: add minimum window dimension constants"
```

---

### Task 2: Add MINMAXINFO struct and WM_GETMINMAXINFO constant

**Files:**
- Modify: `Services/NativeMethods.cs:44-49` (WM_ constants), `Services/NativeMethods.cs:98-103` (near existing POINT struct)

- [ ] **Step 1: Add WM_GETMINMAXINFO constant**

In `Services/NativeMethods.cs`, in the WM_ constants section (after `WM_NCLBUTTONDBLCLK` around line 49), add:

```csharp
public const int WM_GETMINMAXINFO = 0x0024;
```

- [ ] **Step 2: Add MINMAXINFO struct**

After the existing `POINT` struct (line 103), add:

```csharp
[StructLayout(LayoutKind.Sequential)]
public struct MINMAXINFO
{
    public POINT ptReserved;
    public POINT ptMaxSize;
    public POINT ptMaxPosition;
    public POINT ptMinTrackSize;
    public POINT ptMaxTrackSize;
}
```

- [ ] **Step 3: Build to verify**

Run: `dotnet build Launchbox.csproj -p:Platform=x64`
Expected: Build succeeded

- [ ] **Step 4: Commit**

```bash
git add Services/NativeMethods.cs
git commit -m "feat: add MINMAXINFO struct and WM_GETMINMAXINFO constant"
```

---

### Task 3: Add WM_GETMINMAXINFO handler to WindowService

**Files:**
- Modify: `Services/WindowService.cs:154-166` (NewWndProc method)

- [ ] **Step 1: Add WM_GETMINMAXINFO handler to NewWndProc**

`NewWndProc` uses an `if/else` chain (NOT a `switch`). Add a new `if` block **before** the existing `WM_HOTKEY` check (line 156):

```csharp
if (msg == NativeMethods.WM_GETMINMAXINFO)
{
    var mmi = Marshal.PtrToStructure<NativeMethods.MINMAXINFO>(lParam);
    mmi.ptMinTrackSize.X = Constants.MIN_WINDOW_WIDTH;
    mmi.ptMinTrackSize.Y = Constants.MIN_WINDOW_HEIGHT;
    Marshal.StructureToPtr(mmi, lParam, false);
    return IntPtr.Zero;
}
```

Add `using System.Runtime.InteropServices;` at the top of the file (it is not currently present and is required for `Marshal`).

- [ ] **Step 2: Build to verify**

Run: `dotnet build Launchbox.csproj -p:Platform=x64`
Expected: Build succeeded

- [ ] **Step 3: Commit**

```bash
git add Services/WindowService.cs
git commit -m "feat: enforce minimum window size via WM_GETMINMAXINFO"
```

---

### Task 4: Extend ClampToWorkArea with width clamping (preserve _suppressSave)

**Files:**
- Modify: `Services/WindowService.cs:355-403` (ClampToWorkArea)

> **Review fix:** `_suppressSave` must be retained. Removing it causes clamp-induced resizes to overwrite the user's preferred window size when moving to a smaller display. Instead, wrap the `Resize` call with `_suppressSave = true/false` so clamp-induced resizes don't persist.

- [ ] **Step 1: Extend ClampToWorkArea to clamp width**

In `ClampToWorkArea` (line 355), the current logic only clamps height. Add width clamping following the same variable pattern. The existing code uses `size` (from `_appWindow.Size`), `needsResize`, and `clampedHeight`. Add analogous width logic:

```csharp
// Existing height clamping (already present):
int maxHeight = workArea.Height - 40;
bool needsResize = size.Height > maxHeight;
int clampedHeight = needsResize ? maxHeight : size.Height;

// NEW: Add width clamping (same pattern as height):
int maxWidth = workArea.Width - 40;
bool needsWidthClamp = size.Width > maxWidth;
int clampedWidth = needsWidthClamp ? maxWidth : size.Width;
needsResize = needsResize || needsWidthClamp;
```

- [ ] **Step 2: Wrap the Resize call with _suppressSave (preserve existing try/finally)**

The `Resize` call must NOT trigger a settings save (the clamped size is temporary — the user's preferred size should be preserved for when they return to a larger display). The existing `ClampToWorkArea` already uses a `try/finally` pattern for `_suppressSave` (see `WindowService.cs:377-402`). Preserve that pattern — add the width clamping within the existing `try/finally` block:

```csharp
if (needsResize)
{
    _suppressSave = true;
    try
    {
        _appWindow.Resize(new Windows.Graphics.SizeInt32(clampedWidth, clampedHeight));
    }
    finally
    {
        _suppressSave = false;
    }
}
```

**Important:** The existing code already has a `try/finally` wrapping `_suppressSave` around `Resize` and `Move`. Extend that existing block rather than creating a new one. Do NOT replace `try/finally` with a bare assignment — if `Resize` throws, `_suppressSave` must be reset.

After clamping, if position is off-screen horizontally or vertically, center on the work area.

- [ ] **Step 3: Build and run tests**

Run: `dotnet build Launchbox.csproj -p:Platform=x64 && dotnet test Launchbox.Tests/Launchbox.Tests.csproj --no-build -p:Platform=x64`
Expected: Build succeeded, all tests pass

- [ ] **Step 4: Commit**

```bash
git add Services/WindowService.cs
git commit -m "feat: add width clamping to ClampToWorkArea (preserve _suppressSave)"
```

---

### Task 5: Add KeepCentered setting

**Files:**
- Modify: `Services/SettingsService.cs`
- Modify: `Services/WindowService.cs:172-211` (ToggleVisibility)
- Modify: `ViewModels/SettingsViewModel.cs`
- Modify: `SettingsWindow.xaml`

- [ ] **Step 1: Add KeepCentered property to SettingsService**

Follow the existing `GridSize` property pattern. In `Services/SettingsService.cs`:

```csharp
public bool KeepCentered
{
    get
    {
        if (_store.TryGetValue(nameof(KeepCentered), out var val) && val is bool b)
        {
            return b;
        }
        return false;
    }
    set
    {
        if (KeepCentered != value && _store.SetValue(nameof(KeepCentered), value))
        {
            OnPropertyChanged();
        }
    }
}
```

- [ ] **Step 2: Update ToggleVisibility to respect KeepCentered**

In `WindowService.ToggleVisibility` (line 172), restructure the positioning logic. **Critical:** `_hasPositioned` must be set to `true` unconditionally before any positioning path, otherwise the hide toggle (`_window.Visible && _hasPositioned`) breaks when `KeepCentered` is enabled:

```csharp
// Set unconditionally BEFORE any positioning path — required for hide toggle to work
bool firstShow = !_hasPositioned;
_hasPositioned = true;

if (_settingsService.KeepCentered)
{
    if (firstShow)
    {
        // First show: restore saved size (not position), then center at that size
        RestoreWindowPosition(); // loads saved width/height
    }
    CenterOnCurrentDisplay();
}
else if (firstShow)
{
    bool positionRestored = RestoreWindowPosition();
    if (!positionRestored)
    {
        CenterWindow();
    }
}
```

Add a new helper method (distinct from the existing `CenterWindow()` which forces default size):

```csharp
private void CenterOnCurrentDisplay()
{
    if (_appWindow == null) return;
    var displayArea = DisplayArea.GetFromWindowId(_appWindow.Id, DisplayAreaFallback.Primary);
    var currentSize = _appWindow.Size;
    // Add display origin so centering works on secondary monitors (non-zero WorkArea.X/Y)
    var x = displayArea.WorkArea.X + (displayArea.WorkArea.Width - currentSize.Width) / 2;
    var y = displayArea.WorkArea.Y + (displayArea.WorkArea.Height - currentSize.Height) / 2;
    _appWindow.Move(new Windows.Graphics.PointInt32(x, y));
}
```

- [ ] **Step 3: Expose KeepCentered in SettingsViewModel**

Add passthrough property:

```csharp
public bool KeepCentered
{
    get => _settingsService.KeepCentered;
    set => _settingsService.KeepCentered = value;
}
```

Relay `PropertyChanged` from SettingsService for `KeepCentered` in the existing PropertyChanged handler.

- [ ] **Step 4: Add ToggleSwitch to SettingsWindow.xaml**

In the Window section (near line 71, after the Reset Position button):

```xml
<ToggleSwitch x:Uid="Settings_KeepCentered"
              IsOn="{x:Bind ViewModel.KeepCentered, Mode=TwoWay}" />
```

- [ ] **Step 5: Add localized string**

Add to all 8 `Strings/*/Resources.resw` files:
- Key: `Settings_KeepCentered.Header`, Value: `Keep Window Centered` (en-US; translate for others)

- [ ] **Step 6: Build and run tests**

Run: `dotnet build Launchbox.csproj -p:Platform=x64 && dotnet test Launchbox.Tests/Launchbox.Tests.csproj --no-build -p:Platform=x64`
Expected: Build succeeded, all tests pass

- [ ] **Step 7: Commit**

```bash
git add Services/SettingsService.cs Services/WindowService.cs ViewModels/SettingsViewModel.cs SettingsWindow.xaml Strings/
git commit -m "feat: add KeepCentered setting with toggle in Settings UI"
```

---

## Phase 2: Multi-Folder Data Model & Storage

### Task 6: Create ShortcutFolder record and FolderViewMode enum

**Files:**
- Create: `Models/ShortcutFolder.cs`
- Create: `Models/FolderViewMode.cs`
- Modify: `Launchbox.Tests/Launchbox.Tests.csproj` (file-link)

- [ ] **Step 1: Create ShortcutFolder.cs**

```csharp
namespace Launchbox.Models;

public record ShortcutFolder
{
    public required string Path { get; init; }
    public required string Label { get; init; }
    public required int Order { get; init; }
}
```

- [ ] **Step 2: Create FolderViewMode.cs**

```csharp
namespace Launchbox.Models;

public enum FolderViewMode
{
    Merged,
    Grouped
}
```

- [ ] **Step 3: Add file links to test project**

In `Launchbox.Tests/Launchbox.Tests.csproj`, in the `<Compile Include>` section:

```xml
<Compile Include="..\Models\ShortcutFolder.cs" Link="Models\ShortcutFolder.cs" />
<Compile Include="..\Models\FolderViewMode.cs" Link="Models\FolderViewMode.cs" />
```

- [ ] **Step 4: Build both projects**

Run: `dotnet build Launchbox.csproj -p:Platform=x64 && dotnet build Launchbox.Tests/Launchbox.Tests.csproj -p:Platform=x64`
Expected: Build succeeded

- [ ] **Step 5: Commit**

```bash
git add Models/ShortcutFolder.cs Models/FolderViewMode.cs Launchbox.Tests/Launchbox.Tests.csproj
git commit -m "feat: add ShortcutFolder record and FolderViewMode enum"
```

---

### Task 7: Create ShortcutFolderManager with tests (TDD)

**Files:**
- Create: `Services/ShortcutFolderManager.cs`
- Create: `Launchbox.Tests/ShortcutFolderManagerTests.cs`
- Modify: `Launchbox.Tests/Launchbox.Tests.csproj` (file-link)

This is the largest task. Build it incrementally with TDD.

- [ ] **Step 1: Add file link to test project**

```xml
<Compile Include="..\Services\ShortcutFolderManager.cs" Link="Services\ShortcutFolderManager.cs" />
```

- [ ] **Step 2: Write test — GetFolders returns default when no stored value**

```csharp
using Launchbox.Models;
using Launchbox.Services;
using Xunit;

namespace Launchbox.Tests;

public class ShortcutFolderManagerTests
{
    private readonly MockSettingsStore _store;
    private readonly ShortcutFolderManager _manager;

    public ShortcutFolderManagerTests()
    {
        _store = new MockSettingsStore();
        _manager = new ShortcutFolderManager(_store);
    }

    [Fact]
    public void GetFolders_ReturnsDefault_WhenNoStoredValue()
    {
        var folders = _manager.GetFolders();

        Assert.Single(folders);
        Assert.Contains("Shortcuts", folders[0].Path);
        Assert.Equal(0, folders[0].Order);
    }
}
```

- [ ] **Step 3: Run test to verify it fails**

Run: `dotnet test Launchbox.Tests/Launchbox.Tests.csproj --filter "FullyQualifiedName~ShortcutFolderManagerTests.GetFolders_ReturnsDefault" -p:Platform=x64`
Expected: FAIL (class doesn't exist yet)

- [ ] **Step 4: Create minimal ShortcutFolderManager**

```csharp
using Launchbox.Helpers;
using Launchbox.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Launchbox.Services;

public class ShortcutFolderManager
{
    private const string FOLDERS_KEY = "ShortcutFolders";
    private const string LEGACY_KEY = "ShortcutsPath";
    private const int MAX_FOLDERS = 20;
    private const int MAX_JSON_BYTES = 7168; // 7KB safety margin under 8KB LocalSettings limit

    private readonly ISettingsStore _store;
    private readonly object _lock = new();
    // volatile for read visibility from UI thread; lock protects read-modify-write in mutations
    private volatile IReadOnlyList<ShortcutFolder> _cache;

    public ShortcutFolderManager(ISettingsStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        _store = store;
        _cache = LoadFolders();
    }

    public IReadOnlyList<ShortcutFolder> GetFolders() => _cache;

    private IReadOnlyList<ShortcutFolder> LoadFolders()
    {
        // Try reading stored JSON — if the new key exists, legacy is implicitly ignored
        if (_store.TryGetValue(FOLDERS_KEY, out var val) && val is string json && !string.IsNullOrEmpty(json))
        {
            try
            {
                var folders = JsonSerializer.Deserialize<List<ShortcutFolder>>(json);
                if (folders != null && folders.Count > 0)
                {
                    return ValidateAndNormalize(folders);
                }
            }
            catch (JsonException ex)
            {
                System.Diagnostics.Trace.WriteLine($"Corrupt ShortcutFolders JSON, using default: {ex.Message}");
                // Do NOT overwrite — allows manual recovery
            }
        }

        // Migration: check legacy key (new key's existence supersedes legacy — no sentinel needed)
        if (_store.TryGetValue(LEGACY_KEY, out var legacyVal) && legacyVal is string legacyPath
            && !string.IsNullOrEmpty(legacyPath))
        {
            var label = Path.GetFileName(legacyPath) ?? "Shortcuts";
            var migrated = new List<ShortcutFolder>
            {
                new() { Path = legacyPath, Label = label, Order = 0 }
            };
            _store.SetValue(FOLDERS_KEY, JsonSerializer.Serialize(migrated));
            // No sentinel write — presence of FOLDERS_KEY is sufficient to skip legacy on next load
            return ValidateAndNormalize(migrated);
        }

        // Default
        var defaultPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "Shortcuts");
        return [new ShortcutFolder { Path = defaultPath, Label = "Shortcuts", Order = 0 }];
    }

    public bool AddFolder(string path, string? label = null)
    {
        lock (_lock)
        {
            var folders = new List<ShortcutFolder>(_cache);
            if (folders.Count >= MAX_FOLDERS) return false;

            if (PathSecurity.IsUnsafePath(path)) return false;

            label ??= Path.GetFileName(path) ?? path;
            var newFolder = new ShortcutFolder { Path = path, Label = label, Order = folders.Count };
            folders.Add(newFolder);

            return TryPersistAndCache(folders);
        }
    }

    public bool RemoveFolder(int order)
    {
        lock (_lock)
        {
            var folders = new List<ShortcutFolder>(_cache);
            var index = folders.FindIndex(f => f.Order == order);
            if (index < 0) return false;

            folders.RemoveAt(index);
            var normalized = Renumber(folders);
            return TryPersistAndCache(normalized);
        }
    }

    public bool ReorderFolder(int fromOrder, int toOrder)
    {
        lock (_lock)
        {
            var folders = new List<ShortcutFolder>(_cache);
            var fromIndex = folders.FindIndex(f => f.Order == fromOrder);
            var toIndex = folders.FindIndex(f => f.Order == toOrder);
            if (fromIndex < 0 || toIndex < 0) return false;

            var item = folders[fromIndex];
            folders.RemoveAt(fromIndex);
            folders.Insert(toIndex, item);

            var normalized = Renumber(folders);
            return TryPersistAndCache(normalized);
        }
    }

    public bool RenameFolder(int order, string newLabel)
    {
        lock (_lock)
        {
            if (string.IsNullOrWhiteSpace(newLabel)) return false;

            var folders = new List<ShortcutFolder>(_cache);
            var index = folders.FindIndex(f => f.Order == order);
            if (index < 0) return false;

            folders[index] = folders[index] with { Label = newLabel };
            return TryPersistAndCache(folders);
        }
    }

    private bool TryPersistAndCache(List<ShortcutFolder> folders)
    {
        // Serialize once — reuse for both size check and persistence
        var json = JsonSerializer.Serialize(folders);
        if (System.Text.Encoding.UTF8.GetByteCount(json) > MAX_JSON_BYTES)
        {
            return false;
        }
        if (!_store.SetValue(FOLDERS_KEY, json)) return false;
        _cache = ValidateAndNormalize(folders);
        return true;
    }

    private static IReadOnlyList<ShortcutFolder> ValidateAndNormalize(List<ShortcutFolder> folders)
    {
        var valid = folders
            .Where(f => !string.IsNullOrEmpty(f.Path) && !PathSecurity.IsUnsafePath(
                Environment.ExpandEnvironmentVariables(f.Path)))
            .ToList();

        return Renumber(valid).AsReadOnly();
    }

    private static List<ShortcutFolder> Renumber(List<ShortcutFolder> folders)
    {
        return folders.Select((f, i) => f with { Order = i }).ToList();
    }
}
```

- [ ] **Step 5: Run test to verify it passes**

Run: `dotnet test Launchbox.Tests/Launchbox.Tests.csproj --filter "FullyQualifiedName~ShortcutFolderManagerTests.GetFolders_ReturnsDefault" -p:Platform=x64`
Expected: PASS

- [ ] **Step 6: Write remaining tests**

Add tests for:
- `GetFolders_DeserializesStoredJson`
- `GetFolders_MigratesLegacyShortcutsPath`
- `GetFolders_AfterMigration_UsesNewKeyIgnoresLegacy`
- `GetFolders_MalformedJson_ReturnsDefault_DoesNotOverwrite`
- `GetFolders_DropsUnsafePaths`
- `GetFolders_NormalizesOrderGaps`
- `AddFolder_AppendsAndPersists`
- `AddFolder_RejectsAtMaxCap`
- `AddFolder_RejectsUnsafePath`
- `RemoveFolder_RemovesAndRenumbers`
- `ReorderFolder_SwapsCorrectly`
- `RenameFolder_UpdatesLabel`
- `RenameFolder_RejectsEmpty`
- `TryPersist_RejectsOversizedJson`
- `GetFolders_ThreadSafe_ReturnsConsistentSnapshot`

Each test follows the pattern from Step 2. Write all, run all:

Run: `dotnet test Launchbox.Tests/Launchbox.Tests.csproj --filter "FullyQualifiedName~ShortcutFolderManagerTests" -p:Platform=x64`
Expected: All pass

- [ ] **Step 7: Commit**

```bash
git add Services/ShortcutFolderManager.cs Launchbox.Tests/ShortcutFolderManagerTests.cs Launchbox.Tests/Launchbox.Tests.csproj
git commit -m "feat: add ShortcutFolderManager with JSON persistence and migration"
```

---

### Task 8: Update SettingsService to use ShortcutFolderManager

**Files:**
- Modify: `Services/SettingsService.cs`
- Modify: `MainWindow.xaml.cs` (wire ShortcutFolderManager)

- [ ] **Step 1: Add ShortcutFolderManager field and constructor parameter**

In `SettingsService`, add a `ShortcutFolderManager` field. The manager is created in `MainWindow.xaml.cs` and passed to `SettingsService`.

```csharp
private readonly ShortcutFolderManager _folderManager;

public SettingsService(ISettingsStore store, IStartupService startupService, ShortcutFolderManager folderManager)
{
    // existing null guards...
    _folderManager = folderManager ?? throw new ArgumentNullException(nameof(folderManager));
}
```

- [ ] **Step 2: Add folder delegation alongside existing ShortcutsPath**

> **Review fix:** Do NOT remove the `ShortcutsPath` setter yet — it is still referenced by `SettingsViewModel.BrowseFolderCommand` and `SettingsWindow.xaml`. Removing it here would break the build before Tasks 11/12 migrate the UI. Add the new API alongside the existing one; the legacy surface is removed in Task 12 Step 5 after the UI migration.

Add the new folder API methods (keep existing `ShortcutsPath` property intact):

```csharp
public IReadOnlyList<ShortcutFolder> GetShortcutFolders() => _folderManager.GetFolders();

public bool AddShortcutFolder(string path, string? label = null)
{
    if (_folderManager.AddFolder(path, label))
    {
        OnPropertyChanged("ShortcutFolders");
        return true;
    }
    return false;
}

// Similar for RemoveShortcutFolder, ReorderShortcutFolder, RenameShortcutFolder
```

- [ ] **Step 3: Add FolderViewMode and CollapsibleGroups settings**

```csharp
public FolderViewMode FolderViewMode
{
    get
    {
        if (_store.TryGetValue(nameof(FolderViewMode), out var val) && val is string s
            && Enum.TryParse<FolderViewMode>(s, out var mode))
        {
            return mode;
        }
        return FolderViewMode.Merged;
    }
    set
    {
        if (FolderViewMode != value && _store.SetValue(nameof(FolderViewMode), value.ToString()))
        {
            OnPropertyChanged();
        }
    }
}

public bool CollapsibleGroups
{
    get
    {
        if (_store.TryGetValue(nameof(CollapsibleGroups), out var val) && val is bool b)
        {
            return b;
        }
        return true;
    }
    set
    {
        if (CollapsibleGroups != value && _store.SetValue(nameof(CollapsibleGroups), value))
        {
            OnPropertyChanged();
        }
    }
}
```

- [ ] **Step 4: Wire ShortcutFolderManager in MainWindow.xaml.cs**

In `MainWindow` constructor, create the manager and pass to `SettingsService`:

```csharp
var folderManager = new ShortcutFolderManager(settingsStore);
_settingsService = new SettingsService(settingsStore, startupService, folderManager);
```

- [ ] **Step 5: Update existing tests for new constructor parameter**

**All** test files that instantiate `SettingsService` must be updated to pass the new `ShortcutFolderManager` parameter. Search with `grep -rn "new SettingsService" Launchbox.Tests/` and update every match. Known files include: `MainViewModelTests.cs`, `MainViewModelErrorTests.cs`, `MainViewModelPerformanceTests.cs`, `MainViewModelRedactionTests.cs`, `SettingsServiceSecurityTests.cs`, `SettingsServiceTests.cs`, `SettingsViewModelTests.cs`. Create a shared helper if needed:

```csharp
private static SettingsService CreateSettingsService(MockSettingsStore store)
{
    var folderManager = new ShortcutFolderManager(store);
    return new SettingsService(store, new MockStartupService(), folderManager);
}
```

- [ ] **Step 6: Build and run all tests**

Run: `dotnet build Launchbox.csproj -p:Platform=x64 && dotnet test Launchbox.Tests/Launchbox.Tests.csproj -p:Platform=x64`
Expected: Build succeeded, all tests pass

- [ ] **Step 7: Commit**

```bash
git add Services/SettingsService.cs MainWindow.xaml.cs Launchbox.Tests/
git commit -m "feat: integrate ShortcutFolderManager into SettingsService"
```

---

## Phase 3: Multi-Folder Loading & Display

### Task 9: Add FolderLabel to AppItem and create AppItemGroup

**Files:**
- Modify: `Models/AppItem.cs`
- Create: `Models/AppItemGroup.cs`
- Modify: `Launchbox.Tests/Launchbox.Tests.csproj` (file-link)

- [ ] **Step 1: Add FolderLabel and FolderPath to AppItem**

In `Models/AppItem.cs`, add:

```csharp
public string FolderLabel { get; init; } = string.Empty;

/// <summary>
/// Stable identity for grouping — the folder's original path.
/// Labels can be duplicated across folders; paths cannot.
/// </summary>
public string FolderPath { get; init; } = string.Empty;
```

- [ ] **Step 2: Create AppItemGroup.cs**

```csharp
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace Launchbox.Models;

/// <summary>
/// A group of AppItems for a single folder. Supports collapse/expand by mutating the
/// inner ObservableCollection (clear/restore) rather than returning new group instances.
/// WinUI 3's CollectionViewSource hides groups with 0 items, so collapsed groups retain
/// a single invisible placeholder to keep the header visible.
///
/// THREADING: All public methods that mutate the collection (ApplyFilter, IsCollapsed setter)
/// must be called from the UI thread only — ObservableCollection is not thread-safe.
/// </summary>
public class AppItemGroup : ObservableCollection<AppItem>
{
    /// <summary>Sentinel item kept in collapsed groups so CVS doesn't hide the header.</summary>
    private static readonly AppItem CollapsedPlaceholder = new() { Name = string.Empty, Path = string.Empty };

    public string Label { get; }

    /// <summary>
    /// Stable identity for this group — the folder's original path.
    /// Used for collapse-state matching when labels are duplicated or filtered copies are in play.
    /// </summary>
    public string FolderPath { get; }

    // Backup of the full item list — populated on construction, used for expand/restore
    private readonly List<AppItem> _allItems;

    // Tracks the active filter text so expand can re-apply it
    private string? _activeFilter;

    private bool _isCollapsed;
    public bool IsCollapsed
    {
        get => _isCollapsed;
        set
        {
            if (_isCollapsed != value)
            {
                _isCollapsed = value;
                OnPropertyChanged(new PropertyChangedEventArgs(nameof(IsCollapsed)));
                ApplyCollapseState();
            }
        }
    }

    public AppItemGroup(string label, string folderPath, IEnumerable<AppItem> items) : base(items)
    {
        Label = label;
        FolderPath = folderPath;
        _allItems = [.. this]; // snapshot at construction
    }

    /// <summary>
    /// Replaces the visible items with a filtered subset. Preserves the backup for expand.
    /// For collapsed groups: stores the filter but does not change visible items (placeholder stays).
    /// If the filter produces 0 matches and the group is not collapsed, the group is emptied
    /// (CVS will hide the header, which is correct — no matching items means no group to show).
    /// </summary>
    public void ApplyFilter(string? filterText)
    {
        _activeFilter = filterText;

        if (_isCollapsed) return; // collapsed groups keep placeholder; filter applied on expand

        var source = string.IsNullOrEmpty(filterText)
            ? _allItems
            : _allItems.Where(a => a.Name.Contains(filterText, StringComparison.OrdinalIgnoreCase)).ToList();

        // Minimize churn: only replace if the set actually changed
        if (source.SequenceEqual(this)) return;

        Clear();
        foreach (var item in source) Add(item);
    }

    private void ApplyCollapseState()
    {
        Clear();
        if (_isCollapsed)
        {
            // WinUI 3 CVS hides 0-item groups — keep one placeholder so header stays visible
            Add(CollapsedPlaceholder);
        }
        else
        {
            // Expand: re-apply the active filter (don't restore unfiltered _allItems)
            var source = string.IsNullOrEmpty(_activeFilter)
                ? _allItems
                : _allItems.Where(a => a.Name.Contains(_activeFilter, StringComparison.OrdinalIgnoreCase)).ToList();

            foreach (var item in source) Add(item);
        }
    }
}
```

- [ ] **Step 3: Add file link**

```xml
<Compile Include="..\Models\AppItemGroup.cs" Link="Models\AppItemGroup.cs" />
```

- [ ] **Step 4: Build both projects**

Run: `dotnet build Launchbox.csproj -p:Platform=x64 && dotnet build Launchbox.Tests/Launchbox.Tests.csproj -p:Platform=x64`
Expected: Build succeeded

- [ ] **Step 5: Commit**

```bash
git add Models/AppItem.cs Models/AppItemGroup.cs Launchbox.Tests/Launchbox.Tests.csproj
git commit -m "feat: add FolderLabel to AppItem and create AppItemGroup"
```

---

### Task 10: Update MainViewModel for multi-folder loading (TDD)

**Files:**
- Modify: `ViewModels/MainViewModel.cs`
- Modify: `Launchbox.Tests/MainViewModelTests.cs`

This is the core logic change. Build with TDD.

- [ ] **Step 1: Write test — LoadAppsAsync loads from multiple folders**

```csharp
[Fact]
public async Task LoadAppsAsync_LoadsFromMultipleFolders()
{
    var folder2 = Path.Combine("C:", "Games");
    _fileSystem.CreateDirectory(folder2);
    _fileSystem.AddFile(Path.Combine(_shortcutFolder, "Alpha.lnk"));
    _fileSystem.AddFile(Path.Combine(folder2, "Beta.lnk"));

    // Configure two folders via the store (JSON)
    var folders = new[]
    {
        new { Path = _shortcutFolder, Label = "Shortcuts", Order = 0 },
        new { Path = folder2, Label = "Games", Order = 1 }
    };
    settingsStore.SetValue("ShortcutFolders", System.Text.Json.JsonSerializer.Serialize(folders));

    var vm = CreateViewModel();
    await vm.LoadAppsAsync();

    Assert.Equal(2, vm.Apps.Count);
    Assert.Contains(vm.Apps, a => a.Name == "Alpha" && a.FolderLabel == "Shortcuts");
    Assert.Contains(vm.Apps, a => a.Name == "Beta" && a.FolderLabel == "Games");
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test Launchbox.Tests/Launchbox.Tests.csproj --filter "FullyQualifiedName~LoadAppsAsync_LoadsFromMultipleFolders" -p:Platform=x64`
Expected: FAIL

- [ ] **Step 3: Update LoadAppsAsync to iterate folders**

In `MainViewModel.LoadAppsAsync`, replace single-folder enumeration with:

```csharp
var folders = _settingsService.GetShortcutFolders();
List<AppItem> localAppItems = [];
List<string> allFiles = []; // Accumulate all files for icon cache pruning

foreach (var folder in folders.OrderBy(f => f.Order))
{
    var files = await Task.Run(() =>
        _shortcutService.GetShortcutFiles(
            Environment.ExpandEnvironmentVariables(folder.Path),
            Constants.ALLOWED_EXTENSIONS), ct);

    if (files != null)
    {
        allFiles.AddRange(files);
        foreach (var file in files)
        {
            try
            {
                var name = Path.GetFileNameWithoutExtension(file);
                localAppItems.Add(new AppItem
                {
                    Name = name,
                    Path = file,
                    FolderLabel = folder.Label,
                    FolderPath = folder.Path
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"Failed to process {file}: {ex.Message}");
            }
        }
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test Launchbox.Tests/Launchbox.Tests.csproj --filter "FullyQualifiedName~LoadAppsAsync_LoadsFromMultipleFolders" -p:Platform=x64`
Expected: PASS

- [ ] **Step 5: Add GroupedApps property and populate it in LoadAppsAsync**

Add the `GroupedApps` property to `MainViewModel`:

```csharp
public ObservableCollection<AppItemGroup> GroupedApps { get; } = [];

public bool CollapsibleGroupsEnabled => _settingsService.CollapsibleGroups;
```

At the end of `LoadAppsAsync`, after collecting all items on the background thread, **dispatch UI mutations onto the UI thread** (ObservableCollection cannot be modified from a background thread):

```csharp
// Prune icon cache for removed shortcuts (existing behavior — must not be dropped)
await Task.Run(() => _iconService.PruneCache(allFiles), ct);

// Sort merged mode alphabetically (still on background thread — just sorting a local list)
localAppItems.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));

// Build grouped data structure on background thread — group by FolderPath (unique identity),
// display FolderLabel (can be duplicated across folders)
var groupedData = localAppItems
    .GroupBy(a => a.FolderPath)
    .OrderBy(g => folders.FirstOrDefault(f => f.Path == g.Key)?.Order ?? int.MaxValue)
    .Select(g =>
    {
        var folder = folders.FirstOrDefault(f => f.Path == g.Key);
        var label = folder?.Label ?? System.IO.Path.GetFileName(g.Key) ?? g.Key;
        return new AppItemGroup(label, g.Key, g.OrderBy(a => a.Name));
    })
    .ToList();

// Switch to UI thread for ObservableCollection mutations
await _dispatcher.EnqueueAsync(() =>
{
    // Use ReplaceAll for BulkObservableCollection to avoid per-item notifications
    Apps.ReplaceAll(localAppItems);
    IsEmpty = Apps.Count == 0;

    GroupedApps.Clear();
    foreach (var group in groupedData)
    {
        GroupedApps.Add(group);
    }

    OnPropertyChanged(nameof(IsMergedModeVisible));
    OnPropertyChanged(nameof(IsGroupedModeVisible));
});
```

**Important:** The existing `LoadAppsAsync` already dispatches `Apps` updates on the UI thread (via `_dispatcher.EnqueueAsync`). Ensure the `GroupedApps` updates happen inside the same dispatch block.

- [ ] **Step 6: Add filter support via in-place mutation**

> **Review fix:** Do NOT create new `AppItemGroup` instances for filtering — this causes full `CollectionViewSource` churn, scroll position reset, and focus loss. Instead, mutate the inner `ObservableCollection` of each stable `GroupedApps` group via `AppItemGroup.ApplyFilter()`.

Add a helper method that applies the current filter to all groups:

```csharp
private void ApplyGroupedFilter()
{
    foreach (var group in GroupedApps)
    {
        group.ApplyFilter(_filterText);
    }
}
```

**Important:** Update the `FilterText` setter to call `ApplyGroupedFilter()` alongside the existing `OnPropertyChanged(nameof(FilteredApps))`. Without this, typing in the search box won't filter the grouped GridView.

- [ ] **Step 7: Write and implement remaining tests**

Add tests for:
- `LoadAppsAsync_MergedMode_SortsAlphabetically`
- `LoadAppsAsync_GroupedMode_BuildsGroupedApps`
- `LoadAppsAsync_GroupedMode_GroupsOrderedByFolderOrder`
- `ApplyFilter_HidesNonMatchingItemsInGroups`
- `FilteredApps_FilterAcrossMultipleFolders`
- `LoadAppsAsync_DuplicateNames_BothAppear`
- `LoadAppsAsync_EmptyFolder_SkippedInMergedMode`
- `OpenShortcutsFolder_MultipleFolders_OpensFirst`

- [ ] **Step 8: Update OpenShortcutsFolder**

Replace `_settingsService.ShortcutsPath` with:

```csharp
var folders = _settingsService.GetShortcutFolders();
var firstFolder = folders.OrderBy(f => f.Order).FirstOrDefault();
var folderPath = firstFolder != null
    ? Environment.ExpandEnvironmentVariables(firstFolder.Path)
    : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "Shortcuts");
```

- [ ] **Step 9: Update SettingsService_PropertyChanged**

Add handler for `"ShortcutFolders"` to trigger `LoadAppsAsync`, alongside the existing `ShortcutsPath` handler. The existing `SettingsService_PropertyChanged` uses `if/else if` blocks (not a `switch`), so follow the same pattern:

```csharp
else if (e.PropertyName == "ShortcutFolders")
{
    _ = LoadAppsAsync();
}
else if (e.PropertyName == "CollapsibleGroups")
{
    OnPropertyChanged(nameof(CollapsibleGroupsEnabled));
}
```

- [ ] **Step 10: Run all tests**

Run: `dotnet test Launchbox.Tests/Launchbox.Tests.csproj -p:Platform=x64`
Expected: All pass

- [ ] **Step 11: Commit**

```bash
git add ViewModels/MainViewModel.cs Launchbox.Tests/MainViewModelTests.cs
git commit -m "feat: multi-folder loading with merged/grouped modes"
```

---

## Phase 4: Settings UI

### Task 11: Update SettingsViewModel for folder management

**Files:**
- Modify: `ViewModels/SettingsViewModel.cs`

- [ ] **Step 1: Add folder list and commands**

```csharp
public ObservableCollection<ShortcutFolder> Folders { get; } = [];

[RelayCommand]
private async Task AddFolderAsync()
{
    try
    {
        var path = await _filePickerService.PickSingleFolderAsync();
        if (!string.IsNullOrEmpty(path))
        {
            if (_settingsService.AddShortcutFolder(path))
            {
                RefreshFolders();
            }
        }
    }
    catch (Exception ex)
    {
        System.Diagnostics.Trace.WriteLine($"Failed to add folder: {ex.Message}");
    }
}

[RelayCommand]
private void RemoveFolder(int order)
{
    if (_settingsService.RemoveShortcutFolder(order))
    {
        RefreshFolders();
    }
}

[RelayCommand]
private void MoveFolderUp(int order)
{
    if (order > 0 && _settingsService.ReorderShortcutFolder(order, order - 1))
    {
        RefreshFolders();
    }
}

[RelayCommand]
private void MoveFolderDown(int order)
{
    var folders = _settingsService.GetShortcutFolders();
    if (order < folders.Count - 1 && _settingsService.ReorderShortcutFolder(order, order + 1))
    {
        RefreshFolders();
    }
}

// ViewModel stays platform-agnostic — no WinUI types (ContentDialog, XamlRoot).
// The rename UI lives in SettingsWindow.xaml.cs code-behind.
public void ApplyRename(int order, string newLabel)
{
    if (_settingsService.RenameShortcutFolder(order, newLabel))
    {
        RefreshFolders();
    }
}

private void RefreshFolders()
{
    Folders.Clear();
    foreach (var f in _settingsService.GetShortcutFolders())
    {
        Folders.Add(f);
    }
}
```

- [ ] **Step 2: Add view mode and collapsible properties**

```csharp
public FolderViewMode SelectedViewMode
{
    get => _settingsService.FolderViewMode;
    set => _settingsService.FolderViewMode = value;
}

public bool CollapsibleGroups
{
    get => _settingsService.CollapsibleGroups;
    set => _settingsService.CollapsibleGroups = value;
}

// LocalizedOption list for view mode ComboBox (follows SelectedGridSize pattern)
public IReadOnlyList<LocalizedOption> ViewModeOptions { get; } =
[
    new("Merged", Localization.GetString("Settings_ViewMode_Merged")),
    new("Grouped", Localization.GetString("Settings_ViewMode_Grouped"))
];

// Wrapper property for ComboBox SelectedItem binding (matches SelectedGridSize pattern)
public LocalizedOption SelectedViewModeOption
{
    get => ViewModeOptions.FirstOrDefault(o => o.Value == _settingsService.FolderViewMode.ToString())
        ?? ViewModeOptions[0];
    set
    {
        if (value != null && Enum.TryParse<FolderViewMode>(value.Value, out var mode))
        {
            _settingsService.FolderViewMode = mode;
        }
    }
}
```

Relay `PropertyChanged` from `SettingsService` for `FolderViewMode` → raise `OnPropertyChanged(nameof(SelectedViewModeOption))` in the existing PropertyChanged handler.

- [ ] **Step 3: Add rename handler in SettingsWindow.xaml.cs (code-behind)**

The rename UI uses `ContentDialog` which requires `XamlRoot` — this belongs in the View, not the ViewModel (MVVM boundary). Add to `SettingsWindow.xaml.cs`:

```csharp
private async void RenameFolder_Click(object sender, RoutedEventArgs e)
{
    if (sender is not Button { DataContext: ShortcutFolder folder }) return;

    var textBox = new TextBox { Text = folder.Label, PlaceholderText = folder.Label };
    var dialog = new ContentDialog
    {
        Title = Localization.GetString("Settings_RenameFolder_Title"),
        PrimaryButtonText = Localization.GetString("Settings_RenameFolder_OK"),
        CloseButtonText = Localization.GetString("Settings_RenameFolder_Cancel"),
        Content = textBox,
        XamlRoot = Content.XamlRoot
    };

    var result = await dialog.ShowAsync();
    if (result == ContentDialogResult.Primary && !string.IsNullOrWhiteSpace(textBox.Text))
    {
        ViewModel.ApplyRename(folder.Order, textBox.Text);
    }
}
```

- [ ] **Step 4: Initialize folders in constructor**

Call `RefreshFolders()` at end of constructor and relay `PropertyChanged` for `ShortcutFolders` from `SettingsService`.

- [ ] **Step 5: Build and run tests**

> **Review fix:** Do NOT remove `ShortcutsPath` or `BrowseFolderAsync` here — `SettingsWindow.xaml` still binds to them until Task 12 replaces the XAML. Legacy removal happens in Task 12 Step 5 after the UI migration.

Run: `dotnet build Launchbox.csproj -p:Platform=x64 && dotnet test Launchbox.Tests/Launchbox.Tests.csproj -p:Platform=x64`
Expected: Build succeeded, all tests pass

- [ ] **Step 6: Commit**

```bash
git add ViewModels/SettingsViewModel.cs SettingsWindow.xaml.cs
git commit -m "feat: add folder management commands to SettingsViewModel"
```

---

### Task 12: Update SettingsWindow.xaml for folder list UI

**Files:**
- Modify: `SettingsWindow.xaml`
- Modify: `Strings/*/Resources.resw` (8 files)

- [ ] **Step 1: Name the content StackPanel for DataTemplate bindings**

The SettingsWindow has no named root element usable from DataTemplates. Add `x:Name="SettingsContent"` to the `<StackPanel>` at line 26 (inside `<ScrollViewer>`):

```xml
<StackPanel x:Name="SettingsContent" Padding="24" Spacing="20" HorizontalAlignment="Left" MaxWidth="500">
```

Also add `xmlns:models="using:Launchbox.Models"` to the `<Window>` tag if not already present.

- [ ] **Step 2: Replace shortcuts section with folder list**

Replace the existing Shortcuts section (TextBox + Browse button) with:

```xml
<!-- Shortcut Folders -->
<TextBlock x:Uid="Settings_FolderList" Style="{ThemeResource SubtitleTextBlockStyle}" Margin="0,16,0,8" />

<ListView ItemsSource="{x:Bind ViewModel.Folders}" SelectionMode="None" MaxHeight="200">
  <ListView.ItemTemplate>
    <DataTemplate x:DataType="models:ShortcutFolder">
      <Grid ColumnDefinitions="*,Auto,Auto,Auto,Auto" Padding="4" ColumnSpacing="4">
        <StackPanel>
          <TextBlock Text="{x:Bind Label}" FontWeight="SemiBold" />
          <TextBlock Text="{x:Bind Path}" FontSize="12" Opacity="0.6"
                     TextTrimming="CharacterEllipsis"
                     ToolTipService.ToolTip="{x:Bind Path}" />
        </StackPanel>
        <Button Grid.Column="1" x:Uid="Settings_MoveUp"
                Command="{Binding ElementName=SettingsContent, Path=DataContext.ViewModel.MoveFolderUpCommand}"
                CommandParameter="{x:Bind Order}" Padding="4" />
        <Button Grid.Column="2" x:Uid="Settings_MoveDown"
                Command="{Binding ElementName=SettingsContent, Path=DataContext.ViewModel.MoveFolderDownCommand}"
                CommandParameter="{x:Bind Order}" Padding="4" />
        <Button Grid.Column="3" x:Uid="Settings_RenameFolder"
                Click="RenameFolder_Click" Padding="4" />
        <Button Grid.Column="4" x:Uid="Settings_RemoveFolder"
                Command="{Binding ElementName=SettingsContent, Path=DataContext.ViewModel.RemoveFolderCommand}"
                CommandParameter="{x:Bind Order}" Padding="4" />
      </Grid>
    </DataTemplate>
  </ListView.ItemTemplate>
</ListView>

<Button x:Uid="Settings_AddFolder" Command="{x:Bind ViewModel.AddFolderCommand}" Margin="0,8,0,0" />
```

**Important:** In the `SettingsWindow.xaml.cs` constructor, add `SettingsContent.DataContext = this;` after `InitializeComponent()` so that `{Binding}` inside the DataTemplate can reach `ViewModel` through the named element.

- [ ] **Step 3: Add view mode controls**

Below the folder list:

```xml
<!-- View Mode -->
<TextBlock x:Uid="Settings_ViewMode" Style="{ThemeResource SubtitleTextBlockStyle}" Margin="0,16,0,8" />
<ComboBox ItemsSource="{x:Bind ViewModel.ViewModeOptions}"
          SelectedItem="{x:Bind ViewModel.SelectedViewModeOption, Mode=TwoWay}"
          DisplayMemberPath="DisplayName" Width="150" />
<ToggleSwitch x:Uid="Settings_CollapsibleGroups"
              IsOn="{x:Bind ViewModel.CollapsibleGroups, Mode=TwoWay}"
              Margin="0,8,0,0" />
```

- [ ] **Step 4: Add all localized strings**

Add to all 8 `Strings/*/Resources.resw` files the strings from the spec Section 5 (14 strings).

- [ ] **Step 5: Remove legacy ShortcutsPath setter and BrowseFolderCommand**

> **Review fix:** Now that the UI has been migrated (Task 11 added folder commands, Task 12 replaced the Shortcuts TextBox/Browse button), it is safe to remove the legacy API surface.

In `Services/SettingsService.cs`:
- Remove the `ShortcutsPath` setter (keep the getter as a backward-compatible read-only property)
- The getter now delegates to `_folderManager.GetFolders().FirstOrDefault()?.Path`

In `ViewModels/SettingsViewModel.cs`:
- Remove the old `ShortcutsPath` passthrough property
- Remove `BrowseFolderCommand` / `BrowseFolderAsync` (replaced by `AddFolderCommand`)

In `SettingsWindow.xaml`:
- Verify the old Shortcuts TextBox and Browse button have been replaced by the folder list (Step 2)

Update any remaining test code that references the removed setter.

- [ ] **Step 6: Build and run full test suite**

> **Review fix:** This step removes a cross-cutting API (`ShortcutsPath` setter) — build alone is insufficient. Run the full test suite to catch any remaining references.

Run: `dotnet build Launchbox.csproj -p:Platform=x64 && dotnet test Launchbox.Tests/Launchbox.Tests.csproj -p:Platform=x64`
Expected: Build succeeded, all tests pass

- [ ] **Step 7: Commit**

```bash
git add SettingsWindow.xaml SettingsWindow.xaml.cs Services/SettingsService.cs ViewModels/SettingsViewModel.cs Strings/ Launchbox.Tests/
git commit -m "feat: add folder management UI, remove legacy ShortcutsPath setter"
```

---

## Phase 5: Grouped Display in Main Window

### Task 13: Add grouped GridView and CollectionViewSource to MainWindow

**Files:**
- Modify: `MainWindow.xaml`
- Modify: `MainWindow.xaml.cs`

- [ ] **Step 1: Extract shared styles to Grid.Resources**

The existing `AppGrid` defines both an `ItemContainerStyle` and an `ItemTemplate` inline. The `ItemTemplate` references `ElementName=AppGrid` for `Tag.ItemWidth`/`Tag.ItemHeight`/`Tag.IconSize` bindings, making it tightly coupled to `AppGrid`. To share between both GridViews:

1. Extract the `ItemContainerStyle` to `<Grid.Resources>` with `x:Key="AppItemContainerStyle"`:
```xml
<Style x:Key="AppItemContainerStyle" TargetType="GridViewItem">
    <Setter Property="CornerRadius" Value="8"/>
    <Setter Property="Margin" Value="4"/>
    <Setter Property="Padding" Value="4"/>
    <Setter Property="HorizontalContentAlignment" Value="Stretch"/>
    <Setter Property="VerticalContentAlignment" Value="Stretch"/>
</Style>
```

2. Do NOT extract the `ItemTemplate` — it uses `ElementName=AppGrid` bindings for dynamic sizing via `Tag`. Instead, duplicate the DataTemplate inline on both GridViews but update the `GroupedAppGrid` copy to use `ElementName=GroupedAppGrid`. Set the same `Tag` property on `GroupedAppGrid` in code-behind (copy the `Tag` assignment from where `AppGrid.Tag` is set).

3. Update `AppGrid` to use the shared style: `ItemContainerStyle="{StaticResource AppItemContainerStyle}"`.

- [ ] **Step 2: Add CollectionViewSource resource**

In `MainWindow.xaml`, add to `Grid.Resources` (in `RootGrid`):

```xml
<CollectionViewSource x:Name="GroupedAppsSource" x:Key="GroupedAppsSource"
                      Source="{x:Bind ViewModel.GroupedApps, Mode=OneWay}"
                      IsSourceGrouped="True" />
```

> **Review fix:** Bind to the stable `GroupedApps` collection, not `FilteredGroupedApps`. Filtering and collapse are handled by mutating each group's inner `ObservableCollection` via `ApplyFilter()` / `IsCollapsed` setter. This avoids full-source churn and scroll position reset.

- [ ] **Step 3: Add second GridView for grouped mode**

Below the existing `GridView` (merged), add:

```xml
<GridView x:Name="GroupedAppGrid"
          Grid.Row="0"
          Padding="20"
          Background="Transparent"
          SelectionMode="None"
          IsItemClickEnabled="True"
          ItemsSource="{Binding Source={StaticResource GroupedAppsSource}}"
          Visibility="{x:Bind ViewModel.IsGroupedModeVisible, Mode=OneWay, Converter={StaticResource BooleanToVisibilityConverter}}"
          helpers:ListViewBaseExtensions.Command="{x:Bind ViewModel.LaunchAppCommand}"
          ItemContainerStyle="{StaticResource AppItemContainerStyle}"
          ScrollViewer.VerticalScrollBarVisibility="Hidden"
          ScrollViewer.VerticalScrollMode="Enabled"
          ScrollViewer.IsVerticalRailEnabled="True">

  <!-- Inline DataTemplate — same as AppGrid but references ElementName=GroupedAppGrid for Tag bindings.
       Collapse placeholder items (empty Name) so they take zero visual space. -->
  <GridView.ItemTemplate>
    <DataTemplate x:DataType="models:AppItem">
      <StackPanel Width="{Binding Tag.ItemWidth, ElementName=GroupedAppGrid}"
                  Height="{Binding Tag.ItemHeight, ElementName=GroupedAppGrid}"
                  Spacing="4"
                  HorizontalAlignment="Center"
                  VerticalAlignment="Center"
                  ToolTipService.ToolTip="{x:Bind Name}"
                  AutomationProperties.Name="{x:Bind Name}"
                  Visibility="{x:Bind Name, Mode=OneWay, Converter={StaticResource EmptyStringToCollapsedConverter}}">
        <Image Source="{x:Bind (media:ImageSource)Icon, Mode=OneWay}"
               Width="{Binding Tag.IconSize, ElementName=GroupedAppGrid}"
               Height="{Binding Tag.IconSize, ElementName=GroupedAppGrid}"
               Margin="0,10,0,0"
               AutomationProperties.AccessibilityView="Raw"
               x:Phase="1"/>
        <TextBlock Text="{x:Bind Name}"
                   TextAlignment="Center"
                   TextWrapping="Wrap"
                   MaxLines="3"
                   TextTrimming="CharacterEllipsis"
                   FontSize="12"
                   FontWeight="SemiBold"/>
      </StackPanel>
    </DataTemplate>
  </GridView.ItemTemplate>

  <GridView.GroupStyle>
    <GroupStyle>
      <GroupStyle.HeaderTemplate>
        <DataTemplate x:DataType="models:AppItemGroup">
          <Grid Padding="8,12,8,4">
            <TextBlock Text="{x:Bind Label}"
                       Style="{ThemeResource SubtitleTextBlockStyle}" />
          </Grid>
        </DataTemplate>
      </GroupStyle.HeaderTemplate>
    </GroupStyle>
  </GridView.GroupStyle>
</GridView>
```

**Important:** In `MainWindow.xaml.cs`, wherever `AppGrid.Tag` is assigned (the sizing object with `ItemWidth`, `ItemHeight`, `IconSize`), also assign the same value to `GroupedAppGrid.Tag`.

- [ ] **Step 4: Wrap both GridViews to handle empty state**

The existing `AppGrid` has `Visibility` bound to `IsEmpty` (inverted). Both GridViews must be hidden when `IsEmpty` is true. Replace the existing `AppGrid` visibility with `IsMergedModeVisible`, and set the new `GroupedAppGrid` to `IsGroupedModeVisible`:

```xml
<!-- Existing merged GridView — change Visibility to: -->
Visibility="{x:Bind ViewModel.IsMergedModeVisible, Mode=OneWay, Converter={StaticResource BooleanToVisibilityConverter}}"

<!-- Grouped GridView (already added in Step 2) — Visibility is: -->
Visibility="{x:Bind ViewModel.IsGroupedModeVisible, Mode=OneWay, Converter={StaticResource BooleanToVisibilityConverter}}"
```

- [ ] **Step 5: Add mode helper properties to MainViewModel**

```csharp
public bool IsMergedMode => _settingsService.FolderViewMode == FolderViewMode.Merged;
public bool IsGroupedMode => _settingsService.FolderViewMode == FolderViewMode.Grouped;

// Combine view mode with empty state — GridView hidden when no apps loaded
public bool IsMergedModeVisible => IsMergedMode && !IsEmpty;
public bool IsGroupedModeVisible => IsGroupedMode && !IsEmpty;
```

Fire `PropertyChanged` for all four properties when `FolderViewMode` changes in `SettingsService_PropertyChanged`, and for `IsMergedModeVisible`/`IsGroupedModeVisible` when `IsEmpty` changes (after `LoadAppsAsync` completes).

Fire `PropertyChanged` for all four properties when `FolderViewMode` changes in `SettingsService_PropertyChanged`. Also fire `OnPropertyChanged(nameof(IsMergedModeVisible))` and `OnPropertyChanged(nameof(IsGroupedModeVisible))` immediately after setting `IsEmpty` inside the `_dispatcher.EnqueueAsync` block in `LoadAppsAsync`.

- [ ] **Step 6: Build to verify**

Run: `dotnet build Launchbox.csproj -p:Platform=x64`
Expected: Build succeeded

- [ ] **Step 7: Commit**

```bash
git add MainWindow.xaml MainWindow.xaml.cs ViewModels/MainViewModel.cs
git commit -m "feat: add grouped GridView with CollectionViewSource and view-mode switching"
```

---

### Task 14: Implement collapsible group headers

**Files:**
- Modify: `MainWindow.xaml` (group header template)
- Modify: `ViewModels/MainViewModel.cs` (if needed for collapse state)

- [ ] **Step 1: Create CollapseChevronConverter and EmptyStringToCollapsedConverter**

Create `Helpers/CollapseChevronConverter.cs`:

```csharp
using Microsoft.UI.Xaml.Data;
using System;

namespace Launchbox.Helpers;

public class CollapseChevronConverter : IValueConverter
{
    // ChevronRight (collapsed) vs ChevronDown (expanded) — standard Windows tree-view convention
    public object Convert(object value, Type targetType, object parameter, string language) =>
        value is true ? "\uE76C" : "\uE76E";

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}
```

Create `Helpers/EmptyStringToCollapsedConverter.cs`:

```csharp
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using System;

namespace Launchbox.Helpers;

/// <summary>
/// Hides collapsed-group placeholder items (empty Name) so they take zero visual space.
/// </summary>
public class EmptyStringToCollapsedConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language) =>
        value is string s && !string.IsNullOrEmpty(s) ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}
```

Register both in `MainWindow.xaml` resources:

```xml
<helpers:CollapseChevronConverter x:Key="CollapseChevronConverter" />
<helpers:EmptyStringToCollapsedConverter x:Key="EmptyStringToCollapsedConverter" />
```

- [ ] **Step 2: Add collapse toggle to group header**

Update the group header template. The chevron visibility binds to `CollapsibleGroupsEnabled` on the `MainViewModel`, accessed through `RootGrid` (which already exists with `x:Name="RootGrid"` in MainWindow.xaml). The `Tapped` handler toggles `IsCollapsed` on the **stable `GroupedApps` object**, not a temporary copy:

```xml
<Grid ColumnDefinitions="Auto,*" Padding="8,12,8,4" Tapped="GroupHeader_Tapped">
  <FontIcon Glyph="{x:Bind IsCollapsed, Mode=OneWay, Converter={StaticResource CollapseChevronConverter}}"
            FontSize="12"
            Visibility="{Binding ElementName=RootGrid, Path=DataContext.ViewModel.CollapsibleGroupsEnabled,
                         Converter={StaticResource BooleanToVisibilityConverter}}" />
  <TextBlock Grid.Column="1" Text="{x:Bind Label}"
             Style="{ThemeResource SubtitleTextBlockStyle}" />
</Grid>
```

**Note:** `RootGrid` already exists in `MainWindow.xaml` (line 14). Ensure `RootGrid.DataContext = this;` is set in `MainWindow.xaml.cs` constructor so `{Binding}` can reach `ViewModel`.

- [ ] **Step 3: Add Tapped handler in code-behind**

The handler toggles `IsCollapsed` on the **original `GroupedApps` item** (the setter mutates the inner collection in place):

```csharp
private void GroupHeader_Tapped(object sender, TappedRoutedEventArgs e)
{
    if (sender is FrameworkElement { DataContext: AppItemGroup group }
        && ViewModel.CollapsibleGroupsEnabled)
    {
        // Match by FolderPath (stable identity) — labels can be duplicated, paths cannot
        var stableGroup = ViewModel.GroupedApps.FirstOrDefault(g => g.FolderPath == group.FolderPath);
        if (stableGroup != null)
        {
            // IsCollapsed setter mutates the inner ObservableCollection in place —
            // CollectionViewSource observes the change automatically
            stableGroup.IsCollapsed = !stableGroup.IsCollapsed;
        }
    }
}
```

- [ ] **Step 4: Verify collapse uses in-place mutation (no FilteredGroupedApps needed)**

> **Review fix:** Collapse/expand is handled by `AppItemGroup.IsCollapsed` setter, which calls `ApplyCollapseState()` to clear/restore the inner `ObservableCollection`. Since `CollectionViewSource` binds to the stable `GroupedApps` collection, mutations are observed automatically — no separate `FilteredGroupedApps` property is needed.

**Key design:** Both collapse state and filter state live on the stable `GroupedApps` objects (populated in `LoadAppsAsync`). The `IsCollapsed` setter and `ApplyFilter()` method mutate the inner collection in place, so WinUI 3's `CollectionViewSource` observes changes without full-source replacement. This preserves scroll position and avoids the empty-group-vanishing bug.

- [ ] **Step 5: Build and test**

Run: `dotnet build Launchbox.csproj -p:Platform=x64 && dotnet test Launchbox.Tests/Launchbox.Tests.csproj -p:Platform=x64`
Expected: Build succeeded, all tests pass

- [ ] **Step 6: Commit**

```bash
git add MainWindow.xaml MainWindow.xaml.cs ViewModels/MainViewModel.cs Helpers/CollapseChevronConverter.cs
git commit -m "feat: add collapsible group headers with chevron toggle"
```

---

## Phase 6: Final Integration & Cleanup

### Task 15: Format, build, and run full test suite

**Files:** All modified files

- [ ] **Step 1: Run dotnet format**

Run: `dotnet format Launchbox.sln`

- [ ] **Step 2: Build both projects**

Run: `dotnet build Launchbox.csproj -p:Platform=x64 && dotnet build Launchbox.Tests/Launchbox.Tests.csproj -p:Platform=x64`
Expected: Build succeeded, 0 errors, 0 warnings (or only known warnings)

- [ ] **Step 3: Run full test suite**

Run: `dotnet test Launchbox.Tests/Launchbox.Tests.csproj -p:Platform=x64`
Expected: All tests pass

- [ ] **Step 4: Commit any formatting fixes**

```bash
git add -A
git commit -m "style: format all files"
```

---

### Task 16: Update TODO.md

**Files:**
- Modify: `TODO.md`

- [ ] **Step 1: Mark features as complete**

Change the two feature items from `[ ]` to `[x]`:
- `[ ] Window height auto-sizing: detect work area and clamp/resize on activation`
- `[ ] Multi-folder shortcut sources: support multiple folders instead of just one`

- [ ] **Step 2: Commit**

```bash
git add TODO.md
git commit -m "docs: mark window auto-sizing and multi-folder features as complete"
```
