using Launchbox.Helpers;
using Launchbox.Services;
using Xunit;

namespace Launchbox.Tests;

public class WindowServiceTests
{
    private const int VK_A = 0x41;
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

        int expectedX = Math.Max(adapter.WorkArea.X, adapter.WorkArea.X + (adapter.WorkArea.Width - Constants.WINDOW_WIDTH) / 2);
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
        settings.HotkeyKey = VK_A;

        Assert.Equal(callsBefore + 1, hotkey.RegisterCalls.Count);
    }

    // --- Hide ---

    [Fact]
    public void Hide_HidesAdapter()
    {
        var (svc, adapter, _, _) = CreateSut();
        svc.Hide();
        Assert.Equal(1, adapter.HideCount);
    }

    // --- Dispose ---

    [Fact]
    public void Dispose_UnregistersHotkey()
    {
        var (svc, _, hotkey, _) = CreateSut();
        svc.Dispose();
        Assert.True(hotkey.UnregisterCalls.Count >= 1);
    }

    [Fact]
    public void Dispose_Idempotent()
    {
        var (svc, _, hotkey, _) = CreateSut();
        svc.Dispose();
        int countAfterFirst = hotkey.UnregisterCalls.Count;
        svc.Dispose();
        Assert.Equal(countAfterFirst, hotkey.UnregisterCalls.Count);
    }

    // --- ToggleVisibility (KeepCentered) ---

    [Fact]
    public void ToggleVisibility_KeepCentered_FirstShow_CentersWindow()
    {
        var (svc, adapter, _, settings) = CreateSut();
        settings.KeepCentered = true;

        svc.ToggleVisibility();

        Assert.True(adapter.MoveCalls.Count >= 1);
    }

    [Fact]
    public void ToggleVisibility_KeepCentered_SubsequentShow_CentersAgain()
    {
        var (svc, adapter, _, settings) = CreateSut();
        settings.KeepCentered = true;
        svc.ToggleVisibility();
        adapter.IsVisible = false;
        int movesBefore = adapter.MoveCalls.Count;

        svc.ToggleVisibility();

        Assert.True(adapter.MoveCalls.Count > movesBefore);
    }

    // --- RestoreWindowPosition (off-screen) ---

    [Fact]
    public void ToggleVisibility_SavedPositionOffScreen_CentersInstead()
    {
        // Saved position exists but IsRectOnAnyDisplay returns false — RestoreWindowPosition
        // should fall through and CenterWindow() centers instead.
        var positionStore = SavedPosition(5000, 5000, Constants.WINDOW_WIDTH, Constants.WINDOW_HEIGHT);
        var (svc, adapter, _, _) = CreateSut(positionStore);
        adapter.RectOnAnyDisplay = false;

        svc.ToggleVisibility();

        // MoveAndResize is NOT called (restore skipped); Move IS called (centered).
        Assert.Empty(adapter.MoveAndResizeCalls);
        Assert.True(adapter.MoveCalls.Count >= 1);
    }
}
