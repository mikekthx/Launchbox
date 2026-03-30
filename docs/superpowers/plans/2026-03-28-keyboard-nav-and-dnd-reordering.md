# Keyboard Navigation & Drag-and-Drop Reordering Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add keyboard navigation (Enter-to-launch, type-to-search) to the app grid, drag-and-drop folder reordering in Settings, and drag-and-drop shortcut reordering in the main grid.

**Architecture:**
- Keyboard navigation adds event handlers to the existing `GridView` controls in `MainWindow.xaml.cs` — no ViewModel changes.
- Folder drag-and-drop adds one new method to `ShortcutFolderManager` and threads it through `SettingsService` → `SettingsViewModel` → `SettingsWindow` code-behind.
- Shortcut drag-and-drop adds `GetItemOrder`/`SetItemOrder` to `SettingsService`, promotes `FilteredApps` from a computed `IReadOnlyList` to a maintained `BulkObservableCollection`, and wires a `DragItemsCompleted` handler in `MainWindow.xaml.cs`.

**Tech Stack:** WinUI 3 (Windows App SDK 1.8), C# 12 / .NET 10, CommunityToolkit.Mvvm 8.4, xUnit.

---

> **Scope note:** These are three independent subsystems. Each phase produces working, shippable software on its own. Execute phases in order or independently — Phase C depends on nothing from Phase A or B.

---

> **Hotkey note:** The configurable hotkey is already fully implemented — `SettingsViewModel` exposes `SelectedModifier` + `HotkeyKeyString`, `SettingsService` persists `HotkeyModifiers`/`HotkeyKey`, and `WindowService.UpdateHotkey()` re-registers on change. No work needed there.

---

## File Map

**Phase A — Keyboard Navigation**
- Modify: `MainWindow.xaml` — add `KeyDown` and `CharacterReceived` event attributes to both GridViews
- Modify: `MainWindow.xaml.cs` — add two event handlers (shared by both grids)
- No new tests (handlers delegate to already-tested commands)

**Phase B — Folder Drag-and-Drop**
- Modify: `Services/ShortcutFolderManager.cs` — add `SetFolderSequence(IReadOnlyList<string> orderedPaths)`
- Modify: `Services/SettingsService.cs` — add `SetShortcutFolderSequence(IReadOnlyList<string> orderedPaths)`
- Modify: `ViewModels/SettingsViewModel.cs` — add `SetFolderSequence(IReadOnlyList<string> orderedPaths)`
- Modify: `SettingsWindow.xaml` — add drag-and-drop attributes to `ListView`
- Modify: `SettingsWindow.xaml.cs` — add `FolderList_DragItemsCompleted` handler
- Modify: `Launchbox.Tests/ShortcutFolderManagerTests.cs` — add tests for `SetFolderSequence`
- Modify: `Launchbox.Tests/SettingsViewModelTests.cs` — add test for `SetFolderSequence`
- Modify: `Launchbox.Tests/Launchbox.Tests.csproj` — no new file links needed (ShortcutFolderManager already linked)

**Phase C — Shortcut Drag-and-Drop**
- Modify: `Services/SettingsService.cs` — add `GetItemOrder` / `SetItemOrder`
- Modify: `ViewModels/MainViewModel.cs` — promote `FilteredApps` to `BulkObservableCollection`, add `IsFilterEmpty`, add `PersistItemOrder`, update `LoadAppsAsync` to apply custom order
- Modify: `MainWindow.xaml` — add `CanReorderItems`, `AllowDrop`, `CanDragItems`, `DragItemsCompleted` to `AppGrid` only (grouped mode not supported in v1)
- Modify: `MainWindow.xaml.cs` — add `AppGrid_DragItemsCompleted` handler
- Modify: `Launchbox.Tests/SettingsServiceTests.cs` — add item order tests
- Modify: `Launchbox.Tests/MainViewModelTests.cs` — add tests for order application in `LoadAppsAsync`

---

## Phase A: Keyboard Navigation

### Task 1: Enter-to-launch and type-to-search

**Files:**
- Modify: `MainWindow.xaml`
- Modify: `MainWindow.xaml.cs`

- [ ] **Step 1: Wire XAML events**

In `MainWindow.xaml`, add two event attributes to `AppGrid` (around line 99) and the same two to `GroupedAppGrid` (around line 143):

```xml
<!-- AppGrid — add these two attributes -->
<GridView x:Uid="MainWindow_ShortcutsGrid"
          x:Name="AppGrid"
          ...
          KeyDown="Grid_KeyDown"
          CharacterReceived="Grid_CharacterReceived">

<!-- GroupedAppGrid — add these two attributes -->
<GridView x:Name="GroupedAppGrid"
          ...
          KeyDown="Grid_KeyDown"
          CharacterReceived="Grid_CharacterReceived">
```

- [ ] **Step 2: Add handlers in MainWindow.xaml.cs**

Add both handlers in the `// --- WINDOW EVENTS ---` region (or after the existing `Window_Activated` handler, around line 162). Add the following `using` if not present: `using Windows.System;`

```csharp
// Enter key launches the focused shortcut.
// WinUI GridView with IsItemClickEnabled does not automatically fire ItemClick on Enter,
// so we query the focused element manually.
private void Grid_KeyDown(object sender, KeyRoutedEventArgs e)
{
    if (e.Key != VirtualKey.Enter) return;
    var focused = FocusManager.GetFocusedElement(XamlRoot) as GridViewItem;
    if (focused?.DataContext is AppItem item && !string.IsNullOrEmpty(item.Name))
    {
        ViewModel.LaunchAppCommand.Execute(item);
        e.Handled = true;
    }
}

// Typing while the grid has focus redirects characters to the search box,
// so the user can start typing to filter without clicking the search box first.
private void Grid_CharacterReceived(UIElement sender, CharacterReceivedRoutedEventArgs args)
{
    if (char.IsControl(args.Character)) return;
    SearchBox.Text += args.Character.ToString();
    SearchBox.Focus(FocusState.Programmatic);
    SearchBox.SelectionStart = SearchBox.Text.Length;
    args.Handled = true;
}
```

- [ ] **Step 3: Build to verify XAML compiles**

```bash
dotnet build Launchbox.csproj -p:Platform=x64
```

Expected: build succeeds, no errors.

- [ ] **Step 4: Commit**

```bash
git add MainWindow.xaml MainWindow.xaml.cs
git commit -m "feat: add keyboard navigation to app grid (Enter launches, type-to-search)"
```

---

## Phase B: Folder Drag-and-Drop Reordering

### Task 2: Add SetFolderSequence to ShortcutFolderManager (TDD)

**Files:**
- Modify: `Services/ShortcutFolderManager.cs`
- Modify: `Launchbox.Tests/ShortcutFolderManagerTests.cs`

- [ ] **Step 1: Write failing tests**

Add the following tests to `Launchbox.Tests/ShortcutFolderManagerTests.cs`. Find the existing `[Fact]` tests and add after them:

```csharp
[Fact]
public void SetFolderSequence_ReordersAndPersists()
{
    var store = StoreWithFolders([
        new ShortcutFolder { Path = SafePath("A"), Label = "A", Order = 0 },
        new ShortcutFolder { Path = SafePath("B"), Label = "B", Order = 1 },
        new ShortcutFolder { Path = SafePath("C"), Label = "C", Order = 2 },
    ]);
    var manager = new ShortcutFolderManager(store);

    var result = manager.SetFolderSequence([SafePath("C"), SafePath("A"), SafePath("B")]);

    Assert.True(result);
    var folders = manager.GetFolders();
    Assert.Equal(3, folders.Count);
    Assert.Equal("C", folders[0].Label);
    Assert.Equal("A", folders[1].Label);
    Assert.Equal("B", folders[2].Label);
    // Orders are renumbered from 0
    Assert.Equal(0, folders[0].Order);
    Assert.Equal(1, folders[1].Order);
    Assert.Equal(2, folders[2].Order);
}

[Fact]
public void SetFolderSequence_IgnoresUnknownPaths()
{
    var store = StoreWithFolders([
        new ShortcutFolder { Path = SafePath("A"), Label = "A", Order = 0 },
        new ShortcutFolder { Path = SafePath("B"), Label = "B", Order = 1 },
    ]);
    var manager = new ShortcutFolderManager(store);

    // SafePath("Z") is not in the list — should be silently ignored
    var result = manager.SetFolderSequence([SafePath("B"), SafePath("Z"), SafePath("A")]);

    Assert.True(result);
    var folders = manager.GetFolders();
    Assert.Equal(2, folders.Count);
    Assert.Equal("B", folders[0].Label);
    Assert.Equal("A", folders[1].Label);
}

[Fact]
public void SetFolderSequence_AppendsUnrepresentedFolders()
{
    var store = StoreWithFolders([
        new ShortcutFolder { Path = SafePath("A"), Label = "A", Order = 0 },
        new ShortcutFolder { Path = SafePath("B"), Label = "B", Order = 1 },
        new ShortcutFolder { Path = SafePath("C"), Label = "C", Order = 2 },
    ]);
    var manager = new ShortcutFolderManager(store);

    // C is omitted from the sequence — should be appended at the end
    var result = manager.SetFolderSequence([SafePath("B"), SafePath("A")]);

    Assert.True(result);
    var folders = manager.GetFolders();
    Assert.Equal(3, folders.Count);
    Assert.Equal("B", folders[0].Label);
    Assert.Equal("A", folders[1].Label);
    Assert.Equal("C", folders[2].Label);
}
```

- [ ] **Step 2: Run tests to confirm they fail**

```bash
dotnet test Launchbox.Tests/Launchbox.Tests.csproj --filter "FullyQualifiedName~SetFolderSequence"
```

Expected: 3 tests fail with "method not found" or similar.

- [ ] **Step 3: Implement SetFolderSequence in ShortcutFolderManager**

Add the following method after `ReorderFolder` in `Services/ShortcutFolderManager.cs`:

```csharp
/// <summary>
/// Sets the canonical folder order to match <paramref name="orderedPaths"/>.
/// Unknown paths are skipped; folders not present in <paramref name="orderedPaths"/>
/// are appended at the end (preserving them rather than silently dropping them).
/// </summary>
public bool SetFolderSequence(IReadOnlyList<string> orderedPaths)
{
    return MutateAndPersist(folders =>
    {
        var lookup = folders.ToDictionary(f => f.Path, StringComparer.OrdinalIgnoreCase);
        var represented = new HashSet<string>(orderedPaths, StringComparer.OrdinalIgnoreCase);

        var ordered = orderedPaths
            .Where(p => lookup.ContainsKey(p))
            .Select(p => lookup[p])
            .ToList();

        // Append any folders not covered by orderedPaths so they aren't silently lost
        ordered.AddRange(folders.Where(f => !represented.Contains(f.Path)));

        folders.Clear();
        folders.AddRange(ordered);
        return true;
    }, renumber: true);
}
```

- [ ] **Step 4: Run tests — expect all 3 pass**

```bash
dotnet test Launchbox.Tests/Launchbox.Tests.csproj --filter "FullyQualifiedName~SetFolderSequence"
```

Expected: 3 tests pass.

- [ ] **Step 5: Run full test suite**

```bash
dotnet test Launchbox.Tests/Launchbox.Tests.csproj
```

Expected: all tests pass.

- [ ] **Step 6: Commit**

```bash
git add Services/ShortcutFolderManager.cs Launchbox.Tests/ShortcutFolderManagerTests.cs
git commit -m "feat: add SetFolderSequence to ShortcutFolderManager for drag-and-drop ordering"
```

---

### Task 3: Thread SetFolderSequence through SettingsService and SettingsViewModel

**Files:**
- Modify: `Services/SettingsService.cs`
- Modify: `ViewModels/SettingsViewModel.cs`
- Modify: `Launchbox.Tests/SettingsViewModelTests.cs`

- [ ] **Step 1: Write failing ViewModel test**

Add the following to `Launchbox.Tests/SettingsViewModelTests.cs` (find the existing test class and add inside it):

```csharp
[Fact]
public void SetFolderSequence_ReordersAndRefreshesFolders()
{
    var store = new MockSettingsStore();
    var folders = new List<ShortcutFolder>
    {
        new() { Path = @"C:\Desktop\A", Label = "A", Order = 0 },
        new() { Path = @"C:\Desktop\B", Label = "B", Order = 1 },
    };
    store.SetValue("ShortcutFolders", System.Text.Json.JsonSerializer.Serialize(folders));

    var settingsService = new SettingsService(
        store,
        new MockStartupService(),
        new ShortcutFolderManager(store));
    var vm = new SettingsViewModel(settingsService, new MockWindowService(), new MockFilePickerService());

    vm.SetFolderSequence([@"C:\Desktop\B", @"C:\Desktop\A"]);

    Assert.Equal(2, vm.Folders.Count);
    Assert.Equal("B", vm.Folders[0].Label);
    Assert.Equal("A", vm.Folders[1].Label);
}
```

- [ ] **Step 2: Run test — expect fail**

```bash
dotnet test Launchbox.Tests/Launchbox.Tests.csproj --filter "FullyQualifiedName~SetFolderSequence_ReordersAndRefreshesFolders"
```

Expected: fails — `SetFolderSequence` doesn't exist on ViewModel yet.

- [ ] **Step 3: Add SetShortcutFolderSequence to SettingsService**

In `Services/SettingsService.cs`, add after `RenameShortcutFolder`:

```csharp
public bool SetShortcutFolderSequence(IReadOnlyList<string> orderedPaths)
{
    if (_folderManager.SetFolderSequence(orderedPaths))
    {
        OnPropertyChanged("ShortcutFolders");
        return true;
    }
    return false;
}
```

- [ ] **Step 4: Add SetFolderSequence to SettingsViewModel**

In `ViewModels/SettingsViewModel.cs`, add after `ApplyRename`:

```csharp
public void SetFolderSequence(IReadOnlyList<string> orderedPaths)
{
    if (_settingsService.SetShortcutFolderSequence(orderedPaths))
        RefreshFolders();
}
```

- [ ] **Step 5: Run test — expect pass**

```bash
dotnet test Launchbox.Tests/Launchbox.Tests.csproj --filter "FullyQualifiedName~SetFolderSequence_ReordersAndRefreshesFolders"
```

Expected: passes.

- [ ] **Step 6: Run full test suite**

```bash
dotnet test Launchbox.Tests/Launchbox.Tests.csproj
```

Expected: all tests pass.

- [ ] **Step 7: Commit**

```bash
git add Services/SettingsService.cs ViewModels/SettingsViewModel.cs Launchbox.Tests/SettingsViewModelTests.cs
git commit -m "feat: thread SetFolderSequence through SettingsService and SettingsViewModel"
```

---

### Task 4: Wire drag-and-drop in SettingsWindow

**Files:**
- Modify: `SettingsWindow.xaml`
- Modify: `SettingsWindow.xaml.cs`

- [ ] **Step 1: Enable drag-and-drop on the folder ListView in XAML**

In `SettingsWindow.xaml`, find the `<ListView ItemsSource="{x:Bind ViewModel.Folders}"` element (around line 37) and add four attributes plus the completed event:

```xml
<ListView x:Name="FolderList"
          ItemsSource="{x:Bind ViewModel.Folders}"
          SelectionMode="None"
          CanReorderItems="True"
          AllowDrop="True"
          CanDragItems="True"
          DragItemsCompleted="FolderList_DragItemsCompleted"
          MaxHeight="200">
```

- [ ] **Step 2: Add DragItemsCompleted handler in SettingsWindow.xaml.cs**

Add the following handler after the `RenameFolder_Click` method in `SettingsWindow.xaml.cs`:

```csharp
private void FolderList_DragItemsCompleted(ListViewBase sender, DragItemsCompletedEventArgs args)
{
    var orderedPaths = sender.Items.OfType<ShortcutFolder>().Select(f => f.Path).ToList();
    ViewModel.SetFolderSequence(orderedPaths);
}
```

Note: `using Launchbox.Models;` is required for `ShortcutFolder`; add it at the top of the file if not present. `using System.Linq;` is also required.

- [ ] **Step 3: Build**

```bash
dotnet build Launchbox.csproj -p:Platform=x64
```

Expected: succeeds, no errors.

- [ ] **Step 4: Format**

```bash
dotnet format Launchbox.sln
```

- [ ] **Step 5: Commit**

```bash
git add SettingsWindow.xaml SettingsWindow.xaml.cs
git commit -m "feat: add drag-and-drop folder reordering to Settings"
```

---

## Phase C: Shortcut Drag-and-Drop Reordering

### Task 5: Add item order persistence to SettingsService (TDD)

**Files:**
- Modify: `Services/SettingsService.cs`
- Modify: `Launchbox.Tests/SettingsServiceTests.cs`

- [ ] **Step 1: Write failing tests**

Add the following to `Launchbox.Tests/SettingsServiceTests.cs`:

```csharp
[Fact]
public void GetItemOrder_ReturnsEmpty_WhenNotSet()
{
    var svc = new SettingsService(
        new MockSettingsStore(),
        new MockStartupService(),
        new ShortcutFolderManager(new MockSettingsStore()));

    var order = svc.GetItemOrder(@"C:\Desktop\Shortcuts");

    Assert.Empty(order);
}

[Fact]
public void SetItemOrder_PersistsAndRetrievesOrder()
{
    var store = new MockSettingsStore();
    var svc = new SettingsService(
        store,
        new MockStartupService(),
        new ShortcutFolderManager(new MockSettingsStore()));

    var names = new List<string> { "Notepad.lnk", "Calculator.lnk", "Paint.lnk" };
    var result = svc.SetItemOrder(@"C:\Desktop\Shortcuts", names);

    Assert.True(result);
    var retrieved = svc.GetItemOrder(@"C:\Desktop\Shortcuts");
    Assert.Equal(names, retrieved);
}

[Fact]
public void SetItemOrder_PreservesOtherFolderOrders()
{
    var store = new MockSettingsStore();
    var svc = new SettingsService(
        store,
        new MockStartupService(),
        new ShortcutFolderManager(new MockSettingsStore()));

    svc.SetItemOrder(@"C:\FolderA", ["A1.lnk", "A2.lnk"]);
    svc.SetItemOrder(@"C:\FolderB", ["B1.lnk", "B2.lnk"]);

    // Second call must not overwrite FolderA
    Assert.Equal(["A1.lnk", "A2.lnk"], svc.GetItemOrder(@"C:\FolderA"));
    Assert.Equal(["B1.lnk", "B2.lnk"], svc.GetItemOrder(@"C:\FolderB"));
}

[Fact]
public void GetItemOrder_ReturnsCaseInsensitiveMatch()
{
    var store = new MockSettingsStore();
    var svc = new SettingsService(
        store,
        new MockStartupService(),
        new ShortcutFolderManager(new MockSettingsStore()));

    svc.SetItemOrder(@"C:\Desktop\Shortcuts", ["Notepad.lnk"]);

    // Path casing should not matter
    var order = svc.GetItemOrder(@"c:\desktop\shortcuts");
    Assert.Equal(["Notepad.lnk"], order);
}
```

- [ ] **Step 2: Run tests — expect fail**

```bash
dotnet test Launchbox.Tests/Launchbox.Tests.csproj --filter "FullyQualifiedName~ItemOrder"
```

Expected: 4 tests fail — methods don't exist yet.

- [ ] **Step 3: Implement GetItemOrder and SetItemOrder in SettingsService**

Add the following to `Services/SettingsService.cs`. Add `using System.Text.Json;` at the top if not present.

```csharp
private const string ITEM_ORDERS_KEY = "ShortcutItemOrders";

/// <summary>
/// Returns the custom display order for shortcuts in <paramref name="folderPath"/>,
/// as a list of shortcut file names (e.g. "Notepad.lnk").
/// Returns an empty list if no custom order has been saved.
/// </summary>
public IReadOnlyList<string> GetItemOrder(string folderPath)
{
    if (!_store.TryGetValue(ITEM_ORDERS_KEY, out var val) || val is not string json)
        return [];

    try
    {
        var dict = JsonSerializer.Deserialize<Dictionary<string, List<string>>>(json);
        if (dict != null)
        {
            // Case-insensitive path lookup
            var key = dict.Keys.FirstOrDefault(
                k => k.Equals(folderPath, StringComparison.OrdinalIgnoreCase));
            if (key != null)
                return dict[key];
        }
    }
    catch (JsonException ex)
    {
        Trace.WriteLine($"Corrupt ShortcutItemOrders JSON: {PathSecurity.GetSafeExceptionMessage(ex)}");
    }
    return [];
}

/// <summary>
/// Persists a custom display order for shortcuts in <paramref name="folderPath"/>.
/// Existing orders for other folders are preserved.
/// </summary>
public bool SetItemOrder(string folderPath, IReadOnlyList<string> orderedNames)
{
    var existing = _store.TryGetValue(ITEM_ORDERS_KEY, out var val) && val is string json
        ? json : "{}";

    Dictionary<string, List<string>> dict;
    try
    {
        dict = JsonSerializer.Deserialize<Dictionary<string, List<string>>>(existing) ?? [];
    }
    catch (JsonException ex)
    {
        Trace.WriteLine($"Corrupt ShortcutItemOrders JSON, resetting: {PathSecurity.GetSafeExceptionMessage(ex)}");
        dict = [];
    }

    dict[folderPath] = [.. orderedNames];

    if (!_store.SetValue(ITEM_ORDERS_KEY, JsonSerializer.Serialize(dict)))
        return false;

    OnPropertyChanged(ITEM_ORDERS_KEY);
    return true;
}
```

- [ ] **Step 4: Run tests — expect all 4 pass**

```bash
dotnet test Launchbox.Tests/Launchbox.Tests.csproj --filter "FullyQualifiedName~ItemOrder"
```

Expected: 4 tests pass.

- [ ] **Step 5: Run full test suite**

```bash
dotnet test Launchbox.Tests/Launchbox.Tests.csproj
```

Expected: all tests pass.

- [ ] **Step 6: Commit**

```bash
git add Services/SettingsService.cs Launchbox.Tests/SettingsServiceTests.cs
git commit -m "feat: add GetItemOrder/SetItemOrder to SettingsService for shortcut ordering"
```

---

### Task 6: Promote FilteredApps to ObservableCollection and apply custom order on load

**Files:**
- Modify: `ViewModels/MainViewModel.cs`
- Modify: `Launchbox.Tests/MainViewModelTests.cs`

This task is the largest. Read `ViewModels/MainViewModel.cs` in full before editing.

- [ ] **Step 1: Write failing tests**

Add the following to `Launchbox.Tests/MainViewModelTests.cs`. These tests verify that custom order is applied on load and that `IsFilterEmpty` reports correctly.

```csharp
[Fact]
public async Task LoadAppsAsync_AppliesCustomItemOrder()
{
    // Arrange: create two shortcut files
    var folder = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
    Directory.CreateDirectory(folder);
    File.WriteAllText(Path.Combine(folder, "B App.lnk"), string.Empty);
    File.WriteAllText(Path.Combine(folder, "A App.lnk"), string.Empty);

    var store = new MockSettingsStore();
    // Persist a custom order: B App before A App
    var settingsService = CreateSettingsService(store, folder);
    settingsService.SetItemOrder(folder, ["B App.lnk", "A App.lnk"]);

    var vm = CreateViewModel(settingsService);

    try
    {
        // Act
        await vm.LoadAppsAsync();

        // Assert: B App is first despite alphabetical order being A first
        Assert.Equal(2, vm.Apps.Count);
        Assert.Equal("B App", vm.Apps[0].Name);
        Assert.Equal("A App", vm.Apps[1].Name);
    }
    finally
    {
        Directory.Delete(folder, recursive: true);
    }
}

[Fact]
public void IsFilterEmpty_TrueWhenNoFilter()
{
    var vm = CreateViewModel();
    Assert.True(vm.IsFilterEmpty);
}

[Fact]
public void IsFilterEmpty_FalseWhenFilterSet()
{
    var vm = CreateViewModel();
    vm.FilterText = "calc";
    Assert.False(vm.IsFilterEmpty);
}
```

Note: `CreateSettingsService` and `CreateViewModel` may need to be extracted from the existing test setup in that file. Match the patterns already used in `MainViewModelTests.cs` for constructing a SettingsService and MainViewModel with a custom folder path.

- [ ] **Step 2: Run tests — expect fail**

```bash
dotnet test Launchbox.Tests/Launchbox.Tests.csproj --filter "FullyQualifiedName~AppliesCustomItemOrder|FullyQualifiedName~IsFilterEmpty"
```

Expected: tests fail — `IsFilterEmpty` and custom ordering don't exist yet.

- [ ] **Step 3: Change FilteredApps from computed IReadOnlyList to BulkObservableCollection**

Read `ViewModels/MainViewModel.cs` fully before making this change.

In `MainViewModel.cs`:

**a)** Remove the private cache field (find `_cachedFilteredApps` and delete it).

**b)** Change the `FilteredApps` property from a computed property to a collection-backed property:

```csharp
// Before (delete this):
private IReadOnlyList<AppItem>? _cachedFilteredApps;
public IReadOnlyList<AppItem> FilteredApps
{
    get
    {
        if (_cachedFilteredApps != null) return _cachedFilteredApps;
        _cachedFilteredApps = string.IsNullOrEmpty(_filterText)
            ? [.. Apps]
            : [.. Apps.Where(a => a.Name.Contains(_filterText, StringComparison.OrdinalIgnoreCase))];
        return _cachedFilteredApps;
    }
}
```

```csharp
// After (add this):
public BulkObservableCollection<AppItem> FilteredApps { get; } = [];

private void RebuildFilteredApps()
{
    var source = string.IsNullOrEmpty(_filterText)
        ? (IEnumerable<AppItem>)Apps
        : Apps.Where(a => a.Name.Contains(_filterText, StringComparison.OrdinalIgnoreCase));
    FilteredApps.ReplaceAll(source);
}
```

**c)** Find every place that currently does `_cachedFilteredApps = null; OnPropertyChanged(nameof(FilteredApps));` and replace with `RebuildFilteredApps();`. There will be two of these — one in the filter text setter, one at the end of `LoadAppsAsync`.

**d)** Add the `IsFilterEmpty` property after `FilteredApps`:

```csharp
public bool IsFilterEmpty => string.IsNullOrEmpty(_filterText);
```

**e)** Update the `FilterText` setter to also notify `IsFilterEmpty`:

```csharp
// Find the FilterText setter and add OnPropertyChanged(nameof(IsFilterEmpty)):
public string FilterText
{
    get => _filterText;
    set
    {
        if (_filterText == value) return;
        _filterText = value;
        OnPropertyChanged(nameof(FilterText));
        OnPropertyChanged(nameof(IsFilterEmpty));
        RebuildFilteredApps();
        // ... rest of existing setter (HasNoMatches, etc.)
    }
}
```

- [ ] **Step 4: Apply custom item order in LoadAppsAsync**

In `LoadAppsAsync`, after items are built per folder and before the groups are assembled, add custom-order application. Find the section that builds `localAppItems` (a list of `AppItem`), and add the sorting step:

```csharp
// After all items are loaded and before building GroupedApps:
// Apply custom item order per folder, falling back to alphabetical for unlisted items.
var orderedItems = localAppItems
    .GroupBy(a => a.FolderPath)
    .SelectMany(g =>
    {
        var customOrder = _settingsService.GetItemOrder(g.Key);
        if (customOrder.Count == 0)
            return g.OrderBy(a => a.Name);

        // Build lookup: shortcut filename (case-insensitive) → AppItem
        var byName = g.ToDictionary(
            a => Path.GetFileName(a.Path),
            StringComparer.OrdinalIgnoreCase);

        // Items listed in custom order first (in order), then unlisted items alphabetically
        var ordered = customOrder
            .Where(name => byName.ContainsKey(name))
            .Select(name => byName[name])
            .ToList();
        var listed = new HashSet<string>(customOrder, StringComparer.OrdinalIgnoreCase);
        ordered.AddRange(g
            .Where(a => !listed.Contains(Path.GetFileName(a.Path)))
            .OrderBy(a => a.Name));
        return ordered;
    })
    .ToList();
```

Then use `orderedItems` instead of `localAppItems` when building `Apps` and `GroupedApps`. Read the existing construction code carefully to slot this in correctly — the exact variable names may differ from `localAppItems`.

- [ ] **Step 5: Add using for System.IO if needed**

`Path.GetFileName` requires `using System.IO;` — check it's present in `MainViewModel.cs`.

- [ ] **Step 6: Run tests — expect new tests pass, existing tests still pass**

```bash
dotnet test Launchbox.Tests/Launchbox.Tests.csproj --filter "FullyQualifiedName~AppliesCustomItemOrder|FullyQualifiedName~IsFilterEmpty"
```

Then:

```bash
dotnet test Launchbox.Tests/Launchbox.Tests.csproj
```

Expected: all tests pass.

- [ ] **Step 7: Build**

```bash
dotnet build Launchbox.csproj -p:Platform=x64
```

Expected: succeeds — no XAML binding errors. The `FilteredApps` type change from `IReadOnlyList<AppItem>` to `BulkObservableCollection<AppItem>` is compatible with `{x:Bind ViewModel.FilteredApps, Mode=OneWay}`.

- [ ] **Step 8: Commit**

```bash
git add ViewModels/MainViewModel.cs Launchbox.Tests/MainViewModelTests.cs
git commit -m "feat: promote FilteredApps to observable collection, apply custom item order on load"
```

---

### Task 7: Wire drag-and-drop in the main grid

**Files:**
- Modify: `MainWindow.xaml`
- Modify: `MainWindow.xaml.cs`
- Modify: `ViewModels/MainViewModel.cs`

Drag-and-drop shortcut reordering is only enabled in merged mode (`AppGrid`). In grouped mode the items are partitioned by folder — cross-folder drags would imply moving files between folders, which is out of scope for v1.

The drag also only persists when no filter is active. With an active filter, `FilteredApps` is a subset, so dragging within it would produce an ambiguous order for unfiltered items.

- [ ] **Step 1: Add PersistItemOrder to MainViewModel**

In `ViewModels/MainViewModel.cs`, add this method:

```csharp
/// <summary>
/// Persists the current order of <see cref="FilteredApps"/> (call after drag-and-drop).
/// Groups items by folder and writes the ordered file names to settings.
/// Only meaningful when no filter is active (FilteredApps == full list).
/// </summary>
public void PersistItemOrder()
{
    foreach (var group in FilteredApps.GroupBy(a => a.FolderPath))
    {
        var names = group.Select(a => Path.GetFileName(a.Path)).ToList();
        _settingsService.SetItemOrder(group.Key, names);
    }
    // Sync Apps to match the new FilteredApps order so rebuilds stay consistent
    Apps.ReplaceAll(FilteredApps);
}
```

- [ ] **Step 2: Add drag-and-drop attributes to AppGrid in XAML**

In `MainWindow.xaml`, update `AppGrid` to add the drag-and-drop attributes. `CanReorderItems` is bound to `IsFilterEmpty` so drag-and-drop automatically disables itself when a filter is active:

```xml
<GridView x:Uid="MainWindow_ShortcutsGrid"
          x:Name="AppGrid"
          ...
          CanReorderItems="{x:Bind ViewModel.IsFilterEmpty, Mode=OneWay}"
          AllowDrop="{x:Bind ViewModel.IsFilterEmpty, Mode=OneWay}"
          CanDragItems="{x:Bind ViewModel.IsFilterEmpty, Mode=OneWay}"
          DragItemsCompleted="AppGrid_DragItemsCompleted"
          KeyDown="Grid_KeyDown"
          CharacterReceived="Grid_CharacterReceived">
```

Do NOT add `CanReorderItems` to `GroupedAppGrid` — leave that grid unchanged.

- [ ] **Step 3: Add DragItemsCompleted handler in MainWindow.xaml.cs**

Add after `Grid_CharacterReceived`:

```csharp
private void AppGrid_DragItemsCompleted(ListViewBase sender, DragItemsCompletedEventArgs args)
{
    // FilteredApps is already in the new order (WinUI modified it in-place during drag).
    // Persist the new order to settings so it survives app restarts.
    ViewModel.PersistItemOrder();
}
```

- [ ] **Step 4: Build**

```bash
dotnet build Launchbox.csproj -p:Platform=x64
```

Expected: succeeds.

> **Note on SelectionMode:** WinUI `CanReorderItems` works with `SelectionMode="None"` in most cases, but if drag handles don't appear during testing, change `AppGrid`'s `SelectionMode` from `"None"` to `"Single"`. With `SelectionMode="Single"` and `IsItemClickEnabled="True"`, item clicks still fire `ItemClick` (which routes to `LaunchAppCommand`) — selection is cosmetically visible but functionally non-interfering.

- [ ] **Step 5: Format and run full test suite**

```bash
dotnet format Launchbox.sln
dotnet test Launchbox.Tests/Launchbox.Tests.csproj
```

Expected: all tests pass.

- [ ] **Step 6: Commit**

```bash
git add MainWindow.xaml MainWindow.xaml.cs ViewModels/MainViewModel.cs
git commit -m "feat: add drag-and-drop shortcut reordering to merged-mode app grid"
```

---

## Self-Review Checklist

- [x] **Hotkey** — already implemented, explicitly noted at top
- [x] **Phase A** — Enter-to-launch via `FocusManager.GetFocusedElement`, type-to-search via `CharacterReceived`; both grids covered
- [x] **Phase B** — `SetFolderSequence` added bottom-up (Manager → Service → ViewModel → View); TDD at each layer; appends unrepresented folders to prevent silent data loss
- [x] **Phase C** — item order stored as `Dictionary<string, List<string>>` JSON keyed by folder path; custom order applied in `LoadAppsAsync` with alphabetical fallback for new/unlisted items; `FilteredApps` promoted to observable collection to enable in-place WinUI reorder; drag disabled when filter is active; `PersistItemOrder` syncs `Apps` after drag
- [x] **No placeholders** — all method signatures, field names, and code blocks are concrete
- [x] **Type consistency** — `BulkObservableCollection<AppItem>`, `RebuildFilteredApps()`, `PersistItemOrder()`, `SetFolderSequence()`, `GetItemOrder()`/`SetItemOrder()` used consistently across tasks
- [x] **Grouped mode** — explicitly out of scope for shortcut drag-and-drop (cross-folder drop implies file-move; noted in Task 7)
