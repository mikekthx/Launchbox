# Search Bar, Grid Size, TODO Audit, Performance Test — Implementation Plan

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development (if subagents available) or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a live search/filter bar to the main window, configurable Small/Medium/Large icon grid sizes in Settings, a TODO audit pass, and a performance test for large shortcut collections.

**Architecture:** `MainViewModel` gains computed `FilteredApps` / `FilterText` / `HasNoMatches` properties (filter) and `ItemWidth` / `ItemHeight` / `IconSize` properties (grid size). A new `GridSize` enum lives in `Helpers/`. `SettingsService` persists the grid size. The DataTemplate uses `AppGrid.Tag = ViewModel` as a relay to access ViewModel properties from within `x:DataType="models:AppItem"` context.

**Tech Stack:** WinUI 3, .NET 10, MVVM (no DI container), xUnit v3, `BulkObservableCollection<T>`

**Spec:** `docs/superpowers/specs/2026-03-14-search-gridsize-audit-perf-design.md`

---

## Chunk 1: GridSize Enum + SettingsService

### Task 1: `GridSize` enum

**Files:**
- Create: `Helpers/GridSize.cs`

- [ ] **Step 1.1: Create `Helpers/GridSize.cs`**

```csharp
namespace Launchbox.Helpers;

public enum GridSize { Small, Medium, Large }
```

- [ ] **Step 1.2: Add `GridSize.cs` to the test project's file-link list**

In `Launchbox.Tests/Launchbox.Tests.csproj`, add (in the `Helpers` group near line 44):

```xml
<Compile Include="..\Helpers\GridSize.cs" Link="Helpers\GridSize.cs" />
```

- [ ] **Step 1.3: Build to verify no errors**

```bash
dotnet build Launchbox.csproj -p:Platform=x64
```
Expected: 0 errors, 0 warnings.

---

### Task 2: `SettingsService.GridSize` property

**Files:**
- Modify: `Services/SettingsService.cs`
- Modify: `Launchbox.Tests/SettingsViewModelTests.cs` (new tests)

- [ ] **Step 2.1: Write failing test for `GridSize` default**

Add to `Launchbox.Tests/SettingsViewModelTests.cs`:

```csharp
[Fact]
public void GridSize_Default_IsMedium()
{
    var store = new MockSettingsStore();
    var service = new SettingsService(store, new MockStartupService());
    Assert.Equal(GridSize.Medium, service.GridSize);
}
```

- [ ] **Step 2.2: Run test to confirm it fails**

```bash
dotnet test Launchbox.Tests/Launchbox.Tests.csproj --filter "FullyQualifiedName~GridSize_Default_IsMedium"
```
Expected: FAIL — `SettingsService` has no `GridSize` property.

- [ ] **Step 2.3: Add `GridSize` property to `SettingsService`**

Add after `HotkeyKey` property in `Services/SettingsService.cs`:

```csharp
public GridSize GridSize
{
    get
    {
        if (_store.TryGetValue(nameof(GridSize), out var val) && val is string s
            && Enum.TryParse<GridSize>(s, out var parsed))
        {
            return parsed;
        }
        return GridSize.Medium;
    }
    set
    {
        if (GridSize != value)
        {
            _store.SetValue(nameof(GridSize), value.ToString());
            OnPropertyChanged();
        }
    }
}
```

Add `using Launchbox.Helpers;` to the using block in `Services/SettingsService.cs` (alphabetical order: after `Launchbox.Helpers` is already present — check and add if missing).

- [ ] **Step 2.4: Write test for persistence**

Add to `Launchbox.Tests/SettingsViewModelTests.cs`:

```csharp
[Fact]
public void GridSize_WhenSet_Persists()
{
    var store = new MockSettingsStore();
    var service = new SettingsService(store, new MockStartupService());
    service.GridSize = GridSize.Large;

    // Re-read from same store
    var service2 = new SettingsService(store, new MockStartupService());
    Assert.Equal(GridSize.Large, service2.GridSize);
}

[Fact]
public void GridSize_WhenSet_RaisesPropertyChanged()
{
    var store = new MockSettingsStore();
    var service = new SettingsService(store, new MockStartupService());
    string? changedProperty = null;
    service.PropertyChanged += (_, e) => changedProperty = e.PropertyName;

    service.GridSize = GridSize.Small;

    Assert.Equal(nameof(SettingsService.GridSize), changedProperty);
}
```

- [ ] **Step 2.5: Add `GridSize` import to test file if needed**

`Launchbox.Tests/SettingsViewModelTests.cs` already imports `Launchbox.Helpers` — verify the `using` is present or add it.

- [ ] **Step 2.6: Run all new tests**

```bash
dotnet test Launchbox.Tests/Launchbox.Tests.csproj --filter "FullyQualifiedName~GridSize"
```
Expected: 3 tests pass.

- [ ] **Step 2.7: Run full test suite**

```bash
dotnet test Launchbox.Tests/Launchbox.Tests.csproj
```
Expected: all tests pass.

- [ ] **Step 2.8: Format and commit**

```bash
dotnet format Launchbox.sln
git add Helpers/GridSize.cs Services/SettingsService.cs Launchbox.Tests/Launchbox.Tests.csproj Launchbox.Tests/SettingsViewModelTests.cs
git commit -m "feat: add GridSize enum and SettingsService.GridSize property"
```

---

## Chunk 2: MainViewModel Size Properties + SettingsViewModel + SettingsWindow XAML

### Task 3: `MainViewModel` size properties

**Files:**
- Modify: `ViewModels/MainViewModel.cs`
- Modify: `Launchbox.Tests/MainViewModelTests.cs`

- [ ] **Step 3.1: Add required `using` directives to `MainViewModelTests.cs`**

`MainViewModelTests.cs` needs two new `using` statements (check and add if missing):

```csharp
using Launchbox.Helpers;   // for GridSize enum
using System.Linq;         // for .First(), .Count() on IEnumerable<AppItem>
```

- [ ] **Step 3.2: Write failing tests**

Add to `Launchbox.Tests/MainViewModelTests.cs`:

```csharp
[Fact]
public void ItemWidth_Default_Is110()
{
    var vm = CreateViewModel();
    Assert.Equal(110, vm.ItemWidth);
}

[Fact]
public void ItemWidth_WhenGridSizeSmall_Is80()
{
    _settingsService.GridSize = GridSize.Small;
    var vm = CreateViewModel();
    Assert.Equal(80, vm.ItemWidth);
}

[Fact]
public void ItemHeight_WhenGridSizeLarge_Is165()
{
    _settingsService.GridSize = GridSize.Large;
    var vm = CreateViewModel();
    Assert.Equal(165, vm.ItemHeight);
}

[Fact]
public void IconSize_WhenGridSizeSmall_Is32()
{
    _settingsService.GridSize = GridSize.Small;
    var vm = CreateViewModel();
    Assert.Equal(32, vm.IconSize);
}

[Fact]
public void ItemWidth_WhenGridSizeChanges_RaisesPropertyChanged()
{
    var vm = CreateViewModel();
    string? changedProperty = null;
    vm.PropertyChanged += (_, e) => changedProperty = e.PropertyName;

    _settingsService.GridSize = GridSize.Large;

    Assert.Equal(nameof(MainViewModel.ItemWidth), changedProperty);
}
```

- [ ] **Step 3.3: Run to confirm they fail**

```bash
dotnet test Launchbox.Tests/Launchbox.Tests.csproj --filter "FullyQualifiedName~ItemWidth|FullyQualifiedName~ItemHeight|FullyQualifiedName~IconSize"
```
Expected: FAIL.

- [ ] **Step 3.4: Add size properties to `MainViewModel`**

Add `using Launchbox.Helpers;` if not already present (it is already there).

Add these read-only properties after `IsEmpty` in `ViewModels/MainViewModel.cs`:

```csharp
public int ItemWidth => _settingsService.GridSize switch
{
    GridSize.Small => 80,
    GridSize.Large => 140,
    _ => 110,
};

public int ItemHeight => _settingsService.GridSize switch
{
    GridSize.Small => 96,
    GridSize.Large => 165,
    _ => 130,
};

public int IconSize => _settingsService.GridSize switch
{
    GridSize.Small => 32,
    GridSize.Large => 72,
    _ => 56,
};
```

- [ ] **Step 3.5: Wire `SettingsService_PropertyChanged` for `GridSize`**

In `ViewModels/MainViewModel.cs`, update `SettingsService_PropertyChanged`:

```csharp
private void SettingsService_PropertyChanged(object? sender, PropertyChangedEventArgs e)
{
    if (e.PropertyName == nameof(SettingsService.ShortcutsPath))
    {
        _ = LoadAppsAsync();
    }
    else if (e.PropertyName == nameof(SettingsService.GridSize))
    {
        OnPropertyChanged(nameof(ItemWidth));
        OnPropertyChanged(nameof(ItemHeight));
        OnPropertyChanged(nameof(IconSize));
    }
}
```

- [ ] **Step 3.6: Run size tests**

```bash
dotnet test Launchbox.Tests/Launchbox.Tests.csproj --filter "FullyQualifiedName~ItemWidth|FullyQualifiedName~ItemHeight|FullyQualifiedName~IconSize"
```
Expected: 5 tests pass.

---

### Task 4: `SettingsViewModel` grid size support

**Files:**
- Modify: `ViewModels/SettingsViewModel.cs`
- Modify: `Launchbox.Tests/SettingsViewModelTests.cs`

- [ ] **Step 4.1: Write failing tests**

Add to `Launchbox.Tests/SettingsViewModelTests.cs`:

```csharp
[Fact]
public void SelectedGridSize_Default_IsMedium()
{
    var store = new MockSettingsStore();
    var settingsService = new SettingsService(store, new MockStartupService());
    var vm = new SettingsViewModel(settingsService, new MockWindowService(), new MockFilePickerService());
    Assert.Equal("Medium", vm.SelectedGridSize);
}

[Fact]
public void SelectedGridSize_WhenSet_UpdatesSettingsService()
{
    var store = new MockSettingsStore();
    var settingsService = new SettingsService(store, new MockStartupService());
    var vm = new SettingsViewModel(settingsService, new MockWindowService(), new MockFilePickerService());
    vm.SelectedGridSize = "Large";
    Assert.Equal(GridSize.Large, settingsService.GridSize);
}

[Fact]
public void SelectedGridSize_WhenServiceChanges_RaisesPropertyChanged()
{
    var store = new MockSettingsStore();
    var settingsService = new SettingsService(store, new MockStartupService());
    var vm = new SettingsViewModel(settingsService, new MockWindowService(), new MockFilePickerService());

    string? changed = null;
    vm.PropertyChanged += (_, e) => changed = e.PropertyName;
    settingsService.GridSize = GridSize.Small;

    Assert.Equal(nameof(SettingsViewModel.SelectedGridSize), changed);
}

[Fact]
public void GridSizeOptions_ContainsThreeEntries()
{
    var store = new MockSettingsStore();
    var settingsService = new SettingsService(store, new MockStartupService());
    var vm = new SettingsViewModel(settingsService, new MockWindowService(), new MockFilePickerService());
    Assert.Equal(["Small", "Medium", "Large"], vm.GridSizeOptions);
}
```

- [ ] **Step 4.2: Run to confirm they fail**

```bash
dotnet test Launchbox.Tests/Launchbox.Tests.csproj --filter "FullyQualifiedName~SelectedGridSize|FullyQualifiedName~GridSizeOptions"
```
Expected: FAIL.

- [ ] **Step 4.3: Add `GridSizeOptions` and `SelectedGridSize` to `SettingsViewModel`**

In `ViewModels/SettingsViewModel.cs`, after the `Modifiers` property:

```csharp
public IReadOnlyList<string> GridSizeOptions { get; } = ["Small", "Medium", "Large"];

public string SelectedGridSize
{
    get => _settingsService.GridSize.ToString();
    set
    {
        if (Enum.TryParse<GridSize>(value, out var g))
            _settingsService.GridSize = g;
    }
}
```

Add `using Launchbox.Helpers;` if not already present (check existing using block; `Launchbox.Helpers` is already there).

- [ ] **Step 4.4: Wire `OnServicePropertyChanged` for `GridSize`**

In `ViewModels/SettingsViewModel.cs`, update `OnServicePropertyChanged`:

```csharp
private void OnServicePropertyChanged(object? sender, PropertyChangedEventArgs e)
{
    if (e.PropertyName == nameof(SettingsService.ShortcutsPath))
        OnPropertyChanged(nameof(ShortcutsPath));
    else if (e.PropertyName == nameof(SettingsService.IsRunAtStartup))
        OnPropertyChanged(nameof(RunAtStartup));
    else if (e.PropertyName == nameof(SettingsService.HotkeyModifiers))
        OnPropertyChanged(nameof(SelectedModifier));
    else if (e.PropertyName == nameof(SettingsService.HotkeyKey))
        OnPropertyChanged(nameof(HotkeyKeyString));
    else if (e.PropertyName == nameof(SettingsService.GridSize))
        OnPropertyChanged(nameof(SelectedGridSize));
}
```

- [ ] **Step 4.5: Run tests**

```bash
dotnet test Launchbox.Tests/Launchbox.Tests.csproj --filter "FullyQualifiedName~SelectedGridSize|FullyQualifiedName~GridSizeOptions"
```
Expected: 4 tests pass.

---

### Task 5: `SettingsWindow.xaml` — grid size ComboBox

**Files:**
- Modify: `SettingsWindow.xaml`

- [ ] **Step 5.1: Add a "Appearance" section to `SettingsWindow.xaml`**

In `SettingsWindow.xaml`, add a new `<StackPanel>` section after the `<!-- Hotkey -->` section (before the `<!-- Window -->` section):

```xml
<!-- Appearance -->
<StackPanel Spacing="8">
    <TextBlock Text="Appearance" Style="{ThemeResource SubtitleTextBlockStyle}"/>
    <StackPanel Orientation="Horizontal" Spacing="8" VerticalAlignment="Center">
        <TextBlock Text="Icon size" VerticalAlignment="Center"/>
        <ComboBox ItemsSource="{x:Bind ViewModel.GridSizeOptions}"
                  SelectedItem="{x:Bind ViewModel.SelectedGridSize, Mode=TwoWay}"
                  Width="120"
                  AutomationProperties.Name="Icon size"
                  ToolTipService.ToolTip="Set the size of icons in the launcher grid" />
    </StackPanel>
</StackPanel>
```

- [ ] **Step 5.2: Build to verify XAML is valid**

```bash
dotnet build Launchbox.csproj -p:Platform=x64
```
Expected: 0 errors, 0 warnings.

---

### Task 6: `MainWindow.xaml` + code-behind — DataTemplate size bindings

**Files:**
- Modify: `MainWindow.xaml`
- Modify: `MainWindow.xaml.cs`

- [ ] **Step 6.1: Set `AppGrid.Tag = ViewModel` in `MainWindow.xaml.cs`**

In `MainWindow.xaml.cs`, add this line **after `this.InitializeComponent();`** in the constructor (not before — `AppGrid` is only instantiated by `InitializeComponent`):

```csharp
AppGrid.Tag = ViewModel;
```

- [ ] **Step 6.2: Update the DataTemplate in `MainWindow.xaml`**

Replace the existing `DataTemplate` (lines 107–124):

```xml
<GridView.ItemTemplate>
    <DataTemplate x:DataType="models:AppItem">
        <StackPanel Width="{Binding Tag.ItemWidth, ElementName=AppGrid}"
                    Height="{Binding Tag.ItemHeight, ElementName=AppGrid}"
                    Spacing="4"
                    HorizontalAlignment="Center"
                    VerticalAlignment="Center"
                    ToolTipService.ToolTip="{x:Bind Name}"
                    AutomationProperties.Name="{x:Bind Name}">

            <Image Source="{x:Bind (media:ImageSource)Icon, Mode=OneWay}"
                   Width="{Binding Tag.IconSize, ElementName=AppGrid}"
                   Height="{Binding Tag.IconSize, ElementName=AppGrid}"
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
```

- [ ] **Step 6.3: Build and verify**

```bash
dotnet build Launchbox.csproj -p:Platform=x64
```
Expected: 0 errors, 0 warnings.

- [ ] **Step 6.4: Run full test suite**

```bash
dotnet test Launchbox.Tests/Launchbox.Tests.csproj
```
Expected: all tests pass.

- [ ] **Step 6.5: Format and commit**

```bash
dotnet format Launchbox.sln
git add ViewModels/MainViewModel.cs ViewModels/SettingsViewModel.cs Services/SettingsService.cs SettingsWindow.xaml MainWindow.xaml MainWindow.xaml.cs Launchbox.Tests/MainViewModelTests.cs Launchbox.Tests/SettingsViewModelTests.cs
git commit -m "feat: add configurable grid size (Small/Medium/Large) with Settings ComboBox"
```

---

## Chunk 3: Search / Filter Bar

### Task 7: `MainViewModel` filter properties

**Files:**
- Modify: `ViewModels/MainViewModel.cs`
- Modify: `Launchbox.Tests/MainViewModelTests.cs`

- [ ] **Step 7.1: Write failing tests**

`MockFileSystem` uses `AddFile(string fullPath)` — one call per file. It is additive (does not clear). Use `Path.Combine(_shortcutFolder, filename)` to match the folder the ViewModel queries.

Add to `Launchbox.Tests/MainViewModelTests.cs`:

```csharp
[Fact]
public async Task FilterText_WhenSet_FiltersAppsByName()
{
    _fileSystem.AddFile(Path.Combine(_shortcutFolder, "Alpha.lnk"));
    _fileSystem.AddFile(Path.Combine(_shortcutFolder, "Beta.lnk"));
    var vm = CreateViewModel();
    await vm.LoadAppsAsync();

    vm.FilterText = "alp";

    Assert.Single(vm.FilteredApps);
    Assert.Equal("Alpha", vm.FilteredApps.First().Name);
}

[Fact]
public async Task FilterText_WhenEmpty_ReturnsAllApps()
{
    _fileSystem.AddFile(Path.Combine(_shortcutFolder, "Alpha.lnk"));
    _fileSystem.AddFile(Path.Combine(_shortcutFolder, "Beta.lnk"));
    var vm = CreateViewModel();
    await vm.LoadAppsAsync();

    vm.FilterText = string.Empty;

    Assert.Equal(2, vm.FilteredApps.Count());
}

[Fact]
public async Task FilterText_CaseInsensitive_MatchesRegardlessOfCase()
{
    _fileSystem.AddFile(Path.Combine(_shortcutFolder, "Alpha.lnk"));
    var vm = CreateViewModel();
    await vm.LoadAppsAsync();

    vm.FilterText = "ALPHA";

    Assert.Single(vm.FilteredApps);
}

[Fact]
public async Task FilterText_WithNoMatch_SetsHasNoMatches()
{
    _fileSystem.AddFile(Path.Combine(_shortcutFolder, "Alpha.lnk"));
    var vm = CreateViewModel();
    await vm.LoadAppsAsync();

    vm.FilterText = "zzz";

    Assert.True(vm.HasNoMatches);
}

[Fact]
public void HasNoMatches_WhenAppsEmptyAndFilterTextEmpty_IsFalse()
{
    var vm = CreateViewModel();
    Assert.False(vm.HasNoMatches);
}

[Fact]
public async Task FilteredApps_AfterAppsReload_ReflectsNewCollection()
{
    _fileSystem.AddFile(Path.Combine(_shortcutFolder, "Alpha.lnk"));
    var vm = CreateViewModel();
    await vm.LoadAppsAsync();
    vm.FilterText = "Alpha";

    // AddFile is additive — add second matching file then reload
    _fileSystem.AddFile(Path.Combine(_shortcutFolder, "AlphaTwo.lnk"));
    await vm.LoadAppsAsync();

    Assert.Equal(2, vm.FilteredApps.Count());
}
```

- [ ] **Step 7.2: Run tests to confirm they fail**

```bash
dotnet test Launchbox.Tests/Launchbox.Tests.csproj --filter "FullyQualifiedName~FilterText|FullyQualifiedName~FilteredApps|FullyQualifiedName~HasNoMatches"
```
Expected: FAIL — `FilterText`, `FilteredApps`, `HasNoMatches` not yet defined.

- [ ] **Step 7.3: Add `FilterText`, `FilteredApps`, `HasNoMatches` to `MainViewModel`**

Add `using System.Linq;` if not already present in `ViewModels/MainViewModel.cs`.

Add these properties after `IsEmpty`:

```csharp
private string _filterText = string.Empty;
public string FilterText
{
    get => _filterText;
    set
    {
        if (_filterText != value)
        {
            _filterText = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(FilteredApps));
            OnPropertyChanged(nameof(HasNoMatches));
        }
    }
}

public IEnumerable<AppItem> FilteredApps =>
    string.IsNullOrEmpty(_filterText)
        ? Apps
        : Apps.Where(a => a.Name.Contains(_filterText, StringComparison.OrdinalIgnoreCase));

public bool HasNoMatches =>
    !string.IsNullOrEmpty(_filterText) && !FilteredApps.Any();
```

- [ ] **Step 7.4: Wire `CollectionChanged` in constructor**

In `MainViewModel` constructor, after the line `_settingsService.PropertyChanged += SettingsService_PropertyChanged;`, add:

```csharp
Apps.CollectionChanged += (_, _) =>
{
    OnPropertyChanged(nameof(FilteredApps));
    OnPropertyChanged(nameof(HasNoMatches));
};
```

- [ ] **Step 7.5: Run filter tests**

```bash
dotnet test Launchbox.Tests/Launchbox.Tests.csproj --filter "FullyQualifiedName~FilterText|FullyQualifiedName~FilteredApps|FullyQualifiedName~HasNoMatches"
```
Expected: all pass.

---

### Task 8: `MainWindow.xaml` — search TextBox + `AppGrid.ItemsSource` change

**Files:**
- Modify: `MainWindow.xaml`
- Modify: `MainWindow.xaml.cs`

- [ ] **Step 8.1: Change `AppGrid.ItemsSource` to `FilteredApps`**

In `MainWindow.xaml`, line 91, change:

```xml
ItemsSource="{x:Bind ViewModel.Apps, Mode=OneWay}"
```

to:

```xml
ItemsSource="{x:Bind ViewModel.FilteredApps, Mode=OneWay}"
```

- [ ] **Step 8.2: Add `HasNoMatches` empty-state panel**

In `MainWindow.xaml`, after the existing `IsEmpty` `StackPanel` (which ends at line 81), add a new panel for the "no matches" state:

```xml
<StackPanel HorizontalAlignment="Center"
            VerticalAlignment="Center"
            Spacing="12"
            Visibility="{Binding ViewModel.HasNoMatches, Converter={StaticResource BooleanToVisibilityConverter}}">
    <SymbolIcon Symbol="Find" RenderTransformOrigin="0.5,0.5"
                AutomationProperties.AccessibilityView="Raw">
        <SymbolIcon.RenderTransform>
            <ScaleTransform ScaleX="2" ScaleY="2"/>
        </SymbolIcon.RenderTransform>
    </SymbolIcon>
    <TextBlock Text="No matches"
               FontSize="16"
               FontWeight="SemiBold"
               HorizontalAlignment="Center"/>
    <TextBlock Text="No results for:"
               Foreground="{ThemeResource TextFillColorSecondaryBrush}"
               HorizontalAlignment="Center"/>
    <TextBlock Text="{Binding ViewModel.FilterText}"
               FontStyle="Italic"
               HorizontalAlignment="Center"/>
</StackPanel>
```

- [ ] **Step 8.3: Add search TextBox below `AppGrid`**

In `MainWindow.xaml`, add a `TextBox` after the `AppGrid` closing tag (before `</Grid>`):

```xml
<TextBox x:Name="SearchBox"
         PlaceholderText="type to filter..."
         Text="{x:Bind ViewModel.FilterText, Mode=TwoWay}"
         VerticalAlignment="Bottom"
         HorizontalAlignment="Stretch"
         Margin="20,0,20,8"
         Background="{ThemeResource CardBackgroundFillColorDefaultBrush}"
         CornerRadius="6"
         AutomationProperties.Name="Search shortcuts"
         KeyDown="SearchBox_KeyDown"/>
```

- [ ] **Step 8.4: Add Esc handler and focus logic in `MainWindow.xaml.cs`**

In `MainWindow.xaml.cs`, add the `SearchBox_KeyDown` handler:

```csharp
private void SearchBox_KeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
{
    if (e.Key == Windows.System.VirtualKey.Escape)
    {
        ViewModel.FilterText = string.Empty;
        e.Handled = true;
    }
}
```

In the existing `MainWindow_Activated` handler (inside the `if (args.WindowActivationState != WindowActivationState.Deactivated)` block), add focus:

```csharp
SearchBox.Focus(FocusState.Programmatic);
```

The handler currently reads:

```csharp
private void MainWindow_Activated(object sender, WindowActivatedEventArgs args)
{
    _windowService.OnActivated(args);
    // ... existing code
    if (args.WindowActivationState != WindowActivationState.Deactivated)
    {
        // ADD: SearchBox.Focus(FocusState.Programmatic);
    }
}
```

- [ ] **Step 8.5: Build and verify**

```bash
dotnet build Launchbox.csproj -p:Platform=x64
```
Expected: 0 errors, 0 warnings.

- [ ] **Step 8.6: Run full test suite**

```bash
dotnet test Launchbox.Tests/Launchbox.Tests.csproj
```
Expected: all tests pass.

- [ ] **Step 8.7: Format and commit**

```bash
dotnet format Launchbox.sln
git add ViewModels/MainViewModel.cs MainWindow.xaml MainWindow.xaml.cs Launchbox.Tests/MainViewModelTests.cs
git commit -m "feat: add live search/filter bar to main window"
```

---

## Chunk 4: TODO Audit + Performance Test

### Task 9: TODO Audit

**Files:**
- Modify: `TODO.md` (if stale items found)

- [ ] **Step 9.1: Build with full output**

```bash
dotnet build Launchbox.csproj -p:Platform=x64
```
Expected: 0 errors, 0 warnings (TreatWarningsAsErrors is enabled).

- [ ] **Step 9.2: Review `TODO.md`**

Open `TODO.md`. For each unchecked item, verify whether it:
- Is already done → check it off with `[x]`
- Is no longer relevant → remove or strike through with a note
- Is still pending → leave as-is

- [ ] **Step 9.3: Commit if `TODO.md` changed**

```bash
git add TODO.md
git commit -m "chore: update TODO.md — mark completed items, remove stale entries"
```

Skip this step if no changes were needed.

---

### Task 10: Performance Test

**Files:**
- Create: `Launchbox.Tests/MainViewModelPerformanceTests.cs`

- [ ] **Step 10.1: Create `MainViewModelPerformanceTests.cs`**

```csharp
using Launchbox.Services;
using Launchbox.ViewModels;
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Launchbox.Tests;

[Trait("Category", "Performance")]
public class MainViewModelPerformanceTests
{
    [Fact]
    public async Task LoadAppsAsync_With500Files_CompletesWithinTwoSeconds()
    {
        var shortcutService = new MockShortcutService();
        shortcutService.SetFiles(
            Enumerable.Range(0, 500).Select(i => $@"C:\Shortcuts\App{i}.lnk").ToArray());

        var fileSystem = new MockFileSystem();
        var iconService = new MockIconService();
        var imageFactory = new MockImageFactory();
        var dispatcher = new MockDispatcher();
        var appLauncher = new MockAppLauncher();
        var windowService = new MockWindowService();

        var store = new MockSettingsStore();
        var settingsService = new SettingsService(store, new MockStartupService());
        settingsService.ShortcutsPath = @"C:\Shortcuts";

        var vm = new MainViewModel(
            shortcutService,
            iconService,
            imageFactory,
            dispatcher,
            appLauncher,
            fileSystem,
            settingsService,
            windowService);

        var sw = Stopwatch.StartNew();
        await vm.LoadAppsAsync();
        sw.Stop();

        Assert.Equal(500, vm.Apps.Count);
        Assert.True(sw.Elapsed.TotalSeconds < 2.0,
            $"LoadAppsAsync took {sw.Elapsed.TotalSeconds:F2}s — expected < 2s");

        vm.Dispose();
    }
}
```

- [ ] **Step 10.2: Run the performance test**

```bash
dotnet test Launchbox.Tests/Launchbox.Tests.csproj --filter "Category=Performance"
```
Expected: 1 test passes within 2 seconds.

- [ ] **Step 10.3: Run full test suite (excluding performance tests for speed)**

```bash
dotnet test Launchbox.Tests/Launchbox.Tests.csproj --filter "Category!=Performance"
```
Expected: all tests pass.

- [ ] **Step 10.4: Format and commit**

```bash
dotnet format Launchbox.sln
git add Launchbox.Tests/MainViewModelPerformanceTests.cs TODO.md
git commit -m "test: add performance test for 500-shortcut LoadAppsAsync"
```

---

## Final Verification

- [ ] Full build (no warnings): `dotnet build Launchbox.csproj -p:Platform=x64`
- [ ] All non-performance tests pass: `dotnet test Launchbox.Tests/Launchbox.Tests.csproj --filter "Category!=Performance"`
- [ ] Format clean: `dotnet format Launchbox.sln --verify-no-changes`
