using Xunit;
using Launchbox.Services;
using System.Collections.Generic;

namespace Launchbox.Tests;

public class MockSettingsStore : ISettingsStore
{
    private readonly Dictionary<string, object> _store = new();

    public bool ShouldThrow { get; set; }

    public bool TryGetValue(string key, out object? value)
    {
        if (ShouldThrow)
        {
            throw new Exception("Settings store failure");
        }
        return _store.TryGetValue(key, out value);
    }

    public void SetValue(string key, object? value)
    {
        if (ShouldThrow)
        {
            throw new Exception("Settings store failure");
        }

        if (value != null)
        {
            _store[key] = value;
        }
        else
        {
            _store.Remove(key);
        }
    }
}

public class WindowPositionManagerTests
{
    [Fact]
    public void TryGetWindowPosition_ReturnsTrue_WhenAllSettingsAreValid()
    {
        var settings = new MockSettingsStore();
        settings.SetValue("WinX", 100);
        settings.SetValue("WinY", 200);
        settings.SetValue("WinW", 800);
        settings.SetValue("WinH", 600);

        var manager = new WindowPositionManager(settings);
        var result = manager.TryGetWindowPosition(out int x, out int y, out int w, out int h);

        Assert.True(result);
        Assert.Equal(100, x);
        Assert.Equal(200, y);
        Assert.Equal(800, w);
        Assert.Equal(600, h);
    }

    [Fact]
    public void TryGetWindowPosition_ReturnsFalse_WhenSettingsAreMissing()
    {
        var settings = new MockSettingsStore();
        settings.SetValue("WinX", 100);
        // WinY missing
        settings.SetValue("WinW", 800);
        settings.SetValue("WinH", 600);

        var manager = new WindowPositionManager(settings);
        var result = manager.TryGetWindowPosition(out _, out _, out _, out _);

        Assert.False(result);
    }

    [Fact]
    public void TryGetWindowPosition_ReturnsFalse_WhenSettingsAreInvalidTypes()
    {
        var settings = new MockSettingsStore();
        settings.SetValue("WinX", "invalid"); // String instead of int
        settings.SetValue("WinY", 200);
        settings.SetValue("WinW", 800);
        settings.SetValue("WinH", 600);

        var manager = new WindowPositionManager(settings);
        var result = manager.TryGetWindowPosition(out _, out _, out _, out _);

        Assert.False(result);
    }

    [Fact]
    public void SaveWindowPosition_SavesValuesToStore()
    {
        var settings = new MockSettingsStore();
        var manager = new WindowPositionManager(settings);

        manager.SaveWindowPosition(10, 20, 300, 400);

        Assert.True(settings.TryGetValue("WinX", out var x));
        Assert.Equal(10, x);
        Assert.True(settings.TryGetValue("WinY", out var y));
        Assert.Equal(20, y);
        Assert.True(settings.TryGetValue("WinW", out var w));
        Assert.Equal(300, w);
        Assert.True(settings.TryGetValue("WinH", out var h));
        Assert.Equal(400, h);
    }

    [Fact]
    public void SaveWindowPosition_PropagatesException_WhenStoreFails()
    {
        var settings = new MockSettingsStore { ShouldThrow = true };
        var manager = new WindowPositionManager(settings);

        var exception = Assert.Throws<Exception>(() => manager.SaveWindowPosition(10, 20, 300, 400));
        Assert.Equal("Settings store failure", exception.Message);
    }

    [Fact]
    public void TryGetWindowPosition_PropagatesException_WhenStoreFails()
    {
        var settings = new MockSettingsStore { ShouldThrow = true };
        var manager = new WindowPositionManager(settings);

        var exception = Assert.Throws<Exception>(() => manager.TryGetWindowPosition(out _, out _, out _, out _));
        Assert.Equal("Settings store failure", exception.Message);
    }
}
