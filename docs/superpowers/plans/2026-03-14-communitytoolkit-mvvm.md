# CommunityToolkit.Mvvm Adoption Implementation Plan

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development (if subagents available) or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace hand-rolled MVVM infrastructure with CommunityToolkit.Mvvm to reduce maintenance and enable source-generator patterns for future ViewModels.

**Architecture:** Add CommunityToolkit.Mvvm NuGet package, delete 4 hand-rolled files (ObservableObject, SimpleCommand, AsyncSimpleCommand, ViewModelBase), update all consumers to use toolkit base classes and source-generator attributes (`[ObservableProperty]`, `[RelayCommand]`).

**Tech Stack:** CommunityToolkit.Mvvm 8.4.x, .NET 10, WinUI 3

---

## Chunk 1: Package Setup + Infrastructure Deletion

### Task 1: Add NuGet Package and Delete Hand-Rolled Files

**Files:**
- Modify: `Launchbox.csproj` (add PackageReference)
- Modify: `Launchbox.Tests/Launchbox.Tests.csproj` (add PackageReference, remove 4 `<Compile Include>` entries)
- Delete: `Helpers/ObservableObject.cs`
- Delete: `Helpers/SimpleCommand.cs`
- Delete: `Helpers/AsyncSimpleCommand.cs`
- Delete: `ViewModels/ViewModelBase.cs`

- [ ] **Step 1.1: Add CommunityToolkit.Mvvm to both projects**

```bash
cd C:\Users\Mike\source\repos\Launchbox
dotnet add Launchbox.csproj package CommunityToolkit.Mvvm
dotnet add Launchbox.Tests/Launchbox.Tests.csproj package CommunityToolkit.Mvvm
```

- [ ] **Step 1.2: Remove 4 `<Compile Include>` entries from test .csproj**

In `Launchbox.Tests/Launchbox.Tests.csproj`, remove these 4 lines:

```xml
<!-- Line 25 --> <Compile Include="..\Helpers\SimpleCommand.cs" Link="Helpers\SimpleCommand.cs" />
<!-- Line 26 --> <Compile Include="..\Helpers\AsyncSimpleCommand.cs" Link="Helpers\AsyncSimpleCommand.cs" />
<!-- Line 69 --> <Compile Include="..\Helpers\ObservableObject.cs" Link="Helpers\ObservableObject.cs" />
<!-- Line 70 --> <Compile Include="..\ViewModels\ViewModelBase.cs" Link="ViewModels\ViewModelBase.cs" />
```

- [ ] **Step 1.3: Delete the 4 hand-rolled files and 3 obsolete test files**

```bash
rm Helpers/ObservableObject.cs Helpers/SimpleCommand.cs Helpers/AsyncSimpleCommand.cs ViewModels/ViewModelBase.cs
rm Launchbox.Tests/SimpleCommandTests.cs Launchbox.Tests/AsyncSimpleCommandTests.cs Launchbox.Tests/ObservableObjectTests.cs
```

The test files must be deleted now (not later) because they reference the deleted classes and would prevent the test project from compiling at any subsequent checkpoint.

Do NOT commit yet — the build will be broken until consumers are updated.

---

### Task 2: Update AppItem and SettingsService Base Classes

**Files:**
- Modify: `Models/AppItem.cs`
- Modify: `Services/SettingsService.cs`

These two files inherit `ObservableObject` directly (not `ViewModelBase`) and use only `SetProperty` and `OnPropertyChanged` — a straight base class swap.

- [ ] **Step 2.1: Update AppItem**

In `Models/AppItem.cs`:

Change line 1:
```csharp
// Before
using Launchbox.Helpers;
// After
using CommunityToolkit.Mvvm.ComponentModel;
```

Change line 5:
```csharp
// Before
public class AppItem : ObservableObject
// After (no change needed — same class name, different namespace via using)
public class AppItem : ObservableObject
```

The class declaration stays the same — only the `using` changes. `SetProperty(ref field, value)` is provided by the toolkit's `ObservableObject` with the same signature.

- [ ] **Step 2.2: Update SettingsService**

In `Services/SettingsService.cs`:

Add `using CommunityToolkit.Mvvm.ComponentModel;` alongside the existing `using Launchbox.Helpers;` (which must be retained — `SettingsService` uses `PathSecurity`, `Constants`, and `GridSize` from that namespace).

The class declaration `public class SettingsService : ObservableObject` stays the same. All `OnPropertyChanged()` and `SetProperty()` calls are compatible.

- [ ] **Step 2.3: Verify build compiles (expect errors from ViewModels)**

```bash
dotnet build Launchbox.csproj -p:Platform=x64 2>&1
```

Expected: Build errors in `MainViewModel.cs` and `SettingsViewModel.cs` (they still reference `ViewModelBase`, `SimpleCommand`, `AsyncSimpleCommand`). `AppItem` and `SettingsService` should compile cleanly.

---

### Task 3: Update MainViewModel

**Files:**
- Modify: `ViewModels/MainViewModel.cs`

- [ ] **Step 3.1: Update using statements and class declaration**

Replace:
```csharp
using Launchbox.Helpers;
```
With:
```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Launchbox.Helpers;
```

Change line 17:
```csharp
// Before
public class MainViewModel : ViewModelBase, IDisposable
// After
public partial class MainViewModel : ObservableObject, IDisposable
```

- [ ] **Step 3.2: Convert IsEmpty to [ObservableProperty]**

Replace lines 30-35:
```csharp
// Before
private bool _isEmpty;
public bool IsEmpty
{
    get => _isEmpty;
    private set => SetProperty(ref _isEmpty, value);
}
```
With:
```csharp
[ObservableProperty(Setter = Setter.Private)]
private bool _isEmpty;
```

Update all references from `IsEmpty = ...` to `IsEmpty = ...` — the generated property name is the same, so no changes needed in `LoadAppsAsync` or elsewhere.

- [ ] **Step 3.3: Convert FilterText to [ObservableProperty]**

Replace lines 37-51:
```csharp
// Before
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
```
With:
```csharp
[ObservableProperty]
[NotifyPropertyChangedFor(nameof(FilteredApps))]
[NotifyPropertyChangedFor(nameof(HasNoMatches))]
private string _filterText = string.Empty;
```

- [ ] **Step 3.4: Extract inline lambdas to named methods**

Three commands currently use inline lambdas. Extract them to named private methods so `[RelayCommand]` can annotate them.

Add these three methods before `Dispose()` (around line 276):

```csharp
private void ToggleWindow() => _windowService.ToggleVisibility();

private void Exit() => _windowService.Exit();

private void OpenSettings() => _windowService.OpenSettings();
```

- [ ] **Step 3.5: Add [RelayCommand] attributes to all command methods**

Add attributes to each method:

```csharp
[RelayCommand(AllowConcurrentExecutions = true)]
public async Task LoadAppsAsync()
```

```csharp
[RelayCommand]
private void LaunchApp(object? parameter)
```

```csharp
[RelayCommand]
private void OpenShortcutsFolder()
```

```csharp
[RelayCommand]
private void ToggleWindow() => _windowService.ToggleVisibility();
```

```csharp
[RelayCommand]
private void Exit() => _windowService.Exit();
```

```csharp
[RelayCommand]
private void OpenSettings() => _windowService.OpenSettings();
```

- [ ] **Step 3.6: Remove manual command declarations and constructor assignments**

Delete the 6 command property declarations (lines 85-90):
```csharp
public ICommand LoadAppsCommand { get; }
public ICommand LaunchAppCommand { get; }
public ICommand OpenShortcutsFolderCommand { get; }
public ICommand ToggleWindowCommand { get; }
public ICommand ExitCommand { get; }
public ICommand OpenSettingsCommand { get; }
```

Delete the 6 constructor assignments (lines 122-127):
```csharp
LoadAppsCommand = new AsyncSimpleCommand(LoadAppsAsync);
LaunchAppCommand = new SimpleCommand(LaunchApp);
OpenShortcutsFolderCommand = new SimpleCommand(OpenShortcutsFolder);
ToggleWindowCommand = new SimpleCommand(() => _windowService.ToggleVisibility());
ExitCommand = new SimpleCommand(() => _windowService.Exit());
OpenSettingsCommand = new SimpleCommand(() => _windowService.OpenSettings());
```

Also remove `using System.Windows.Input;` if no other `ICommand` references remain in the file.

- [ ] **Step 3.7: Build and verify**

```bash
dotnet build Launchbox.csproj -p:Platform=x64 2>&1
```

Expected: Errors only from `SettingsViewModel.cs` (still references `ViewModelBase`, `SimpleCommand`, `AsyncSimpleCommand`). `MainViewModel` should compile.

---

### Task 4: Update SettingsViewModel

**Files:**
- Modify: `ViewModels/SettingsViewModel.cs`

- [ ] **Step 4.1: Update using statements and class declaration**

Replace:
```csharp
using Launchbox.Helpers;
```
With:
```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Launchbox.Helpers;
```

Keep `using Launchbox.Helpers;` because `Constants`, `GridSize`, and `MODIFIER_MAP` values reference it.

Change line 14:
```csharp
// Before
public class SettingsViewModel : ViewModelBase, IDisposable
// After
public partial class SettingsViewModel : ObservableObject, IDisposable
```

- [ ] **Step 4.2: Extract ResetPosition to a named method**

Currently `ResetPositionCommand = new SimpleCommand(() => _windowService.ResetPosition());` — the lambda needs to become a named method.

Add before `Dispose()`:

```csharp
private void ResetPosition() => _windowService.ResetPosition();
```

- [ ] **Step 4.3: Add [RelayCommand] attributes**

```csharp
[RelayCommand]
private void ResetPosition() => _windowService.ResetPosition();
```

On the existing `BrowseFolderAsync` method:
```csharp
[RelayCommand(AllowConcurrentExecutions = true)]
private async Task BrowseFolderAsync()
```

- [ ] **Step 4.4: Remove manual command declarations and constructor assignments**

Delete lines 20-21:
```csharp
public ICommand ResetPositionCommand { get; }
public ICommand BrowseFolderCommand { get; }
```

Delete lines 56-57 in the constructor:
```csharp
ResetPositionCommand = new SimpleCommand(() => _windowService.ResetPosition());
BrowseFolderCommand = new AsyncSimpleCommand(BrowseFolderAsync);
```

Also remove `using System.Windows.Input;` if no other `ICommand` references remain.

- [ ] **Step 4.5: Build the full solution**

```bash
dotnet build Launchbox.csproj -p:Platform=x64 2>&1
```

Expected: Clean build, 0 errors, 0 warnings.

- [ ] **Step 4.6: Run all tests**

```bash
dotnet test Launchbox.Tests/Launchbox.Tests.csproj --filter "Category!=Performance" -v minimal
```

Expected: All tests pass. The obsolete test files (`SimpleCommandTests.cs`, `AsyncSimpleCommandTests.cs`, `ObservableObjectTests.cs`) were already deleted in Step 1.3.

- [ ] **Step 4.7: Format and commit**

```bash
dotnet format Launchbox.sln
git add -A
git commit -m "refactor: replace hand-rolled MVVM with CommunityToolkit.Mvvm"
```

---

## Chunk 2: Test Cleanup + Documentation

### Task 5: Update Test References

**Files:**
- Modify: `Launchbox.Tests/AppItemTests.cs`

Note: The 3 obsolete test files (`SimpleCommandTests.cs`, `AsyncSimpleCommandTests.cs`, `ObservableObjectTests.cs`) were already deleted in Step 1.3.

- [ ] **Step 5.1: Update comment in AppItemTests.cs**

In `Launchbox.Tests/AppItemTests.cs` line 126, change:
```csharp
// Before
// LaunchAppCommand uses SimpleCommand(LaunchApp) which guards on 'is AppItem'
// After
// LaunchAppCommand uses RelayCommand(LaunchApp) which guards on 'is AppItem'
```

- [ ] **Step 5.2: Run all tests**

```bash
dotnet test Launchbox.Tests/Launchbox.Tests.csproj --filter "Category!=Performance" -v minimal
```

Expected: All tests pass. The deleted test files reduce the count but all remaining tests should be green.

- [ ] **Step 5.3: Run performance tests**

```bash
dotnet test Launchbox.Tests/Launchbox.Tests.csproj --filter "Category=Performance" -v minimal
```

Expected: All performance tests pass.

- [ ] **Step 5.4: Format and commit**

```bash
dotnet format Launchbox.sln
git add Launchbox.Tests/AppItemTests.cs
git commit -m "docs: update AppItemTests comment for RelayCommand"
```

---

### Task 6: Update Documentation

**Files:**
- Modify: `AGENTS.md`
- Modify: `CLAUDE.md`

- [ ] **Step 6.1: Update AGENTS.md**

**Project structure** — in the `Helpers/` tree:
- Remove the `AsyncSimpleCommand.cs` line
- Remove the `ObservableObject.cs` line
- Change `SimpleCommand.cs` description to: `SimpleCommand.cs        # Legacy ICommand (new commands use [RelayCommand])`

Actually — `SimpleCommand.cs` was deleted. Remove it entirely. The Helpers tree should now be:
```
├── Helpers/
│   ├── BooleanToVisibilityConverter.cs
│   ├── BulkObservableCollection.cs # ObservableCollection with batch-update support
│   ├── Constants.cs            # Global constants (hotkey, window size, etc.)
│   ├── GridSize.cs             # Small/Medium/Large grid size enum
│   ├── IconHelper.cs           # Icon extraction helpers
│   ├── ImageHeaderParser.cs    # Image format detection
│   ├── ListViewBaseExtensions.cs # Attached property for ItemClick → ICommand binding
│   └── PathSecurity.cs         # Path validation and sanitization
```

In the `ViewModels/` tree, remove the `ViewModelBase.cs` line.

**Dependencies table** — add a row:
```
| CommunityToolkit.Mvvm 8.4.x    | MVVM source generators  |
```

**Architecture notes** — in the MVVM Pattern section, add a note:
> ViewModels use CommunityToolkit.Mvvm source generators (`[ObservableProperty]`, `[RelayCommand]`). New commands should use `[RelayCommand]` on a private method rather than manual `ICommand` properties.

- [ ] **Step 6.2: Update CLAUDE.md**

In the **Helpers** section, remove references to `SimpleCommand` / `AsyncSimpleCommand`. Update the description to note that `BulkObservableCollection` is the main custom helper remaining.

In the **Common Tasks** section under "Add a new service", add:
> For new commands, use `[RelayCommand]` on a private method. For async commands, use `[RelayCommand(AllowConcurrentExecutions = true)]` on a `private async Task` method.

- [ ] **Step 6.3: Format and commit**

```bash
dotnet format Launchbox.sln
git add AGENTS.md CLAUDE.md
git commit -m "docs: update AGENTS.md and CLAUDE.md for CommunityToolkit.Mvvm"
```
