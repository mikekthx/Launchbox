# WindowService Testability Seam — Design Spec

**Date:** 2026-04-16
**Issue:** #337
**Branch:** `feat/337-window-service-testability`

## Problem

`WindowService` is the highest-risk integration point in Launchbox (hotkey registration, WndProc hook, position persistence, visibility toggling, settings sync) yet has zero direct unit coverage. The blocker is that `Microsoft.UI.Xaml.Window` and `AppWindow` have no testable seam — constructing or using them requires a real WinUI runtime and window handle.

## Goal

Introduce two narrow interfaces that let `WindowService`'s core logic be exercised in unit tests without WinUI or Win32 runtime dependencies. Add a `WindowServiceTests` class covering the priority scenarios called out in the issue.

## New Interfaces

### `IAppWindowAdapter` — `Services/IAppWindowAdapter.cs`

Wraps the `AppWindow` and `Window.Visible` surface used by `WindowService`.

```csharp
public interface IAppWindowAdapter
{
    bool IsVisible { get; }
    Windows.Graphics.SizeInt32 Size { get; }
    Windows.Graphics.PointInt32 Position { get; }
    event EventHandler? PositionOrSizeChanged;
    void Show();
    void Hide();
    void Move(Windows.Graphics.PointInt32 point);
    void Resize(Windows.Graphics.SizeInt32 size);
    void MoveAndResize(Windows.Graphics.RectInt32 rect);
    Windows.Graphics.RectInt32 GetWorkArea();
    bool IsRectOnAnyDisplay(Windows.Graphics.RectInt32 rect);
}
```

- `GetWorkArea()` abstracts `DisplayArea.GetFromWindowId(_appWindow.Id, DisplayAreaFallback.Primary).WorkArea`
- `IsRectOnAnyDisplay()` abstracts `DisplayArea.GetFromRect(rect, DisplayAreaFallback.None) != null` (used in `RestoreWindowPosition`)
- `PositionOrSizeChanged` is raised by the production adapter only when `AppWindow.Changed` fires with `DidPositionChange || DidSizeChange`

### `INativeHotkeyService` — `Services/INativeHotkeyService.cs`

Wraps the two Win32 hotkey calls used in `UpdateHotkey`.

```csharp
public interface INativeHotkeyService
{
    bool RegisterHotKey(IntPtr hWnd, int id, uint modifiers, uint virtualKey);
    bool UnregisterHotKey(IntPtr hWnd, int id);
}
```

## New Production Adapters

### `WinUIAppWindowAdapter` — `Services/WinUIAppWindowAdapter.cs`

- Constructor takes `AppWindow appWindow, Window window`
- `IsVisible` delegates to `window.Visible`
- `Size/Position` delegate to `appWindow.Size/Position`
- `Show/Hide/Move/Resize/MoveAndResize` delegate to corresponding `appWindow` methods
- `GetWorkArea()`: calls `DisplayArea.GetFromWindowId(appWindow.Id, DisplayAreaFallback.Primary).WorkArea`
- `IsRectOnAnyDisplay()`: calls `DisplayArea.GetFromRect(rect, DisplayAreaFallback.None) != null`
- Subscribes to `appWindow.Changed` internally; raises `PositionOrSizeChanged` only when `args.DidPositionChange || args.DidSizeChange`
- Implements `IDisposable` to unsubscribe from `appWindow.Changed`

### `Win32HotkeyService` — `Services/Win32HotkeyService.cs`

One-line wrappers:
```csharp
public bool RegisterHotKey(IntPtr hWnd, int id, uint mod, uint key)
    => NativeMethods.RegisterHotKey(hWnd, id, (uint)mod, (uint)key);

public bool UnregisterHotKey(IntPtr hWnd, int id)
    => NativeMethods.UnregisterHotKey(hWnd, id);
```

## `WindowService` Changes

### Field replacements

| Before | After |
|--------|-------|
| `AppWindow? _appWindow` | `IAppWindowAdapter? _adapter` |
| `_window.Visible` (in `IsVisible`) | `_adapter?.IsVisible ?? false` |
| `_appWindow.Show/Hide/Move/Resize/MoveAndResize` | `_adapter.Show/Hide/Move/Resize/MoveAndResize` |
| `_appWindow.Size/Position` | `_adapter.Size/Position` |
| `_appWindow.Changed += AppWindow_Changed` | `_adapter.PositionOrSizeChanged += Adapter_PositionOrSizeChanged` |
| `DisplayArea.GetFromWindowId(...)` | `_adapter.GetWorkArea()` |
| `DisplayArea.GetFromRect(...)` | `_adapter.IsRectOnAnyDisplay(rect)` |
| `NativeMethods.RegisterHotKey(...)` | `_hotkeyService.RegisterHotKey(...)` |
| `NativeMethods.UnregisterHotKey(...)` | `_hotkeyService.UnregisterHotKey(...)` |

### `Initialize()` changes

After obtaining the real `AppWindow` from the HWND:
```csharp
_adapter = new WinUIAppWindowAdapter(appWindow, _window);
_hotkeyService = new Win32HotkeyService();
```
All subsequent code in `Initialize()` uses `_adapter` and `_hotkeyService`. The WndProc/HWND setup is unchanged.

### Internal test constructor

```csharp
internal WindowService(
    IAppWindowAdapter adapter,
    INativeHotkeyService hotkeyService,
    WindowPositionManager positionManager,
    SettingsService settingsService,
    IDispatcher dispatcher)
```

This constructor sets `_adapter` and `_hotkeyService` directly, subscribes to `SettingsService.PropertyChanged`, and skips all Win32/WinUI initialization. Tests use it exclusively. The existing public constructor is unchanged.

### `Dispose()` changes

Unsubscribe `_adapter.PositionOrSizeChanged` and dispose the adapter if it is `IDisposable`.

## New Test Mocks

### `MockAppWindowAdapter` — `Launchbox.Tests/MockAppWindowAdapter.cs`

- Configurable `IsVisible`, `Size`, `Position`, `WorkArea` (returned by `GetWorkArea()`)
- `IsRectOnAnyDisplay` returns `true` by default (configurable)
- Tracks: `ShowCount`, `HideCount`, last `Move`/`Resize`/`MoveAndResize` call arguments
- `FirePositionOrSizeChanged()` test helper raises the event

### `MockNativeHotkeyService` — `Launchbox.Tests/MockNativeHotkeyService.cs`

- `RegisterResults` — a `Queue<bool>` consumed in order by each `RegisterHotKey` call (default: all `true`)
- Dequeues one result per call; if the queue is empty, returns `true`
- Tracks: ordered list of `RegisterHotKey`/`UnregisterHotKey` calls (hWnd, id, mod, key)

## Tests — `WindowServiceTests.cs`

All tests use the internal constructor. `SettingsService` is constructed with `MockSettingsStore`; changing `settingsService.HotkeyModifiers` or `HotkeyKey` fires `UpdateHotkey` synchronously via `MockDispatcher`.

| Test | Scenario |
|------|----------|
| `ClampToWorkArea_HeightExceedsWorkArea_ShrinksToFit` | Window taller than work area → Resize called with clamped height |
| `ClampToWorkArea_WindowOffScreenHorizontally_Repositions` | Window X beyond work area right edge → Move called to re-center |
| `ClampToWorkArea_SmallDisplay_ClampsToMinimumDimensions` | Work area smaller than MIN_WINDOW_HEIGHT/WIDTH → Resize called with minimum constants |
| `OnActivated_Deactivated_HidesWindow` | `OnActivated(isDeactivated: true)` → `Hide` called |
| `OnActivated_Activated_DoesNotHideWindow` | `OnActivated(isDeactivated: false)` → `Hide` not called |
| `ToggleVisibility_WhenVisible_HidesWindow` | adapter visible + hasPositioned → `Hide` called |
| `ToggleVisibility_WhenHidden_ShowsWindow` | adapter not visible → `Show` called |
| `UpdateHotkey_Success_UpdatesCurrentKey` | Register succeeds → `_currentMod/_currentKey` updated, `_isHotkeyRegistered` true |
| `UpdateHotkey_Fails_FallsBackToPreviousKey` | Register fails, fallback succeeds → old key re-registered |
| `UpdateHotkey_Fails_RevertsSettingsToOldKey` | Register fails, fallback succeeds → `settingsService.HotkeyModifiers/Key` reverted |
| `UpdateHotkey_FailsAndFallbackFails_IsHotkeyRegisteredFalse` | Both fail → `_isHotkeyRegistered` becomes false |

## File Checklist

**New production files (must be added to `Launchbox.Tests/Launchbox.Tests.csproj` via `<Compile Include>`):**
- `Services/IAppWindowAdapter.cs`
- `Services/INativeHotkeyService.cs`
- `Services/WinUIAppWindowAdapter.cs`
- `Services/Win32HotkeyService.cs`

**Modified production files:**
- `Services/WindowService.cs`

**New test files (auto-included, no csproj edit needed):**
- `Launchbox.Tests/MockAppWindowAdapter.cs`
- `Launchbox.Tests/MockNativeHotkeyService.cs`
- `Launchbox.Tests/WindowServiceTests.cs`

## Constraints

- `UpdateHotkey` remains `private`; tests trigger it via `SettingsService` property changes (synchronous via `MockDispatcher`)
- `_freshShow` sequencing is tested indirectly through `ToggleVisibility` — the `Showing` event fires before `Show()`, which the mock can verify via `ShowCount`
- `WndProc`, `IsIconic`, `ShowWindow`, `SetForegroundWindow` remain un-mocked in this PR (out of scope)
- The existing public `WindowService` constructor signature is unchanged; `MainWindow.xaml.cs` needs no edits
