# Design: CommunityToolkit.Mvvm Adoption

**Date:** 2026-03-14
**Status:** Approved

---

## Overview

Replace hand-rolled MVVM infrastructure with CommunityToolkit.Mvvm to reduce maintenance burden and establish source-generator-based patterns for future ViewModels.

---

## Package Setup

Add `CommunityToolkit.Mvvm` (latest stable 8.4.x) to both projects:
- `Launchbox.csproj`
- `Launchbox.Tests/Launchbox.Tests.csproj` (needed because test project file-links production code that references toolkit types)

---

## Deleted Files

Remove these 6 files:

| File | Lines | Reason |
|------|-------|--------|
| `Helpers/ObservableObject.cs` | 27 | Replaced by `CommunityToolkit.Mvvm.ComponentModel.ObservableObject` |
| `Helpers/SimpleCommand.cs` | 26 | Replaced by `RelayCommand` / `RelayCommand<T>` |
| `Helpers/AsyncSimpleCommand.cs` | 67 | Replaced by `AsyncRelayCommand` / `AsyncRelayCommand<T>` |
| `ViewModels/ViewModelBase.cs` | 7 | Empty passthrough; ViewModels inherit toolkit's `ObservableObject` directly |
| `Launchbox.Tests/SimpleCommandTests.cs` | — | Tests deleted hand-rolled class |
| `Launchbox.Tests/AsyncSimpleCommandTests.cs` | — | Tests deleted hand-rolled class |
| `Launchbox.Tests/ObservableObjectTests.cs` | — | Tests deleted hand-rolled class |

**Retained:** `Helpers/BulkObservableCollection.cs` — no toolkit equivalent. Inherits from `System.Collections.ObjectModel.ObservableCollection<T>` (not our `ObservableObject`), so it is unaffected.

---

## ViewModel Changes

### MainViewModel

```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

public partial class MainViewModel : ObservableObject, IDisposable
```

**`partial` is required** for source generators.

**Properties converted to `[ObservableProperty]`:**

| Property | Attributes | Notes |
|----------|-----------|-------|
| `IsEmpty` | `[ObservableProperty(Setter = Setter.Private)]` | Replaces `SetProperty(ref _isEmpty, value)` with `private bool _isEmpty;`. `Setter.Private` preserves the existing `private set` encapsulation. |
| `FilterText` | `[ObservableProperty]`, `[NotifyPropertyChangedFor(nameof(FilteredApps))]`, `[NotifyPropertyChangedFor(nameof(HasNoMatches))]` | Replaces manual setter with equality check + 3 `OnPropertyChanged` calls |

**Properties that stay manual** (computed, no backing field):
`FilteredApps`, `HasNoMatches`, `ItemWidth`, `ItemHeight`, `IconSize`, `ToggleWindowText`

**Commands converted to `[RelayCommand]`:**

| Method | Attribute | Generated Property Name | Notes |
|--------|-----------|------------------------|-------|
| `LoadAppsAsync()` | `[RelayCommand(AllowConcurrentExecutions = true)]` | `LoadAppsCommand` | The source generator strips the `Async` suffix by default. Method already has internal try/catch; `AllowConcurrentExecutions` preserves current behavior (no re-entrancy guard) |
| `LaunchApp(object?)` | `[RelayCommand]` | `LaunchAppCommand` | |
| `OpenShortcutsFolder()` | `[RelayCommand]` | `OpenShortcutsFolderCommand` | |
| `ToggleWindow()` | `[RelayCommand]` | `ToggleWindowCommand` | Currently an inline lambda `() => _windowService.ToggleVisibility()`; extract to a named private method |
| `Exit()` | `[RelayCommand]` | `ExitCommand` | Currently an inline lambda `() => _windowService.Exit()`; extract to named private method |
| `OpenSettings()` | `[RelayCommand]` | `OpenSettingsCommand` | Currently an inline lambda `() => _windowService.OpenSettings()`; extract to named private method |

The manual `ICommand` field declarations and constructor assignments are removed.

**No XAML/code-behind binding changes needed.** The source generator strips the `Async` suffix, so `LoadAppsAsync()` → `LoadAppsCommand` and all existing bindings match.

### SettingsViewModel

```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

public partial class SettingsViewModel : ObservableObject, IDisposable
```

**Commands converted:**

| Method | Attribute | Generated Property Name |
|--------|-----------|------------------------|
| `ResetPosition()` | `[RelayCommand]` | `ResetPositionCommand` |
| `BrowseFolderAsync()` | `[RelayCommand(AllowConcurrentExecutions = true)]` | `BrowseFolderCommand` |

**No XAML binding changes needed.** The source generator strips the `Async` suffix, so `BrowseFolderAsync()` → `BrowseFolderCommand` matches the existing binding.

**Properties that stay manual** (all pass-through facades over `SettingsService`, no backing fields):
`ShortcutsPath`, `SelectedModifier`, `SelectedGridSize`, `HotkeyKeyString`, `RunAtStartup`, `Modifiers`, `GridSizeOptions`

The `OnServicePropertyChanged` relay pattern is required and persists regardless of toolkit adoption.

### AppItem

```csharp
using CommunityToolkit.Mvvm.ComponentModel;

public class AppItem : ObservableObject
```

Base class swap from `Launchbox.Helpers.ObservableObject` to `CommunityToolkit.Mvvm.ComponentModel.ObservableObject`. All three properties (`Name`, `Path`, `Icon`) use `SetProperty(ref field, value)` — the toolkit's inherited `SetProperty` is a drop-in replacement. No source generators needed (no `partial`, no `[ObservableProperty]`).

### SettingsService

```csharp
using CommunityToolkit.Mvvm.ComponentModel;

public class SettingsService : ObservableObject
```

Base class swap only. All properties remain manual:
- `ShortcutsPath`, `HotkeyModifiers`, `HotkeyKey`, `GridSize` — store-backed, no backing fields
- `IsRunAtStartup` — has `private set`; toolkit `[ObservableProperty]` generates a public setter by default, so it stays as manual `SetProperty(ref _isRunAtStartup, value)` using the toolkit's inherited method

---

## Error Handling

The hand-rolled `AsyncSimpleCommand` caught exceptions and logged them via `PathSecurity.GetSafeExceptionMessage`. The toolkit's `AsyncRelayCommand` does not do this.

**No wrapper needed.** Both async command methods (`LoadAppsAsync`, `BrowseFolderAsync`) already have internal `try/catch` blocks with sanitized `Trace.WriteLine` logging. The `AsyncSimpleCommand`'s outer catch was a redundant safety net.

---

## Test Impact

**Deleted:** `SimpleCommandTests.cs`, `AsyncSimpleCommandTests.cs`, `ObservableObjectTests.cs`

**Removed from test `.csproj`:** 4 `<Compile Include>` entries for `ObservableObject.cs`, `SimpleCommand.cs`, `AsyncSimpleCommand.cs`, `ViewModelBase.cs`

**Updated:** 1 comment in `AppItemTests.cs` referencing `SimpleCommand` → `RelayCommand`

**Unchanged:** All ViewModel tests (`MainViewModelTests.cs`, `SettingsViewModelTests.cs`) test through the public ViewModel API, not through command classes directly. They are unaffected.

---

## Documentation Updates

**AGENTS.md:**
- Project structure: remove `SimpleCommand.cs` line, note commands use `[RelayCommand]`
- Dependencies table: add `CommunityToolkit.Mvvm` row
- Architecture notes: mention toolkit dependency

**CLAUDE.md:**
- Helpers section: remove `SimpleCommand` / `AsyncSimpleCommand` references, add toolkit note
- Common Tasks: note that new commands should use `[RelayCommand]`

---

## Implementation Order

1. Add NuGet package to both `.csproj` files
2. Delete 4 hand-rolled infrastructure files
3. Update `AppItem` base class
4. Update `SettingsService` base class
5. Update `MainViewModel`: base class, `partial`, `[ObservableProperty]`, `[RelayCommand]`, remove manual command fields
6. Update `SettingsViewModel`: base class, `partial`, `[RelayCommand]`, remove manual command fields
7. Delete test files (`SimpleCommandTests.cs`, `AsyncSimpleCommandTests.cs`, `ObservableObjectTests.cs`), update test `.csproj`, update `AppItemTests.cs` comment
8. Update `AGENTS.md` and `CLAUDE.md`
9. Run `dotnet format`, build, run all tests
