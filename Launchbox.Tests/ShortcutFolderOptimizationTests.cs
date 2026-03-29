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
