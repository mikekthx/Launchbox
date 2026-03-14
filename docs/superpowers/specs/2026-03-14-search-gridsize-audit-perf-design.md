# Design: Search Bar, Grid Size, TODO Audit, Performance Test

**Date:** 2026-03-14
**Status:** Approved

---

## Overview

Four enhancements to the Launchbox WinUI 3 app launcher:

1. **Search/filter bar** — live filter in the main window
2. **Configurable grid size** — Small/Medium/Large presets in Settings
3. **TODO audit** — build and static analysis pass
4. **Performance test** — load test for large shortcut collections

---

## Feature 1: Search/Filter Bar

### Decision

Option C: auto-focused overlay at the bottom of the main window. The TextBox sits subtly below the grid, receives focus when the window opens, and filters the grid live as the user types. Esc clears the filter.

### Architecture

`MainViewModel` owns all filter state. `AppGrid` binds to `FilteredApps` instead of `Apps`.

**New properties on `MainViewModel`:**

| Property | Type | Description |
|----------|------|-------------|
| `FilterText` | `string` | Two-way bound to TextBox. Setter calls `OnPropertyChanged(nameof(FilteredApps))`, `OnPropertyChanged(nameof(HasNoMatches))`. |
| `FilteredApps` | `IEnumerable<AppItem>` | Returns `Apps` filtered by `Name.Contains(FilterText, OrdinalIgnoreCase)` when `FilterText` is non-empty; returns `Apps` otherwise. |
| `HasNoMatches` | `bool` | `FilterText` is non-empty AND `FilteredApps` is empty. Used for "No matches" empty state. |
| `HasNoApps` | `bool` | `Apps` is empty. Used for "No shortcuts found" empty state (already implied, may be extracted from existing logic). |

### Components

- **`MainWindow.xaml`** — `TextBox` below `AppGrid` (or below the scroll viewer), `PlaceholderText="type to filter..."`, `x:Bind ViewModel.FilterText, Mode=TwoWay`. Esc key handler in code-behind clears `FilterText`. Window `Activated` handler focuses the TextBox.
- **`MainViewModel.cs`** — filter logic as described above. No debounce needed (in-memory collection).
- **Empty state** — a `TextBlock` or `StackPanel` overlay conditionally visible via `BooleanToVisibilityConverter`:
  - `HasNoApps` → "No shortcuts found. Open Settings to set your folder."
  - `HasNoMatches` → "No matches for '{FilterText}'."

### Data Flow

```
User types → FilterText changes → FilteredApps recomputed → AppGrid updates (live)
Esc pressed → FilterText = "" → FilteredApps = Apps → AppGrid restored
Window shown → TextBox.Focus() called in Activated handler
```

### Error Handling

No errors expected. Filter is pure in-memory computation.

### Testing

- `FilterText_WhenSet_FiltersAppsByName` — partial name match returns subset
- `FilterText_WhenEmpty_ReturnsAllApps`
- `FilterText_CaseInsensitive_MatchesRegardlessOfCase`
- `FilterText_WithNoMatch_SetsHasNoMatches`
- `HasNoMatches_WhenAppsEmpty_IsFalseNotTrue` — distinguishes from `HasNoApps`

---

## Feature 2: Configurable Grid Size

### Decision

Option A: Small/Medium/Large presets in the Settings panel. No controls in the main window.

### Architecture

New `GridSize` enum. `SettingsService` persists the selection. `MainViewModel` exposes pixel dimensions as computed properties that the DataTemplate binds.

### New Files

**`Helpers/GridSize.cs`**

```csharp
namespace Launchbox.Helpers;

public enum GridSize { Small, Medium, Large }
```

### Changes to Existing Files

**`SettingsService`** — new `GridSize` property backed by `LocalSettingsStore` key `"GridSize"`. Default: `GridSize.Medium`. Raises `PropertyChanged`.

**`MainViewModel`** — subscribes to `SettingsService.PropertyChanged`. Exposes:

| Property | Small | Medium (default) | Large |
|----------|-------|-----------------|-------|
| `ItemWidth` | 80 | 110 | 140 |
| `ItemHeight` | 96 | 130 | 165 |
| `IconSize` | 32 | 56 | 72 |

All three raise `PropertyChanged` when `SettingsService.GridSize` changes.

**`SettingsViewModel`** — new `SelectedGridSize` string property (values: `"Small"`, `"Medium"`, `"Large"`). Reads/writes `SettingsService.GridSize`.

**`SettingsWindow.xaml`** — `ComboBox` with three string items bound to `SelectedGridSize`.

**`MainWindow.xaml` DataTemplate** — since `x:DataType="models:AppItem"` prevents direct ViewModel bindings inside the DataTemplate, use `ElementName` binding to the root `MainWindow` to reach `ViewModel.ItemWidth` / `ViewModel.ItemHeight` / `ViewModel.IconSize`. Alternatively, add a `GridSizeHelper` static resource with converter, or add size properties to `AppItem` (not preferred — violates single responsibility). `ElementName` binding is the cleanest option.

### Error Handling

Invalid stored value defaults to `GridSize.Medium` via `Enum.TryParse`.

### Testing

- `GridSize_Default_IsMedium`
- `GridSize_Small_ReturnsCorrectDimensions`
- `GridSize_Large_ReturnsCorrectDimensions`
- `GridSize_Persists_AcrossSettingsServiceInstances`
- `SelectedGridSize_WhenChanged_UpdatesSettingsService`

---

## Feature 3: TODO Audit

Run `dotnet build -p:Platform=x64` with full analyzer output and review `TODO.md` for:

- Items completed but not checked off
- Items that are now stale or no longer applicable
- New warnings surfaced by `TreatWarningsAsErrors` on analyzers
- Any `.editorconfig` suppressions that can be removed

This is an investigative step, not a code change. Output: updated `TODO.md` with stale items removed or re-categorized, and a commit if changes are made.

---

## Feature 4: Performance Test

### New File

**`Launchbox.Tests/MainViewModelPerformanceTests.cs`**

```
[Trait("Category", "Performance")]
public class MainViewModelPerformanceTests
```

### Test: `LoadAppsAsync_With500Files_CompletesWithinTwoSeconds`

1. Create `MockShortcutService` returning 500 `.lnk` `AppItem` entries
2. Construct `MainViewModel` with all required mocks
3. Call `LoadAppsAsync()` and measure elapsed time
4. Assert: elapsed < 2 seconds AND `Apps.Count == 500`

The 500-entry collection exercises `BulkObservableCollection` batch update and any filtering or sorting paths that run at load time.

### Add to `Launchbox.Tests.csproj`

```xml
<Compile Include="..\ViewModels\MainViewModel.cs" Link="ViewModels\MainViewModel.cs" />
```
(if not already linked)

---

## Implementation Order

1. `GridSize` enum and `SettingsService` change (pure model, no UI)
2. `MainViewModel` size properties + `SettingsViewModel.SelectedGridSize`
3. `SettingsWindow.xaml` ComboBox
4. `MainWindow.xaml` DataTemplate `ElementName` bindings
5. `MainViewModel` filter properties
6. `MainWindow.xaml` TextBox + Esc handler + focus-on-show
7. TODO audit pass
8. Performance test

Each step gets its own commit (or grouped by logical unit).
