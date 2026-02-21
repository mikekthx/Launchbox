using Launchbox.Services;
using System;
using System.IO;
using System.Runtime.InteropServices;
using Xunit;

namespace Launchbox.Tests;

public class IconServiceEnvVarsTests
{
    private readonly MockFileSystem _mockFileSystem;
    private readonly IconService _iconService;

    public IconServiceEnvVarsTests()
    {
        _mockFileSystem = new MockFileSystem();
        _iconService = new IconService(_mockFileSystem);
    }

    [Fact]
    public void ResolveIconPath_ExpandsEnvironmentVariables()
    {
        // 1. Setup Environment
        string envVar = "TEST_LAUNCHBOX_ICONS";

        // Use a path format that works for MockFileSystem across platforms
        // On Windows it will be C:\ExpandedIcons, on Linux C:/ExpandedIcons
        string envVal = Path.Combine("C:", "ExpandedIcons");

        Environment.SetEnvironmentVariable(envVar, envVal);

        try
        {
            // 2. Determine syntax
            bool isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
            // If running on Linux, .NET uses $VAR syntax for Environment.ExpandEnvironmentVariables
            string envRef = isWindows ? $"%{envVar}%" : $"${envVar}";

            // 3. Setup File System
            string urlFile = Path.Combine("C:", "Shortcuts", "App.url");

            // The path stored in the INI file, using the environment variable
            // Note: Path.Combine might mess up if envRef starts with $? No.
            string iconRelPath = Path.Combine(envRef, "App.ico");

            // The expected path after expansion
            string expectedIconPath = Path.Combine(envVal, "App.ico");

            _mockFileSystem.AddFile(urlFile);
            _mockFileSystem.AddFile(expectedIconPath); // Add the resolved path to FS, NOT the %VAR% path

            // Set the INI value with the variable
            _mockFileSystem.SetIniValue(urlFile, "InternetShortcut", "IconFile", iconRelPath);

            // 4. Act
            string result = _iconService.ResolveIconPath(urlFile);

            // 5. Assert
            // Before the fix, this will return urlFile because FileExists(iconRelPath) fails.
            // After the fix, it should match expectedIconPath.
            Assert.Equal(expectedIconPath, result);
        }
        finally
        {
            Environment.SetEnvironmentVariable(envVar, null);
        }
    }
}
