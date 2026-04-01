using Launchbox.Models;
using Launchbox.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Xunit;

namespace Launchbox.Tests;

public class ShortcutFolderOptimizationTests
{
    [Fact]
    public void ShortcutFolder_ExpandedPath_IsNotSerialized()
    {
        var folder = new ShortcutFolder
        {
            Path = "%USERPROFILE%\\Shortcuts",
            Label = "My Shortcuts",
            Order = 1,
            ExpandedPath = "C:\\Users\\Jules\\Shortcuts"
        };

        var json = JsonSerializer.Serialize(folder);

        Assert.DoesNotContain("ExpandedPath", json);
        Assert.Contains("\"Path\":\"%USERPROFILE%\\\\Shortcuts\"", json);
    }

    [Fact]
    public void ShortcutFolder_ExpandedPath_FallsBackToExpansion_WhenNotPrePopulated()
    {
        // Simulate a folder constructed without going through ValidateAndNormalize
        // (e.g., deserialized directly from the settings store).
        var path = "%TEMP%\\LaunchboxFallbackTest";
        var folder = new ShortcutFolder { Path = path, Label = "Test", Order = 0 };

        Assert.Equal(Environment.ExpandEnvironmentVariables(path), folder.ExpandedPath);
    }

    [Fact]
    public void ShortcutFolder_Equals_IgnoresCachedExpandedPath()
    {
        var a = new ShortcutFolder { Path = "X", Label = "Y", Order = 0 };
        var b = a with { ExpandedPath = "expanded_X" };

        Assert.Equal(a, b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void ShortcutFolderManager_LoadFromStore_PopulatesExpandedPath()
    {
        // Simulate persisted state: JSON has no ExpandedPath (it's [JsonIgnore]).
        // A new manager loading from the store must still populate ExpandedPath via ValidateAndNormalize.
        var path = "%TEMP%\\LaunchboxLoadTest";
        var store = new MockSettingsStore();
        var seed = new System.Collections.Generic.List<ShortcutFolder>
        {
            new() { Path = path, Label = "Loaded Folder", Order = 0 }
        };
        store.SetValue("ShortcutFolders", JsonSerializer.Serialize(seed));

        var manager = new ShortcutFolderManager(store);
        var folder = manager.GetFolders().FirstOrDefault();

        Assert.NotNull(folder);
        Assert.Equal(Environment.ExpandEnvironmentVariables(path), folder.ExpandedPath);
    }

    [Fact]
    public void ShortcutFolderManager_PopulatesExpandedPath()
    {
        var store = new MockSettingsStore();
        // Path with environment variable
        var path = "%TEMP%\\LaunchboxTest";
        var expectedExpanded = Environment.ExpandEnvironmentVariables(path);

        var manager = new ShortcutFolderManager(store);
        manager.AddFolder(path, "Temp Folder");

        var folders = manager.GetFolders();
        var folder = folders.FirstOrDefault(f => f.Label == "Temp Folder");

        Assert.NotNull(folder);
        Assert.Equal(path, folder.Path);
        Assert.Equal(expectedExpanded, folder.ExpandedPath);
    }
}
