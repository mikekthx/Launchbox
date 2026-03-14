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

**New/changed properties on `MainViewModel`:**

| Property | Type | Description |
|----------|------|-------------|
| `FilterText` | `string` | Two-way bound to TextBox. Setter calls `OnPropertyChanged(nameof(FilteredApps))` and `OnPropertyChanged(nameof(HasNoMatches))`. |
| `FilteredApps` | `IEnumerable<AppItem>` | Returns `Apps` filtered by `Name.Contains(FilterText, OrdinalIgnoreCase)` when `FilterText` is non-empty; returns `Apps` otherwise. Computed property — not a separate collection. |
| `HasNoMatches` | `bool` | `FilterText` is non-empty AND `FilteredApps` is empty. |
| `IsEmpty` | `bool` | Already exists. Remains the "no shortcuts found" indicator (Apps collection is empty). Not replaced or renamed. |

**`Apps.CollectionChanged` subscription:** In the `MainViewModel` constructor, after `Apps` is assigned, subscribe:

```csharp
Apps.CollectionChanged += (_, _) =>
{
    OnPropertyChanged(nameof(FilteredApps));
    OnPropertyChanged(nameof(HasNoMatches));
};
```

This ensures `FilteredApps` re-evaluates when a reload happens while a filter string is active, including clearing a `HasNoMatches` state when the reloaded collection now contains items matching `FilterText`. Note: `IsEmpty` is not set here — the existing assignment inside `LoadAppsAsync` (dispatched on the UI thread) handles it. Do not duplicate that assignment.

### Components

- **`MainWindow.xaml`** — Change `AppGrid.ItemsSource` from `{x:Bind ViewModel.Apps, Mode=OneWay}` to `{x:Bind ViewModel.FilteredApps, Mode=OneWay}`. Add a `TextBox` below the scroll viewer containing `AppGrid`, using `{x:Bind ViewModel.FilterText, Mode=TwoWay}` (in-tree element, compiled binding correct). Esc key handler in code-behind sets `ViewModel.FilterText = string.Empty`. Focus called inside the existing `MainWindow_Activated` handler, inside the already-present `if (args.WindowActivationState != WindowActivationState.Deactivated)` guard at line 153.
- **`MainViewModel.cs`** — filter logic as described above. No debounce needed (in-memory collection).
- **Empty state panel** — a `TextBlock` for "No matches" state uses `{Binding}` (not `{x:Bind}`) to match the existing `IsEmpty`-based visibility binding pattern used elsewhere in the file. The text binding uses `{Binding ViewModel.FilterText}` for the current filter string.

### Data Flow

```
User types → FilterText changes → FilteredApps recomputed → AppGrid updates (live)
Esc pressed → FilterText = "" → FilteredApps = Apps → AppGrid restored
Window shown → existing Activated handler (non-deactivated branch) → TextBox.Focus()
Apps reloaded → CollectionChanged fires → FilteredApps re-evaluated with current FilterText
```

### Error Handling

No errors expected. Filter is pure in-memory computation.

### Testing

- `FilterText_WhenSet_FiltersAppsByName` — partial name match returns subset
- `FilterText_WhenEmpty_ReturnsAllApps`
- `FilterText_CaseInsensitive_MatchesRegardlessOfCase`
- `FilterText_WithNoMatch_SetsHasNoMatches`
- `HasNoMatches_WhenAppsEmptyAndFilterTextEmpty_IsFalse` — `HasNoMatches` is false when `FilterText` is empty even if `Apps` is empty (that's `IsEmpty`)
- `FilteredApps_AfterAppsReload_ReflectsNewCollection` — reload with filter active returns correct subset

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

**`SettingsService`** — new `GridSize` property backed by `LocalSettingsStore` key `"GridSize"`. Stored as `GridSize.ToString()`. Loaded via `Enum.TryParse<GridSize>`, defaulting to `GridSize.Medium` on parse failure. Raises `PropertyChanged`.

**`MainViewModel`** — subscribes to `SettingsService.PropertyChanged`. Exposes computed read-only properties:

| Property | Small | Medium (default) | Large |
|----------|-------|-----------------|-------|
| `ItemWidth` | 80 | 110 | 140 |
| `ItemHeight` | 96 | 130 | 165 |
| `IconSize` | 32 | 56 | 72 |

Add to `SettingsService_PropertyChanged`:

```csharp
else if (e.PropertyName == nameof(SettingsService.GridSize))
{
    OnPropertyChanged(nameof(ItemWidth));
    OnPropertyChanged(nameof(ItemHeight));
    OnPropertyChanged(nameof(IconSize));
}
```

**`SettingsViewModel`** — new `SelectedGridSize` string property and `GridSizeOptions` string array. Follows the same pattern as `Modifiers` / `SelectedModifier`:

```csharp
public IReadOnlyList<string> GridSizeOptions { get; } = ["Small", "Medium", "Large"];

public string SelectedGridSize
{
    get => _settingsService.GridSize.ToString();
    set { if (Enum.TryParse<GridSize>(value, out var g)) _settingsService.GridSize = g; }
}
```

Add to `OnServicePropertyChanged` (following the existing pattern at line 88):

```csharp
else if (e.PropertyName == nameof(SettingsService.GridSize))
    OnPropertyChanged(nameof(SelectedGridSize));
```

**`SettingsWindow.xaml`** — `ComboBox` using `ItemsSource` bound to `GridSizeOptions` and `SelectedItem` two-way bound to `SelectedGridSize` (same pattern as the existing `Modifiers` ComboBox at line 56):

```xml
<ComboBox ItemsSource="{x:Bind ViewModel.GridSizeOptions}"
          SelectedItem="{x:Bind ViewModel.SelectedGridSize, Mode=TwoWay}"
          Width="100" />
```

**`MainWindow.xaml` DataTemplate** — the DataTemplate is inline at `GridView.ItemTemplate` and `x:DataType="models:AppItem"` prevents direct ViewModel access. Use `AppGrid.Tag` as a relay: in `MainWindow` code-behind (in the constructor or `Loaded` handler), set `AppGrid.Tag = ViewModel;`. Then in the DataTemplate use `{Binding}` (not `{x:Bind}`) to reach the ViewModel through `Tag`:

```xml
<Grid Width="{Binding Tag.ItemWidth, ElementName=AppGrid}"
      Height="{Binding Tag.ItemHeight, ElementName=AppGrid}">
    <Image Width="{Binding Tag.IconSize, ElementName=AppGrid}"
           Height="{Binding Tag.IconSize, ElementName=AppGrid}" />
```

Note: `ElementName=AppGrid` works here because the DataTemplate is inline (not a resource) and `AppGrid` is in the same XAML namescope.

When `SettingsService.GridSize` changes, `MainViewModel` raises `PropertyChanged` for `ItemWidth`/`ItemHeight`/`IconSize`, which notifies the `Tag` binding on `AppGrid` and triggers a GridView item refresh. Because the bindings use `{Binding}` (not `{x:Bind}`), they respond to the ViewModel's `INotifyPropertyChanged`.

### Error Handling

Invalid stored value → `Enum.TryParse` falls back to `GridSize.Medium`.

### Testing

- `GridSize_Default_IsMedium`
- `GridSize_Small_ReturnsCorrectDimensions` — `ItemWidth == 80`, `ItemHeight == 96`, `IconSize == 32`
- `GridSize_Large_ReturnsCorrectDimensions`
- `GridSize_Persists_AcrossSettingsServiceInstances`
- `SelectedGridSize_WhenChanged_UpdatesSettingsService`
- `SelectedGridSize_WhenServiceChanges_RaisesPropertyChanged` — validates `OnServicePropertyChanged` relay

---

## Feature 3: TODO Audit

Run `dotnet build Launchbox.csproj -p:Platform=x64` with full output and review `TODO.md` (present in the repo root) for:

- Items completed but not checked off
- Items that are now stale or no longer applicable
- New warnings surfaced by analyzers now that `TreatWarningsAsErrors=true`
- Any `.editorconfig` suppressions that can be removed

Output: updated `TODO.md` with stale items removed or re-categorized, plus a commit if changes are made.

---

## Feature 4: Performance Test

### New File

**`Launchbox.Tests/MainViewModelPerformanceTests.cs`** with `[Trait("Category", "Performance")]` — excluded from the default CI `dotnet test` run to prevent flakiness on slow CI agents.

### Test: `LoadAppsAsync_With500Files_CompletesWithinTwoSeconds`

1. Configure `MockShortcutService` with 500 file paths: `Enumerable.Range(0, 500).Select(i => $@"C:\Shortcuts\App{i}.lnk").ToArray()` — the mock returns paths; `MainViewModel.LoadAppsAsync` builds `AppItem` instances internally.
2. Construct `MainViewModel` with all required mocks (`MockIconService`, `MockDispatcher`, etc.)
3. Record `Stopwatch.StartNew()`, call `await LoadAppsAsync()`, stop watch
4. Assert: `elapsed.TotalSeconds < 2.0` AND `Apps.Count == 500`

The 2-second threshold is generous relative to the in-memory mock execution; it guards against accidental O(n²) regressions. The test should not be run in CI by default (use `--filter "Category!=Performance"`).

The 500-entry collection exercises the `BulkObservableCollection` batch-update path and `CollectionChanged` notifications.

`MainViewModel.cs` is already linked in `Launchbox.Tests.csproj` — no new `<Compile Include>` entry needed.

---

## Implementation Order

1. `GridSize` enum (`Helpers/GridSize.cs`)
2. `SettingsService.GridSize` property
3. `MainViewModel` size properties + `SettingsService_PropertyChanged` handler update
4. `SettingsViewModel.SelectedGridSize` + `GridSizeOptions` + `OnServicePropertyChanged` relay
5. `SettingsWindow.xaml` ComboBox
6. `MainWindow.xaml` DataTemplate: set `AppGrid.Tag = ViewModel` in code-behind; add `{Binding Tag.ItemWidth/Height/IconSize, ElementName=AppGrid}` to DataTemplate
7. `MainViewModel` filter properties (`FilterText`, `FilteredApps`, `HasNoMatches`) + `CollectionChanged` subscription
8. `MainWindow.xaml`: change `AppGrid.ItemsSource` to `FilteredApps`; add TextBox with `{x:Bind ViewModel.FilterText, Mode=TwoWay}`; add Esc key handler; add TextBox focus in `Activated` handler
9. TODO audit pass
10. Performance test (`MainViewModelPerformanceTests.cs`)

Run `dotnet format Launchbox.sln` before each commit.
