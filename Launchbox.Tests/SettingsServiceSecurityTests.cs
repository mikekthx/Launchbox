using Launchbox.Helpers;
using Launchbox.Services;
using System.Diagnostics;
using System.IO;
using Xunit;

namespace Launchbox.Tests;

public class SettingsServiceSecurityTests
{
    [Fact]
    public void ShortcutsPath_Setter_RedactsUnsafePath_InTrace()
    {
        using var sw = new StringWriter();
        using var listener = new TextWriterTraceListener(sw);
        Trace.Listeners.Add(listener);

        try
        {
            var settingsStore = new MockSettingsStore();
            var startupService = new MockStartupService();
            var service = new SettingsService(settingsStore, startupService);

            string unsafePath = @"\\attacker\share\SecretProject";

            // Act
            service.ShortcutsPath = unsafePath;

            Trace.Flush();
            string log = sw.ToString();

            // Assert
            string expectedMessage = $"Blocked setting unsafe ShortcutsPath: {PathSecurity.RedactPath(unsafePath)}";
            Assert.Contains(expectedMessage, log);
        }
        finally
        {
            Trace.Listeners.Remove(listener);
        }
    }

    [Fact]
    public void ShortcutsPath_Setter_RejectsUnsafePath()
    {
        var settingsStore = new MockSettingsStore();
        var startupService = new MockStartupService();
        var service = new SettingsService(settingsStore, startupService);

        string unsafePath = @"\\attacker\share\Shortcuts";

        // Act
        service.ShortcutsPath = unsafePath;

        // Assert
        // Should remain default or previous value, NOT the unsafe path
        Assert.NotEqual(unsafePath, service.ShortcutsPath);
    }

    [Fact]
    public void ShortcutsPath_Getter_SanitizesUnsafePathInStore()
    {
        var settingsStore = new MockSettingsStore();
        var startupService = new MockStartupService();
        var service = new SettingsService(settingsStore, startupService);

        string unsafePath = @"\\attacker\share\Shortcuts";

        // Manually inject unsafe path into store
        settingsStore.SetValue("ShortcutsPath", unsafePath);

        // Act
        string currentPath = service.ShortcutsPath;

        // Assert
        // Should ignore the stored unsafe path and return default
        Assert.NotEqual(unsafePath, currentPath);
    }
}
