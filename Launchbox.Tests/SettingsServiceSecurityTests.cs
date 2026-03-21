using Launchbox.Helpers;
using Launchbox.Services;
using System.Diagnostics;
using System.IO;
using Xunit;

namespace Launchbox.Tests;

[Trait("Category", "Security")]
public class SettingsServiceSecurityTests
{
    [Fact]
    public void AddShortcutFolder_DoesNotAddUnsafePath()
    {
        using var sw = new StringWriter();
        using var listener = new TextWriterTraceListener(sw);
        Trace.Listeners.Add(listener);

        try
        {
            var settingsStore = new MockSettingsStore();
            var startupService = new MockStartupService();
            var service = new SettingsService(settingsStore, startupService, new ShortcutFolderManager(settingsStore));
            var initialCount = service.GetShortcutFolders().Count;

            string unsafePath = @"\\attacker\share\SecretProject";

            // Act — AddShortcutFolder should reject unsafe paths
            bool added = service.AddShortcutFolder(unsafePath);

            Trace.Flush();

            // The unsafe folder must not have been added
            Assert.False(added);
            Assert.Equal(initialCount, service.GetShortcutFolders().Count);
            Assert.DoesNotContain(service.GetShortcutFolders(), f => f.Path == unsafePath);
        }
        finally
        {
            Trace.Listeners.Remove(listener);
        }
    }

    [Fact]
    public void AddShortcutFolder_RejectsUnsafePath()
    {
        var settingsStore = new MockSettingsStore();
        var startupService = new MockStartupService();
        var service = new SettingsService(settingsStore, startupService, new ShortcutFolderManager(settingsStore));

        string unsafePath = @"\\attacker\share\Shortcuts";

        // Act
        bool added = service.AddShortcutFolder(unsafePath);

        // Assert — unsafe path must be rejected; ShortcutsPath returns the safe default
        Assert.False(added);
        Assert.DoesNotContain(service.GetShortcutFolders(), f => f.Path == unsafePath);
        Assert.NotEqual(unsafePath, service.ShortcutsPath);
    }

    [Fact]
    public void ShortcutsPath_Getter_SanitizesUnsafePathInFolderManager()
    {
        var settingsStore = new MockSettingsStore();
        var startupService = new MockStartupService();

        string unsafePath = @"\\attacker\share\Shortcuts";

        // Inject unsafe path into store before constructing manager (simulating tampered settings)
        settingsStore.SetValue("ShortcutFolders", $"[{{\"Order\":0,\"Path\":\"{unsafePath.Replace("\\", "\\\\")}\",\"Label\":\"Test\"}}]");

        // Construct after injection so the manager loads the tampered JSON
        var service = new SettingsService(settingsStore, startupService, new ShortcutFolderManager(settingsStore));

        // Act
        string currentPath = service.ShortcutsPath;

        // Assert: should ignore the stored unsafe path and return default
        Assert.NotEqual(unsafePath, currentPath);
    }
}
