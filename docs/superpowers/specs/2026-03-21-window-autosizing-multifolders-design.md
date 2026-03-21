# Window Auto-Sizing & Multi-Folder Shortcut Sources

**Date:** 2026-03-21
**Status:** Draft
**Features:** TODO items #2 (Window height auto-sizing) and #4 (Multi-folder shortcut sources)

---

## 1. Overview

Two features that enhance Launchbox's flexibility:

1. **Window Auto-Sizing** — The window remembers user-set dimensions, freely resizable via edge/corner dragging, clamps to fit the work area on smaller displays, and optionally stays centered.
2. **Multi-Folder Sources** — Users configure multiple shortcut folders with custom labels, displayed as a flat merged grid or visually grouped sections with collapsible headers.

---

## 2. Window Auto-Sizing

### 2.1 Behavior

**First launch (no saved position):**
- Window sized to `WINDOW_WIDTH` (650) x `WINDOW_HEIGHT` (700), centered on primary display, saved immediately.
- These constants become first-run defaults only — never referenced after the first save.

**User resize:**
- User drags edges/corners to resize freely. Minimum dimensions enforced via `WM_GETMINMAXINFO` in `WindowService.NewWndProc`:
  - `MIN_WINDOW_WIDTH = 300` (fits ~2 icon columns at Medium grid size)
  - `MIN_WINDOW_HEIGHT = 200` (fits search box + ~1 row of icons)
- `WM_GETMINMAXINFO` implementation: define `MINMAXINFO` struct in `NativeMethods.cs` (reuse the existing `POINT` struct at line 98-103), read `lParam` via `Marshal.PtrToStructure`, set `ptMinTrackSize`, write back via `Marshal.StructureToPtr`.
- Existing debounce timer (500ms) saves new dimensions via `WindowPositionManager`. No changes to this flow.

**Show on smaller display:**
- `ClampToWorkArea` clamps both width and height to `workArea - 40px` margin.
- `_suppressSave` removed — clamped dimensions persist as the new preferred size.
- If position is off-screen after clamping, center on the available display.
- **Note:** This is an intentional UX tradeoff. A user who drags the window large on a 4K display, then shows it on a laptop screen, will permanently shrink their saved size. Moving back to the 4K display will not auto-expand. This matches the product requirement: "stay at clamped size."

**Show on larger display:**
- No automatic expansion. Window stays at last saved size. User can manually drag larger.

**Keep Centered option:**
- New `KeepCentered` setting (bool, default `false`).
- When enabled, window re-centers on the active display each time it's shown via hotkey. Saved width/height still apply (clamped to work area as needed). Saved X/Y position ignored but still persisted — so disabling the toggle restores the last manual position.
- **Monitor selection:** Uses `DisplayArea.GetFromPoint` with the window's current center point (or saved center point) to determine the active display. Falls back to `DisplayArea.Primary` if the point is not on any display. This matches the existing `RestoreWindowPosition` pattern.

### 2.2 Changes

| File | Change |
|------|--------|
| `Helpers/Constants.cs` | Add `MIN_WINDOW_WIDTH = 300`, `MIN_WINDOW_HEIGHT = 200` |
| `Services/NativeMethods.cs` | Add `MINMAXINFO` struct (reuse existing `POINT`), `WM_GETMINMAXINFO` constant |
| `Services/WindowService.cs` | Remove `_suppressSave` flag. Extend `ClampToWorkArea` to clamp width. Add `WM_GETMINMAXINFO` handler in `NewWndProc`. Respect `KeepCentered` in `ToggleVisibility`. |
| `Services/SettingsService.cs` | Add `KeepCentered` property (bool, default false) |
| `ViewModels/SettingsViewModel.cs` | Expose `KeepCentered` as bindable property |
| `SettingsWindow.xaml` | Add "Keep centered" ToggleSwitch in Window section |

---

## 3. Multi-Folder Shortcut Sources

### 3.1 Data Model

New record:
```csharp
public record ShortcutFolder
{
    public required string Path { get; init; }
    public required string Label { get; init; }  // defaults to folder name
    public required int Order { get; init; }
}
```

The `required` keyword enforces that all properties are set at construction, consistent with the project's nullable reference type conventions.

Extend `AppItem`:
```csharp
public string FolderLabel { get; init; }  // source folder label
```

New grouping model for Grouped view mode:
```csharp
public class AppItemGroup : ObservableCollection<AppItem>, INotifyPropertyChanged
{
    public string Label { get; }

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
            }
        }
    }

    public AppItemGroup(string label, IEnumerable<AppItem> items) : base(items)
    {
        Label = label;
    }
}
```

`AppItemGroup` extends `ObservableCollection<AppItem>` to satisfy `CollectionViewSource`'s requirement that the grouped source be an `IEnumerable` of groups where each group is an `IEnumerable` of items. `IsCollapsed` raises `PropertyChanged` for XAML binding to chevron rotation and items panel visibility.

Note: `ObservableCollection<T>` already implements `INotifyPropertyChanged`, so `AppItemGroup` inherits that implementation and can call `OnPropertyChanged` directly.

### 3.2 Settings

New settings in `SettingsService`:

| Setting | Type | Default | Storage |
|---------|------|---------|---------|
| `ShortcutFolders` | `List<ShortcutFolder>` | Single entry: `Desktop\Shortcuts` | JSON string via `System.Text.Json` |
| `FolderViewMode` | enum: `Merged`, `Grouped` | `Merged` | String |
| `CollapsibleGroups` | bool | `true` | Bool (only applies in Grouped mode) |

### 3.3 Storage & Migration

**Storage format** — single JSON string in `LocalSettings`:
```json
[
  {"Path": "C:\\Users\\Mike\\Desktop\\Shortcuts", "Label": "Shortcuts", "Order": 0},
  {"Path": "C:\\Users\\Mike\\Games", "Label": "Games", "Order": 1}
]
```

**Storage limits:** Windows `LocalSettings` has an 8KB per-value limit. Enforce a defensive cap of **20 folders** in `AddFolder`. Additionally, `ShortcutFolderManager` checks the serialized JSON byte length before writing — if it exceeds 7KB (leaving margin), the write is rejected and the add/rename operation returns `false`. The UI shows a localized error notification.

**In-memory caching and threading:** `ShortcutFolderManager` caches the folder list as an immutable `IReadOnlyList<ShortcutFolder>` snapshot. `GetFolders()` returns this snapshot directly (no deserialization). Mutations create a new immutable list, write-through to the store, then swap the cached reference. Since the cached field is a reference swap, readers always get a consistent snapshot without locking. The field is declared `volatile` to ensure visibility across threads.

**Migration (one-time, on first read):**
1. If `ShortcutFolders` key missing, check for legacy `ShortcutsPath` key.
2. If present: create single-entry list with `Path = legacy value`, `Label = folder name`, `Order = 0`.
3. Persist new `ShortcutFolders` JSON. Set legacy `ShortcutsPath` value to empty string (since `ISettingsStore` has no `Remove` method — empty string is the sentinel for "migrated").
4. If neither key exists, or `ShortcutsPath` is empty string: return default (one entry pointing to `Desktop\Shortcuts`).

**JSON corruption recovery:** If deserialization fails (malformed/truncated JSON), log the error via `Trace.WriteLine`, return the default single-folder list, and do **not** overwrite the corrupt value (allows manual recovery from the settings store).

**Validation on deserialization:**
- `PathSecurity.IsUnsafePath` — unsafe entries silently dropped.
- Environment variables expanded at read time (stored as-entered for portability).
- Empty/null paths dropped.
- `Order` values re-normalized to 0..N-1 (no gaps).

### 3.4 Architecture: Folder Settings Separation

To prevent `SettingsService` from becoming a grab-bag of CRUD logic, extract folder management into a dedicated component:

```csharp
public class ShortcutFolderManager
{
    // Owns: JSON serialization, in-memory cache, validation, migration, CRUD
    // Depends on: ISettingsStore (for persistence), PathSecurity (for validation)

    public IReadOnlyList<ShortcutFolder> GetFolders();
    public bool AddFolder(string path, string? label = null);
    public bool RemoveFolder(int order);
    public bool ReorderFolder(int fromOrder, int toOrder);
    public bool RenameFolder(int order, string newLabel);
}
```

`SettingsService` delegates to `ShortcutFolderManager` and fires `PropertyChanged` with `nameof(ShortcutFolders)` when the list is mutated. This follows the existing `SettingsService` pattern where all change notifications use `PropertyChanged` from `ObservableObject`. `MainViewModel` subscribes to this event to trigger `LoadAppsAsync`.

### 3.5 Loading Pipeline

`ShortcutService.GetShortcutFiles` unchanged — called once per folder.

**`LoadAppsAsync` updated flow:**
1. Read folders from `ShortcutFolderManager` (via `SettingsService`).
2. For each folder (ordered by `Order`), call `GetShortcutFiles` on background thread.
3. Build `AppItem` list, tag each with `FolderLabel`.
4. **Merged mode:** Sort all items alphabetically. Folder order is tiebreaker for same-name shortcuts. Expose as flat `Apps` collection (current behavior).
5. **Grouped mode:** Build `ObservableCollection<AppItemGroup>` — one group per folder, alphabetical within each group. Expose via a new `GroupedApps` property.
6. Replace collection(s) on UI thread.
7. Icon extraction unchanged — runs per-item regardless of source folder.

**`OpenShortcutsFolder` command update:**
Currently depends on `_settingsService.ShortcutsPath`. Updated behavior:
- If exactly one folder configured: open that folder (same as today).
- If multiple folders configured: open the first folder (by `Order`).
- The command remains a convenience shortcut; users manage folders via Settings.

**Duplicate handling:** Shortcuts with the same name in different folders both appear. In grouped mode they're distinguished by group header. In merged mode they appear as separate entries.

**IconService impact:** None. Icons cached by absolute shortcut path — works regardless of source folder. `PruneCache` receives the full merged file list.

### 3.6 Display

**Merged mode:**
- Single flat grid, all shortcuts sorted alphabetically. Identical to current single-folder display. No folder indicators.
- `MainWindow.xaml` binds `GridView.ItemsSource` to `FilteredApps` (flat, as today).

**Grouped mode:**
- A second `GridView` in `MainWindow.xaml`, bound to a `CollectionViewSource` with `IsSourceGrouped="True"`. The `CollectionViewSource.Source` binds to `FilteredGroupedApps`.
- Each group renders via a `GroupStyle.HeaderTemplate`: folder label text + optional collapse chevron.
- **View-mode switching:** Two `GridView` instances with mutually exclusive `Visibility` bindings driven by `FolderViewMode`. The merged `GridView` is visible when `FolderViewMode == Merged`, the grouped `GridView` when `FolderViewMode == Grouped`. This avoids runtime `ItemsSource` swapping and the binding errors that come with it.

**Filtering:**
- **Merged mode:** `FilteredApps` remains a flat `IEnumerable<AppItem>` filtered by `FilterText`, as today.
- **Grouped mode:** A new `FilteredGroupedApps` property (`IEnumerable<AppItemGroup>`) filters each group's items by `FilterText` and excludes groups with zero matches. Both properties are recomputed when `FilterText` or `Apps`/`GroupedApps` change.
- `HasNoMatches` logic: true when apps exist, filter is active, and both `FilteredApps` and `FilteredGroupedApps` are empty (depending on current view mode).

**Collapsible groups (when enabled):**
- Chevron icon on group header, click toggles `AppItemGroup.IsCollapsed` (bindable, raises `PropertyChanged`).
- When collapsed, the group's items panel `Visibility` is bound to `IsCollapsed` via `BooleanToVisibilityConverter` (inverted). Items are not removed from the collection — just hidden.
- Collapsed state is ephemeral — not persisted across sessions.
- Controlled by `CollapsibleGroups` setting toggle. When disabled, chevrons hidden via `Visibility` binding and all groups forced expanded.

**Empty states:**
- Folder has no shortcuts: group header shown with "No shortcuts found" (grouped mode). Invisible in merged mode.
- All folders empty: existing empty-state UI.
- No folders configured: empty state with prompt to add a folder in Settings.

### 3.7 Settings UI

**Folder Management section (replaces "Shortcuts" section):**

- `ListView` showing configured folders. Each row:
  - Folder label (editable via rename action)
  - Path (secondary text, truncated with tooltip)
  - Up/Down arrow buttons for reordering
  - Remove button (X icon)
- "Add Folder" button below the list — opens `FolderPicker`, adds with folder name as default label. Disabled when at 20-folder cap or when serialized size would exceed 7KB.
- Inline rename via edit button on the label.

**View mode controls (below folder list):**
- ComboBox for Merged/Grouped view mode.
- "Collapsible groups" ToggleSwitch — enabled only when view mode is Grouped.

### 3.8 Changes

| File | Change |
|------|--------|
| `Models/ShortcutFolder.cs` | New file: `ShortcutFolder` record with `required` properties |
| `Models/AppItem.cs` | Add `FolderLabel` property |
| `Models/FolderViewMode.cs` | New file: `Merged`/`Grouped` enum |
| `Models/AppItemGroup.cs` | New file: grouping collection with bindable `IsCollapsed` |
| `Services/ShortcutFolderManager.cs` | New file: folder CRUD, JSON serialization, migration, validation, immutable cached snapshots, size-guard writes |
| `Services/SettingsService.cs` | Replace `ShortcutsPath` with delegation to `ShortcutFolderManager`. Add `FolderViewMode`, `CollapsibleGroups`. Fire `PropertyChanged` on folder mutations. |
| `ViewModels/MainViewModel.cs` | Update `LoadAppsAsync` for multi-folder iteration, sorting, grouping. Add `GroupedApps` and `FilteredGroupedApps` properties. Update `OpenShortcutsFolder`. Update `SettingsService_PropertyChanged`. |
| `ViewModels/SettingsViewModel.cs` | Expose folder list, add/remove/reorder/rename commands, view mode, collapsible toggle. |
| `SettingsWindow.xaml` | Replace shortcuts section with folder list UI, add view mode controls. |
| `MainWindow.xaml` | Two `GridView` instances (merged/grouped) with visibility switching. `CollectionViewSource` for grouped display, group header template with collapse chevron. |
| `MainWindow.xaml.cs` | Wire `ShortcutFolderManager` in constructor, pass to `SettingsService`. |

---

## 4. Testing Strategy

### 4.1 Window Auto-Sizing Tests

- **WindowService:** `ClampToWorkArea` saves clamped dimensions (no `_suppressSave`). Width clamping works. `KeepCentered` re-centers on toggle.
- **Constants:** `MIN_WINDOW_WIDTH` and `MIN_WINDOW_HEIGHT` exist with expected values.
- **WM_GETMINMAXINFO:** Win32 interop — verified by app build and manual testing, not unit-testable. `MINMAXINFO` struct layout verified by size assertion.

### 4.2 Multi-Folder Tests

**ShortcutFolderManager tests:**
- Serialization round-trip (write folders, read back, verify).
- Migration from legacy `ShortcutsPath` to `ShortcutFolders`.
- Migration with empty-string sentinel (already migrated) returns default.
- Malformed JSON returns default list, does not overwrite stored value.
- Validation: unsafe paths dropped, order re-normalized, empty paths dropped.
- Environment variables stored as-entered, expanded at read time.
- Add/remove/reorder/rename operations.
- 20-folder cap enforced on add.
- Serialized size guard: add/rename rejected when JSON would exceed 7KB.
- Thread safety: concurrent `GetFolders` during mutation returns consistent snapshot.
- In-memory cache consistency after mutations.

**SettingsService tests:**
- `FolderViewMode` and `CollapsibleGroups` persistence.
- `PropertyChanged` fired with `nameof(ShortcutFolders)` on folder mutations.

**MainViewModel tests:**
- `LoadAppsAsync` with multiple folders — all shortcuts loaded with correct `FolderLabel`.
- Merged mode: alphabetical sort, folder order as tiebreaker for duplicates.
- Grouped mode: `GroupedApps` contains correct `AppItemGroup` instances, correct label, alphabetical within group.
- `FilteredGroupedApps`: filter hides non-matching items, excludes empty groups.
- `FilteredApps` in merged mode: filters across all folders as today.
- Empty folder handling (null from `GetShortcutFiles`).
- Duplicate shortcut names across folders — both appear.
- `OpenShortcutsFolder` opens first folder when multiple configured.

**SettingsViewModel tests:**
- Add folder updates list and fires `PropertyChanged`.
- Remove folder removes correct entry, re-normalizes order.
- Reorder (up/down) swaps correctly.
- Rename updates label.
- Add disabled at 20-folder cap.

### 4.3 Existing Test Impact

- `MainViewModelTests` setup: legacy `ShortcutsPath` key auto-migrates via `ShortcutFolderManager`. Existing tests work unchanged through migration path.
- `MockSettingsStore`: no changes (still key-value string storage).
- New file links in `Launchbox.Tests.csproj` for `ShortcutFolder.cs`, `FolderViewMode.cs`, `AppItemGroup.cs`, `ShortcutFolderManager.cs`.

---

## 5. Localization

New strings needed across all 8 locale files:

| Key | English (en-US) |
|-----|-----------------|
| `Settings_FolderList.Header` | Shortcut Folders |
| `Settings_AddFolder.Content` | Add Folder |
| `Settings_RemoveFolder.ToolTip` | Remove |
| `Settings_MoveUp.ToolTip` | Move Up |
| `Settings_MoveDown.ToolTip` | Move Down |
| `Settings_RenameFolder.ToolTip` | Rename |
| `Settings_ViewMode.Header` | View Mode |
| `Settings_ViewMode_Merged` | Merged |
| `Settings_ViewMode_Grouped` | Grouped |
| `Settings_CollapsibleGroups.Header` | Collapsible Groups |
| `Settings_KeepCentered.Header` | Keep Window Centered |
| `Settings_FolderCap` | Maximum number of folders reached |
| `FolderGroup_Empty` | No shortcuts found |
| `EmptyState_NoFolders` | Add a shortcut folder in Settings |

---

## 6. Out of Scope

- Individual shortcut drag-and-drop reordering (future phase).
- ListView drag-and-drop for folder reordering (up/down buttons used instead).
- Persisting collapsed/expanded state of group headers across sessions.
- Automatic window expansion when moving to a larger display.
- `ISettingsStore.Remove` method (migration uses empty-string sentinel instead).
