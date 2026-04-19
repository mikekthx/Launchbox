# WindowService Testability Seam Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Introduce `IAppWindowAdapter` and `INativeHotkeyService` seams into `WindowService` so its core logic (clamping, visibility toggling, hotkey registration) can be unit-tested without a WinUI runtime.

**Architecture:** Two new interfaces wrap the `AppWindow`/`DisplayArea` surface and the `RegisterHotKey`/`UnregisterHotKey` Win32 calls. Production adapters implement each interface for real use. `WindowService` gains an `internal` constructor that accepts the interfaces directly, used exclusively by tests. The public constructor and its callers are unchanged.

**Tech Stack:** C# / .NET 10, xUnit v3, file-linked test project (`Launchbox.Tests`)

---

## File Map

| Status | File | Role |
|--------|------|------|
| Create | `Services/IAppWindowAdapter.cs` | Interface: window position/size/visibility surface |
| Create | `Services/INativeHotkeyService.cs` | Interface: Win32 hotkey register/unregister |
| Create | `Services/WinUIAppWindowAdapter.cs` | Production adapter wrapping `AppWindow` + `Window` |
| Create | `Services/Win32HotkeyService.cs` | Production adapter wrapping `NativeMethods` hotkey calls |
| Modify | `Services/WindowService.cs` | Replace `_appWindow` with `_adapter`, hotkeys with `_hotkeyService`, add `internal` ctor |
| Modify | `Launchbox.Tests/Launchbox.Tests.csproj` | Add `<Compile Include>` links for 4 new production files |
| Create | `Launchbox.Tests/MockAppWindowAdapter.cs` | Stateful test double for `IAppWindowAdapter` |
| Create | `Launchbox.Tests/MockNativeHotkeyService.cs` | Recording test double for `INativeHotkeyService` |
| Create | `Launchbox.Tests/WindowServiceTests.cs` | 11 unit tests for `WindowService` |

---

## Task 1: Define the two interfaces and add csproj links

**Files:**
- Create: `Services/IAppWindowAdapter.cs`
- Create: `Services/INativeHotkeyService.cs`
- Modify: `Launchbox.Tests/Launchbox.Tests.csproj`

- [ ] **Step 1: Create `IAppWindowAdapter`**

Create `Services/IAppWindowAdapter.cs`:

```csharp
using System;

namespace Launchbox.Services;

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

- [ ] **Step 2: Create `INativeHotkeyService`**

Create `Services/INativeHotkeyService.cs`:

```csharp
using System;

namespace Launchbox.Services;

public interface INativeHotkeyService
{
    bool RegisterHotKey(IntPtr hWnd, int id, uint modifiers, uint virtualKey);
    bool UnregisterHotKey(IntPtr hWnd, int id);
}
```

- [ ] **Step 3: Add `VK_A` to `Helpers/Constants.cs`**

The `UpdateHotkey_FailsAndFallbackFails_IsHotkeyRegisteredFalse` test uses `Constants.VK_A`, which doesn't exist yet. Add it alongside `VK_S`:

```csharp
public const int VK_S = 0x53;
public const int VK_A = 0x41;
```

- [ ] **Step 4: Add Compile links to `Launchbox.Tests/Launchbox.Tests.csproj`**

Add these four lines inside the existing `<ItemGroup>` that contains the other `<Compile Include>` entries (after the `CollapseChevronConverter` line):

```xml
    <Compile Include="..\Services\IAppWindowAdapter.cs" Link="Services\IAppWindowAdapter.cs" />
    <Compile Include="..\Services\INativeHotkeyService.cs" Link="Services\INativeHotkeyService.cs" />
    <Compile Include="..\Services\WinUIAppWindowAdapter.cs" Link="Services\WinUIAppWindowAdapter.cs" />
    <Compile Include="..\Services\Win32HotkeyService.cs" Link="Services\Win32HotkeyService.cs" />
```

Note: `WinUIAppWindowAdapter.cs` and `Win32HotkeyService.cs` don't exist yet — add the links now so later tasks don't require a csproj edit. The build will succeed once those files are created in Task 5.

- [ ] **Step 5: Verify test project builds (expect an error about missing files — that's OK)**

Run:
```bash
dotnet build Launchbox.Tests/Launchbox.Tests.csproj 2>&1 | head -20
```

Expected: build error referencing the two files not yet created (`WinUIAppWindowAdapter.cs`, `Win32HotkeyService.cs`). Interfaces themselves should compile fine. Proceed regardless — those files are created in Task 5.

---

## Task 2: Create test mocks

**Files:**
- Create: `Launchbox.Tests/MockAppWindowAdapter.cs`
- Create: `Launchbox.Tests/MockNativeHotkeyService.cs`

- [ ] **Step 1: Create `MockAppWindowAdapter`**

Create `Launchbox.Tests/MockAppWindowAdapter.cs`:

```csharp
using Launchbox.Services;
using System;
using System.Collections.Generic;

namespace Launchbox.Tests;

public class MockAppWindowAdapter : IAppWindowAdapter
{
    public bool IsVisible { get; set; }
    public Windows.Graphics.SizeInt32 Size { get; private set; } = new Windows.Graphics.SizeInt32(650, 700);
    public Windows.Graphics.PointInt32 Position { get; private set; }
    public Windows.Graphics.RectInt32 WorkArea { get; set; } = new Windows.Graphics.RectInt32(0, 0, 1920, 1040);
    public bool RectOnAnyDisplay { get; set; } = true;

    public int ShowCount { get; private set; }
    public int HideCount { get; private set; }
    public List<Windows.Graphics.SizeInt32> ResizeCalls { get; } = [];
    public List<Windows.Graphics.PointInt32> MoveCalls { get; } = [];
    public List<Windows.Graphics.RectInt32> MoveAndResizeCalls { get; } = [];

    public event EventHandler? PositionOrSizeChanged;

    public void Show() => ShowCount++;
    public void Hide() => HideCount++;

    public void Move(Windows.Graphics.PointInt32 point)
    {
        Position = point;
        MoveCalls.Add(point);
    }

    public void Resize(Windows.Graphics.SizeInt32 size)
    {
        Size = size;
        ResizeCalls.Add(size);
    }

    public void MoveAndResize(Windows.Graphics.RectInt32 rect)
    {
        Position = new Windows.Graphics.PointInt32(rect.X, rect.Y);
        Size = new Windows.Graphics.SizeInt32(rect.Width, rect.Height);
        MoveAndResizeCalls.Add(rect);
    }

    public Windows.Graphics.RectInt32 GetWorkArea() => WorkArea;
    public bool IsRectOnAnyDisplay(Windows.Graphics.RectInt32 rect) => RectOnAnyDisplay;

    public void FirePositionOrSizeChanged() => PositionOrSizeChanged?.Invoke(this, EventArgs.Empty);
}
```

- [ ] **Step 2: Create `MockNativeHotkeyService`**

Create `Launchbox.Tests/MockNativeHotkeyService.cs`:

```csharp
using Launchbox.Services;
using System;
using System.Collections.Generic;

namespace Launchbox.Tests;

public class MockNativeHotkeyService : INativeHotkeyService
{
    public record RegisterCall(IntPtr HWnd, int Id, uint Modifiers, uint VirtualKey);
    public record UnregisterCall(IntPtr HWnd, int Id);

    public Queue<bool> RegisterResults { get; } = new();
    public List<RegisterCall> RegisterCalls { get; } = [];
    public List<UnregisterCall> UnregisterCalls { get; } = [];

    public bool RegisterHotKey(IntPtr hWnd, int id, uint modifiers, uint virtualKey)
    {
        RegisterCalls.Add(new RegisterCall(hWnd, id, modifiers, virtualKey));
        return RegisterResults.Count > 0 ? RegisterResults.Dequeue() : true;
    }

    public bool UnregisterHotKey(IntPtr hWnd, int id)
    {
        UnregisterCalls.Add(new UnregisterCall(hWnd, id));
        return true;
    }
}
```

---

## Task 3: Write failing tests

**Files:**
- Create: `Launchbox.Tests/WindowServiceTests.cs`

These tests will **fail to compile** until `WindowService` gains the `internal` constructor (Task 4). That is the expected TDD red state — proceed to Task 4 immediately after writing this file.

- [ ] **Step 1: Create `WindowServiceTests.cs`**

Create `Launchbox.Tests/WindowServiceTests.cs`:

```csharp
using Launchbox.Helpers;
using Launchbox.Services;
using Xunit;

namespace Launchbox.Tests;

public class WindowServiceTests
{
    // Creates a WindowService wired to test doubles.
    // positionStore: pre-populate with WinX/WinY/WinW/WinH (int) to simulate a saved position.
    private static (WindowService svc, MockAppWindowAdapter adapter, MockNativeHotkeyService hotkey, SettingsService settings)
        CreateSut(MockSettingsStore? positionStore = null)
    {
        var settingsStore = new MockSettingsStore();
        var settings = new SettingsService(
            settingsStore,
            new MockStartupService(),
            new ShortcutFolderManager(settingsStore));
        var positionManager = new WindowPositionManager(positionStore ?? new MockSettingsStore());
        var adapter = new MockAppWindowAdapter();
        var hotkey = new MockNativeHotkeyService();
        var svc = new WindowService(adapter, hotkey, positionManager, settings, new MockDispatcher());
        return (svc, adapter, hotkey, settings);
    }

    // Returns a positionStore pre-loaded with the given window rect.
    private static MockSettingsStore SavedPosition(int x, int y, int w, int h)
    {
        var store = new MockSettingsStore();
        store.SetValue("WinX", x);
        store.SetValue("WinY", y);
        store.SetValue("WinW", w);
        store.SetValue("WinH", h);
        return store;
    }

    // --- ClampToWorkArea ---

    [Fact]
    public void ClampToWorkArea_HeightExceedsWorkArea_ShrinksToFit()
    {
        // Work area height 300; saved window height 800 — ClampToWorkArea must shrink height to 300-40=260.
        var positionStore = SavedPosition(100, 0, Constants.WINDOW_WIDTH, 800);
        var (svc, adapter, _, _) = CreateSut(positionStore);
        adapter.WorkArea = new Windows.Graphics.RectInt32(0, 0, 1920, 300);

        svc.ToggleVisibility();

        var expectedHeight = Math.Max(Constants.MIN_WINDOW_HEIGHT, 300 - Constants.WINDOW_WORKAREA_MARGIN);
        Assert.Equal(new Windows.Graphics.SizeInt32(Constants.WINDOW_WIDTH, expectedHeight), adapter.ResizeCalls[^1]);
    }

    [Fact]
    public void ClampToWorkArea_WindowOffScreenHorizontally_Repositions()
    {
        // Saved position is entirely off the right edge of the 1920-wide work area.
        var positionStore = SavedPosition(2000, 100, Constants.WINDOW_WIDTH, Constants.WINDOW_HEIGHT);
        var (svc, adapter, _, _) = CreateSut(positionStore);

        svc.ToggleVisibility();

        int expectedX = Math.Max(0, (1920 - Constants.WINDOW_WIDTH) / 2);
        Assert.Equal(expectedX, adapter.MoveCalls[^1].X);
    }

    [Fact]
    public void ClampToWorkArea_SmallDisplay_ClampsToMinimumDimensions()
    {
        // Work area 200×150 is smaller than both MIN_WINDOW_WIDTH and MIN_WINDOW_HEIGHT.
        var positionStore = SavedPosition(0, 0, Constants.WINDOW_WIDTH, Constants.WINDOW_HEIGHT);
        var (svc, adapter, _, _) = CreateSut(positionStore);
        adapter.WorkArea = new Windows.Graphics.RectInt32(0, 0, 200, 150);

        svc.ToggleVisibility();

        Assert.Equal(
            new Windows.Graphics.SizeInt32(Constants.MIN_WINDOW_WIDTH, Constants.MIN_WINDOW_HEIGHT),
            adapter.ResizeCalls[^1]);
    }

    // --- OnActivated ---

    [Fact]
    public void OnActivated_Deactivated_HidesWindow()
    {
        var (svc, adapter, _, _) = CreateSut();

        svc.OnActivated(isDeactivated: true);

        Assert.Equal(1, adapter.HideCount);
    }

    [Fact]
    public void OnActivated_Activated_DoesNotHideWindow()
    {
        var (svc, adapter, _, _) = CreateSut();

        svc.OnActivated(isDeactivated: false);

        Assert.Equal(0, adapter.HideCount);
    }

    // --- ToggleVisibility ---

    [Fact]
    public void ToggleVisibility_WhenHidden_ShowsWindow()
    {
        var (svc, adapter, _, _) = CreateSut();

        svc.ToggleVisibility();

        Assert.Equal(1, adapter.ShowCount);
    }

    [Fact]
    public void ToggleVisibility_WhenVisible_HidesWindow()
    {
        var (svc, adapter, _, _) = CreateSut();
        // First toggle: show the window and mark _hasPositioned = true.
        svc.ToggleVisibility();
        adapter.IsVisible = true;

        svc.ToggleVisibility();

        Assert.Equal(1, adapter.HideCount);
    }

    // --- UpdateHotkey ---
    // UpdateHotkey() is private. Tests trigger it by mutating SettingsService properties,
    // which fires PropertyChanged. MockDispatcher runs the enqueued action synchronously.
    // Note: when fallback succeeds it writes back to SettingsService, which fires a recursive
    // UpdateHotkey() via MockDispatcher (synchronous). Tests account for this in call counts.

    [Fact]
    public void UpdateHotkey_Success_UpdatesCurrentKey()
    {
        var (_, _, hotkey, settings) = CreateSut();
        // Change from default MOD_ALT to MOD_CONTROL — triggers UpdateHotkey.
        settings.HotkeyModifiers = Constants.MOD_CONTROL;

        Assert.True(hotkey.RegisterCalls.Count >= 1);
        var firstRegister = hotkey.RegisterCalls[0];
        Assert.Equal((uint)Constants.MOD_CONTROL, firstRegister.Modifiers);
        Assert.Equal((uint)Constants.VK_S, firstRegister.VirtualKey);
    }

    [Fact]
    public void UpdateHotkey_Fails_FallsBackToPreviousKey()
    {
        var (_, _, hotkey, settings) = CreateSut();

        // Step 1: register MOD_CONTROL — succeeds, establishes _currentMod/_currentKey.
        hotkey.RegisterResults.Enqueue(true);
        settings.HotkeyModifiers = Constants.MOD_CONTROL;

        // Step 2: register MOD_SHIFT — fails; fallback to MOD_CONTROL should succeed.
        hotkey.RegisterResults.Enqueue(false); // MOD_SHIFT attempt
        hotkey.RegisterResults.Enqueue(true);  // fallback MOD_CONTROL attempt
        int callsBefore = hotkey.RegisterCalls.Count;
        settings.HotkeyModifiers = Constants.MOD_SHIFT;

        // The fallback call immediately follows the failed attempt.
        // callsBefore+1 = failed MOD_SHIFT attempt index; callsBefore+2 = fallback index.
        Assert.True(hotkey.RegisterCalls.Count >= callsBefore + 2);
        var fallbackCall = hotkey.RegisterCalls[callsBefore + 1];
        Assert.Equal((uint)Constants.MOD_CONTROL, fallbackCall.Modifiers);
        Assert.Equal((uint)Constants.VK_S, fallbackCall.VirtualKey);
    }

    [Fact]
    public void UpdateHotkey_Fails_RevertsSettingsToOldKey()
    {
        var (_, _, hotkey, settings) = CreateSut();

        // Step 1: register MOD_CONTROL — succeeds.
        hotkey.RegisterResults.Enqueue(true);
        settings.HotkeyModifiers = Constants.MOD_CONTROL;

        // Step 2: register MOD_SHIFT — fails; fallback restores MOD_CONTROL.
        hotkey.RegisterResults.Enqueue(false);
        hotkey.RegisterResults.Enqueue(true);
        settings.HotkeyModifiers = Constants.MOD_SHIFT;

        // After fallback write-back + recursive UpdateHotkey settles, settings should reflect MOD_CONTROL.
        Assert.Equal(Constants.MOD_CONTROL, settings.HotkeyModifiers);
        Assert.Equal(Constants.VK_S, settings.HotkeyKey);
    }

    [Fact]
    public void UpdateHotkey_FailsAndFallbackFails_IsHotkeyRegisteredFalse()
    {
        var (_, _, hotkey, settings) = CreateSut();

        // Step 1: register MOD_CONTROL — succeeds (_isHotkeyRegistered = true).
        hotkey.RegisterResults.Enqueue(true);
        settings.HotkeyModifiers = Constants.MOD_CONTROL;

        // Step 2: register MOD_SHIFT — fails; fallback also fails (_isHotkeyRegistered → false).
        hotkey.RegisterResults.Enqueue(false); // MOD_SHIFT attempt
        hotkey.RegisterResults.Enqueue(false); // fallback attempt
        settings.HotkeyModifiers = Constants.MOD_SHIFT;

        // Step 3: trigger a fresh hotkey change that fails.
        // If _isHotkeyRegistered is correctly false, no fallback is attempted → exactly 1 new register call.
        // If _isHotkeyRegistered were still true, 2+ new calls would occur.
        hotkey.RegisterResults.Enqueue(false); // step 3 attempt fails
        int callsBefore = hotkey.RegisterCalls.Count;
        settings.HotkeyKey = Constants.VK_A;

        Assert.Equal(callsBefore + 1, hotkey.RegisterCalls.Count);
    }
}
```

- [ ] **Step 2: Confirm the test file causes a compilation error**

Run:
```bash
dotnet build Launchbox.Tests/Launchbox.Tests.csproj 2>&1 | grep -i "error"
```

Expected: Error — `WindowService` does not contain a constructor matching the internal signature. This is the expected TDD red state.

---

## Task 4: Add internal constructor and refactor `WindowService`

**Files:**
- Modify: `Services/WindowService.cs`

This task makes all tests from Task 3 compile and pass. Replace the full contents of `Services/WindowService.cs` with the refactored version below.

**Key changes vs. current file:**
- `_window` becomes `Window?` (nullable — internal constructor has no window)
- `_filePickerService` becomes `IFilePickerService?` (not provided by internal constructor)
- `AppWindow? _appWindow` removed; replaced by `IAppWindowAdapter? _adapter`
- `INativeHotkeyService? _hotkeyService` field added
- `IsVisible` uses `_adapter?.IsVisible ?? false`
- `UpdateHotkey()` routes through `_hotkeyService`
- `AppWindow_Changed` handler renamed `Adapter_PositionOrSizeChanged`; simpler (adapter pre-filters)
- All `_appWindow.X` calls replaced with `_adapter.X` equivalents
- All `DisplayArea.GetFromWindowId(...)` replaced with `_adapter.GetWorkArea()`
- `DisplayArea.GetFromRect(...)` replaced with `_adapter.IsRectOnAnyDisplay(...)`
- `Dispose()` unsubscribes from `_adapter.PositionOrSizeChanged` and disposes the adapter
- Internal constructor added; sets `_adapter`, `_hotkeyService`, subscribes to settings

- [ ] **Step 1: Replace `Services/WindowService.cs` with refactored version**

```csharp
using Launchbox;
using Launchbox.Helpers;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Launchbox.Services;

public class WindowService : IWindowService, IDisposable
{
    private readonly Window? _window;
    private readonly WindowPositionManager _positionManager;
    private readonly SettingsService _settingsService;
    private readonly IFilePickerService? _filePickerService;
    private readonly IDispatcher _dispatcher;
    private IAppWindowAdapter? _adapter;
    private INativeHotkeyService? _hotkeyService;
    private IntPtr _hWnd;
    private IntPtr _oldWndProc;
    private WndProcDelegate? _wndProcDelegate;
    // True once the window has been shown at a real screen position (not the initial -10000,-10000).
    // Prevents treating the off-screen start position as a valid saved position.
    private bool _hasPositioned;
    private SettingsWindow? _settingsWindow;
    private int _currentMod;
    private int _currentKey;
    private bool _isHotkeyRegistered;
    private DispatcherQueueTimer? _savePositionTimer;
    // Suppresses position persistence during display-time clamping so that a
    // temporarily reduced size on a small display is not saved as the user's intent.
    private bool _suppressSave;

    public bool IsVisible => _adapter?.IsVisible ?? false;

    public event EventHandler<bool>? VisibilityChanged;
    public event EventHandler? Showing;
    public event EventHandler<string>? HotkeyRegistrationFailed;

    public WindowService(Window window, WindowPositionManager positionManager, SettingsService settingsService, IFilePickerService filePickerService, IDispatcher dispatcher)
    {
        ArgumentNullException.ThrowIfNull(window);
        ArgumentNullException.ThrowIfNull(positionManager);
        ArgumentNullException.ThrowIfNull(settingsService);
        ArgumentNullException.ThrowIfNull(filePickerService);
        ArgumentNullException.ThrowIfNull(dispatcher);

        _window = window;
        _positionManager = positionManager;
        _settingsService = settingsService;
        _filePickerService = filePickerService;
        _dispatcher = dispatcher;

        _window.VisibilityChanged += Window_VisibilityChanged;
        _settingsService.PropertyChanged += SettingsService_PropertyChanged;
    }

    internal WindowService(
        IAppWindowAdapter adapter,
        INativeHotkeyService hotkeyService,
        WindowPositionManager positionManager,
        SettingsService settingsService,
        IDispatcher dispatcher)
    {
        ArgumentNullException.ThrowIfNull(adapter);
        ArgumentNullException.ThrowIfNull(hotkeyService);
        ArgumentNullException.ThrowIfNull(positionManager);
        ArgumentNullException.ThrowIfNull(settingsService);
        ArgumentNullException.ThrowIfNull(dispatcher);

        _adapter = adapter;
        _hotkeyService = hotkeyService;
        _positionManager = positionManager;
        _settingsService = settingsService;
        _dispatcher = dispatcher;

        _settingsService.PropertyChanged += SettingsService_PropertyChanged;
    }

    private void Window_VisibilityChanged(object sender, WindowVisibilityChangedEventArgs args)
    {
        VisibilityChanged?.Invoke(this, args.Visible);
    }

    public void Initialize()
    {
        _hWnd = WinRT.Interop.WindowNative.GetWindowHandle(_window!);
        var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(_hWnd);
        var appWindow = AppWindow.GetFromWindowId(windowId);
        _adapter = new WinUIAppWindowAdapter(appWindow, _window!);
        _hotkeyService = new Win32HotkeyService();

        // Window Setup
        _window!.ExtendsContentIntoTitleBar = true;
        appWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Collapsed;
        appWindow.IsShownInSwitchers = false;

        // Start Off-Screen
        _adapter.Resize(new Windows.Graphics.SizeInt32(Constants.WINDOW_WIDTH, Constants.WINDOW_HEIGHT));
        _adapter.Move(new Windows.Graphics.PointInt32(-10000, -10000));
        _adapter.PositionOrSizeChanged += Adapter_PositionOrSizeChanged;

        // Debounce timer for window position persistence — saves after 500ms of no movement
        _savePositionTimer = _window.DispatcherQueue.CreateTimer();
        _savePositionTimer.Interval = TimeSpan.FromMilliseconds(500);
        _savePositionTimer.IsRepeating = false;
        _savePositionTimer.Tick += SavePositionTimer_Tick;

        // Hotkey
        UpdateHotkey();

        // WndProc — delegate stored in _wndProcDelegate field to satisfy GC-root requirement;
        // see NativeMethods.SetWindowLongPtr(WndProcDelegate) remarks.
        _wndProcDelegate = NewWndProc;
        _oldWndProc = NativeMethods.SetWindowLongPtr(_hWnd, NativeMethods.GWLP_WNDPROC, _wndProcDelegate);
        if (_oldWndProc == IntPtr.Zero)
        {
            Trace.WriteLine("Failed to set WndProc hook.");
        }
    }

    private void SettingsService_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SettingsService.HotkeyModifiers) ||
            e.PropertyName == nameof(SettingsService.HotkeyKey))
        {
            // RegisterHotKey is thread-affine and must be called on the thread that created the window.
            _dispatcher.TryEnqueue(() => UpdateHotkey());
        }
    }

    private void UpdateHotkey()
    {
        int mod = _settingsService.HotkeyModifiers;
        int key = _settingsService.HotkeyKey;

        // Unregister existing first
        _hotkeyService?.UnregisterHotKey(_hWnd, Constants.HOTKEY_ID);

        if (!(_hotkeyService?.RegisterHotKey(_hWnd, Constants.HOTKEY_ID, (uint)mod, (uint)key) ?? false))
        {
            var errorMessage = $"Failed to register hotkey: {mod}+{key}";
            Trace.WriteLine(errorMessage);
            HotkeyRegistrationFailed?.Invoke(this, errorMessage);

            if (_isHotkeyRegistered)
            {
                if (_hotkeyService?.RegisterHotKey(_hWnd, Constants.HOTKEY_ID, (uint)_currentMod, (uint)_currentKey) ?? false)
                {
                    Trace.WriteLine($"Restored previous hotkey: {_currentMod}+{_currentKey}");
                    // Write back the restored values so persisted settings stay in sync with Win32 state.
                    // This prevents the failed hotkey from being reloaded on next launch.
                    _settingsService.HotkeyModifiers = _currentMod;
                    _settingsService.HotkeyKey = _currentKey;
                }
                else
                {
                    Trace.WriteLine($"Failed to restore previous hotkey: {_currentMod}+{_currentKey}");
                    _isHotkeyRegistered = false;
                }
            }
        }
        else
        {
            Trace.WriteLine($"Registered hotkey: {mod}+{key}");
            _currentMod = mod;
            _currentKey = key;
            _isHotkeyRegistered = true;
        }
    }

    private void Adapter_PositionOrSizeChanged(object? sender, EventArgs e)
    {
        if (_suppressSave) return;

        if (_hasPositioned)
        {
            // Reset the debounce timer — only persists after 500ms of no movement
            _savePositionTimer?.Stop();
            _savePositionTimer?.Start();
        }
    }

    private IntPtr NewWndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        if (msg == NativeMethods.WM_GETMINMAXINFO)
        {
            var mmi = Marshal.PtrToStructure<NativeMethods.MINMAXINFO>(lParam);
            mmi.ptMinTrackSize.X = Constants.MIN_WINDOW_WIDTH;
            mmi.ptMinTrackSize.Y = Constants.MIN_WINDOW_HEIGHT;
            Marshal.StructureToPtr(mmi, lParam, false);
            return IntPtr.Zero;
        }
        if (msg == NativeMethods.WM_HOTKEY && wParam.ToInt32() == Constants.HOTKEY_ID)
        {
            ToggleVisibility();
            return IntPtr.Zero;
        }
        // Suppress: prevents the default double-click-title-bar maximize behavior.
        // Launchbox has no title bar and maximizing would break the layout.
        if (msg == NativeMethods.WM_NCLBUTTONDBLCLK) return IntPtr.Zero;

        return NativeMethods.CallWindowProc(_oldWndProc, hWnd, msg, wParam, lParam);
    }

    /// <summary>
    /// Toggles the main window's visibility, restoring its previous position or centering it if displayed for the first time.
    /// Also restores the window state if it was minimized (iconic) and brings it to the foreground.
    /// When KeepCentered is enabled, re-centers on every show using the current window size.
    /// </summary>
    public void ToggleVisibility()
    {
        if (_adapter == null) return;

        try
        {
            bool isWindowCurrentlyVisible = _adapter.IsVisible && _hasPositioned;

            if (isWindowCurrentlyVisible)
            {
                _adapter.Hide();
                return;
            }

            // Set unconditionally BEFORE any positioning path — required for hide toggle to work
            bool firstShow = !_hasPositioned;
            _hasPositioned = true;

            if (_settingsService.KeepCentered)
            {
                if (firstShow)
                {
                    // First show: restore saved size (not position), then center at that size
                    RestoreWindowPosition();
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

            ClampToWorkArea();
            // Raise Showing before adapter.Show so subscribers can set state that must be
            // ready before the Activated event fires (Activated is synchronous with SetForegroundWindow
            // and can fire before the async VisibilityChanged event).
            Showing?.Invoke(this, EventArgs.Empty);
            _adapter.Show();

            if (NativeMethods.IsIconic(_hWnd))
            {
                NativeMethods.ShowWindow(_hWnd, NativeMethods.SW_RESTORE);
            }

            NativeMethods.SetForegroundWindow(_hWnd);
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"Failed to toggle window visibility: {PathSecurity.GetSafeExceptionMessage(ex)}");
        }
    }

    public void Hide()
    {
        if (_adapter == null) return;
        try
        {
            _adapter.Hide();
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"Failed to hide window: {PathSecurity.GetSafeExceptionMessage(ex)}");
        }
    }

    public void Exit()
    {
        _window?.Close();
    }

    private void SettingsWindow_Closed(object sender, WindowEventArgs args)
    {
        if (_settingsWindow != null)
        {
            _settingsWindow.Closed -= SettingsWindow_Closed;
            _settingsWindow = null;
        }
    }

    public void OpenSettings()
    {
        if (_settingsWindow != null)
        {
            _settingsWindow.Activate();
            return;
        }

        try
        {
            _settingsWindow = new SettingsWindow(_settingsService, this, _filePickerService!);
            _settingsWindow.Closed += SettingsWindow_Closed;
            _settingsWindow.Activate();
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"Error opening settings: {PathSecurity.GetSafeExceptionMessage(ex)}");
        }
    }

    public void ResetPosition()
    {
        if (_adapter == null) return;

        try
        {
            _hasPositioned = true;
            CenterWindow();
            _adapter.Show();
            NativeMethods.SetForegroundWindow(_hWnd);
            SaveWindowPosition();
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"Failed to reset window position: {PathSecurity.GetSafeExceptionMessage(ex)}");
        }
    }

    private bool _disposed;

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        // Unsubscribe managed events
        _window?.VisibilityChanged -= Window_VisibilityChanged;
        _settingsService.PropertyChanged -= SettingsService_PropertyChanged;

        if (_adapter != null)
        {
            _adapter.PositionOrSizeChanged -= Adapter_PositionOrSizeChanged;
            (_adapter as IDisposable)?.Dispose();
        }

        if (_savePositionTimer != null)
        {
            _savePositionTimer.Tick -= SavePositionTimer_Tick;
            if (_savePositionTimer.IsRunning)
            {
                _savePositionTimer.Stop();
                SaveWindowPosition();
            }
            _savePositionTimer = null;
        }

        if (_settingsWindow != null)
        {
            _settingsWindow.Closed -= SettingsWindow_Closed;
            _settingsWindow.Close();
            _settingsWindow = null;
        }

        // UnregisterHotKey and SetWindowLongPtr are thread-affine Win32 APIs that must
        // run on the window-owning thread. They belong here in Dispose() (called on the
        // UI thread), not in a finalizer which runs on the GC thread.
        try
        {
            _hotkeyService?.UnregisterHotKey(_hWnd, Constants.HOTKEY_ID);

            if (_oldWndProc != IntPtr.Zero)
            {
                NativeMethods.SetWindowLongPtr(_hWnd, NativeMethods.GWLP_WNDPROC, _oldWndProc);
                _oldWndProc = IntPtr.Zero;
            }
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"Error during exit: {PathSecurity.GetSafeExceptionMessage(ex)}");
        }
    }

    private void CenterWindow()
    {
        if (_adapter == null) return;

        try
        {
            var workArea = _adapter.GetWorkArea();
            int height = Math.Min(Constants.WINDOW_HEIGHT, Math.Max(Constants.MIN_WINDOW_HEIGHT, workArea.Height - Constants.WINDOW_WORKAREA_MARGIN));
            _adapter.Resize(new Windows.Graphics.SizeInt32(Constants.WINDOW_WIDTH, height));
            CenterOnCurrentDisplay();
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"Failed to center window: {PathSecurity.GetSafeExceptionMessage(ex)}");
        }
    }

    private void CenterOnCurrentDisplay()
    {
        if (_adapter == null) return;

        try
        {
            var workArea = _adapter.GetWorkArea();
            var currentSize = _adapter.Size;
            // Add display origin so centering works on secondary monitors (non-zero WorkArea.X/Y)
            // Clamp to work-area origin so the window never starts off the top or left edge
            // on a display smaller than the window (the OS min-size hook still prevents resizing below MIN_WINDOW_*).
            var x = Math.Max(workArea.X, workArea.X + (workArea.Width - currentSize.Width) / 2);
            var y = Math.Max(workArea.Y, workArea.Y + (workArea.Height - currentSize.Height) / 2);
            _adapter.Move(new Windows.Graphics.PointInt32(x, y));
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"Failed to center window on display: {PathSecurity.GetSafeExceptionMessage(ex)}");
        }
    }

    /// <summary>
    /// Ensures the window fits within the current display's work area.
    /// Shrinks height and width if needed and repositions if the window is off-screen horizontally
    /// (e.g., after a monitor disconnect). Clamped changes are display-time only and
    /// are not persisted, so the user's preferred size restores on a larger display.
    /// </summary>
    private void ClampToWorkArea()
    {
        if (_adapter == null) return;

        try
        {
            var workArea = _adapter.GetWorkArea();
            var pos = _adapter.Position;
            var size = _adapter.Size;

            int maxHeight = Math.Max(Constants.MIN_WINDOW_HEIGHT, workArea.Height - Constants.WINDOW_WORKAREA_MARGIN);
            bool needsResize = size.Height > maxHeight;
            int clampedHeight = needsResize ? maxHeight : size.Height;

            int maxWidth = Math.Max(Constants.MIN_WINDOW_WIDTH, workArea.Width - Constants.WINDOW_WORKAREA_MARGIN);
            bool needsWidthClamp = size.Width > maxWidth;
            int clampedWidth = needsWidthClamp ? maxWidth : size.Width;
            needsResize = needsResize || needsWidthClamp;

            // Check if the window is entirely off-screen on either axis
            bool offScreenHorizontally = pos.X >= workArea.X + workArea.Width
                                         || pos.X + clampedWidth <= workArea.X;
            bool offScreenVertically = pos.Y >= workArea.Y + workArea.Height
                                       || pos.Y + clampedHeight <= workArea.Y;

            if (!needsResize && !offScreenHorizontally && !offScreenVertically) return;

            // Suppress save so clamped dimensions are not persisted
            _suppressSave = true;
            try
            {
                if (needsResize)
                {
                    _adapter.Resize(new Windows.Graphics.SizeInt32(clampedWidth, clampedHeight));
                }

                int newX = pos.X;
                int newY = pos.Y;
                bool needsMove = false;

                if (offScreenHorizontally)
                {
                    // Clamp so the window never starts left of the work area (can happen when
                    // clampedWidth > workArea.Width on an extremely narrow display).
                    newX = Math.Max(workArea.X, workArea.X + (workArea.Width - clampedWidth) / 2);
                    needsMove = true;
                }

                if (offScreenVertically)
                {
                    // Clamp so the window never starts above the work area (can happen when
                    // clampedHeight > workArea.Height on an extremely short display).
                    newY = Math.Max(workArea.Y, workArea.Y + (workArea.Height - clampedHeight) / 2);
                    needsMove = true;
                }

                if (needsMove)
                {
                    _adapter.Move(new Windows.Graphics.PointInt32(newX, newY));
                }
            }
            finally
            {
                _suppressSave = false;
            }
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"Failed to clamp window to work area: {PathSecurity.GetSafeExceptionMessage(ex)}");
            _suppressSave = false;
        }
    }

    private void SavePositionTimer_Tick(DispatcherQueueTimer sender, object args) => SaveWindowPosition();

    private void SaveWindowPosition()
    {
        if (_adapter == null) return;

        try
        {
            var pos = _adapter.Position;
            var size = _adapter.Size;
            _positionManager.SaveWindowPosition(pos.X, pos.Y, size.Width, size.Height);
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"Failed to save window position: {PathSecurity.GetSafeExceptionMessage(ex)}");
        }
    }

    private bool RestoreWindowPosition()
    {
        if (_adapter == null) return false;

        try
        {
            if (_positionManager.TryGetWindowPosition(out int x, out int y, out int w, out int h))
            {
                var rect = new Windows.Graphics.RectInt32(x, y, w, h);
                // IsRectOnAnyDisplay returns false if the saved position is off all connected displays
                // (e.g., after disconnecting a monitor). We detect this and center the window instead.
                if (_adapter.IsRectOnAnyDisplay(rect))
                {
                    _adapter.MoveAndResize(rect);
                    return true;
                }
            }
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"Failed to restore window position: {PathSecurity.GetSafeExceptionMessage(ex)}");
        }
        return false;
    }

    public void OnActivated(bool isDeactivated)
    {
        if (_adapter != null && isDeactivated)
        {
            // Flush any pending debounced save before hiding
            if (_savePositionTimer != null && _savePositionTimer.IsRunning)
            {
                _savePositionTimer.Stop();
                SaveWindowPosition();
            }
            _adapter.Hide();
        }
    }
}
```

- [ ] **Step 2: Run the tests**

```bash
dotnet test Launchbox.Tests/Launchbox.Tests.csproj --filter "FullyQualifiedName~WindowServiceTests" -v normal 2>&1 | tail -30
```

Expected: All 11 `WindowServiceTests` pass. (Other tests may fail if the csproj links for `WinUIAppWindowAdapter.cs` / `Win32HotkeyService.cs` aren't created yet — that's expected.)

If any `WindowServiceTests` fail, diagnose and fix before proceeding.

---

## Task 5: Create production adapters and wire `Initialize()`

`WindowService.Initialize()` already references `WinUIAppWindowAdapter` and `Win32HotkeyService` — they just need to exist. These files are the final piece to make `dotnet build Launchbox.csproj` pass.

**Files:**
- Create: `Services/WinUIAppWindowAdapter.cs`
- Create: `Services/Win32HotkeyService.cs`

- [ ] **Step 1: Create `WinUIAppWindowAdapter`**

Create `Services/WinUIAppWindowAdapter.cs`:

```csharp
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using System;

namespace Launchbox.Services;

public sealed class WinUIAppWindowAdapter : IAppWindowAdapter, IDisposable
{
    private readonly AppWindow _appWindow;
    private readonly Window _window;

    public WinUIAppWindowAdapter(AppWindow appWindow, Window window)
    {
        _appWindow = appWindow;
        _window = window;
        _appWindow.Changed += AppWindow_Changed;
    }

    public bool IsVisible => _window.Visible;
    public Windows.Graphics.SizeInt32 Size => _appWindow.Size;
    public Windows.Graphics.PointInt32 Position => _appWindow.Position;

    public event EventHandler? PositionOrSizeChanged;

    public void Show() => _appWindow.Show();
    public void Hide() => _appWindow.Hide();
    public void Move(Windows.Graphics.PointInt32 point) => _appWindow.Move(point);
    public void Resize(Windows.Graphics.SizeInt32 size) => _appWindow.Resize(size);
    public void MoveAndResize(Windows.Graphics.RectInt32 rect) => _appWindow.MoveAndResize(rect);

    public Windows.Graphics.RectInt32 GetWorkArea()
        => DisplayArea.GetFromWindowId(_appWindow.Id, DisplayAreaFallback.Primary).WorkArea;

    public bool IsRectOnAnyDisplay(Windows.Graphics.RectInt32 rect)
        => DisplayArea.GetFromRect(rect, DisplayAreaFallback.None) != null;

    private void AppWindow_Changed(AppWindow sender, AppWindowChangedEventArgs args)
    {
        if (args.DidPositionChange || args.DidSizeChange)
            PositionOrSizeChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose()
    {
        _appWindow.Changed -= AppWindow_Changed;
    }
}
```

- [ ] **Step 2: Create `Win32HotkeyService`**

Create `Services/Win32HotkeyService.cs`:

```csharp
using System;

namespace Launchbox.Services;

public sealed class Win32HotkeyService : INativeHotkeyService
{
    public bool RegisterHotKey(IntPtr hWnd, int id, uint modifiers, uint virtualKey)
        => NativeMethods.RegisterHotKey(hWnd, id, modifiers, virtualKey);

    public bool UnregisterHotKey(IntPtr hWnd, int id)
        => NativeMethods.UnregisterHotKey(hWnd, id);
}
```

- [ ] **Step 3: Build the app**

```bash
dotnet build Launchbox.csproj -p:Platform=x64 2>&1 | tail -20
```

Expected: Build succeeded, 0 errors.

- [ ] **Step 4: Run the full test suite**

```bash
dotnet test Launchbox.Tests/Launchbox.Tests.csproj 2>&1 | tail -20
```

Expected: All tests pass (including the 11 new `WindowServiceTests`).

---

## Task 6: Format, multi-model review, and commit

- [ ] **Step 1: Run formatter**

```bash
dotnet format Launchbox.sln 2>&1 | tail -10
```

Expected: No output (already formatted) or minor whitespace fixes applied.

- [ ] **Step 2: Final build + test verification**

```bash
dotnet build Launchbox.csproj -p:Platform=x64 && dotnet test Launchbox.Tests/Launchbox.Tests.csproj 2>&1 | tail -20
```

Expected: Build succeeded, all tests pass.

- [ ] **Step 3: Multi-model review**

Per CLAUDE.md policy, this change touches 7+ files, introduces new public interfaces and classes, and modifies a production service — multi-model review is required. Dispatch both `gemini-reviewer` and `gpt-architect` subagents before committing.

- [ ] **Step 4: Commit**

```bash
git add Helpers/Constants.cs Services/IAppWindowAdapter.cs Services/INativeHotkeyService.cs Services/WinUIAppWindowAdapter.cs Services/Win32HotkeyService.cs Services/WindowService.cs Launchbox.Tests/Launchbox.Tests.csproj Launchbox.Tests/MockAppWindowAdapter.cs Launchbox.Tests/MockNativeHotkeyService.cs Launchbox.Tests/WindowServiceTests.cs
git commit -m "$(cat <<'EOF'
feat: add testability seam to WindowService (#337)

Introduce IAppWindowAdapter and INativeHotkeyService interfaces so
WindowService's clamping, visibility, and hotkey logic can be unit-tested
without a WinUI runtime. Production adapters delegate to AppWindow and
NativeMethods unchanged. Adds 11 unit tests covering ClampToWorkArea,
ToggleVisibility, OnActivated, and UpdateHotkey scenarios.

Co-Authored-By: Claude Sonnet 4.6 <noreply@anthropic.com>
EOF
)"
```
