using Launchbox.Services;
using System;
using System.IO;
using Xunit;

namespace Launchbox.Tests;

public class IconServiceEnvTests : IDisposable
{
    private readonly MockFileSystem _mockFileSystem;
    private readonly IconService _iconService;
    private const string TEST_ENV_VAR = "LAUNCHBOX_TEST_ENV";
    private readonly string? _originalEnvValue;

    public IconServiceEnvTests()
    {
        _mockFileSystem = new MockFileSystem();
        _iconService = new IconService(_mockFileSystem);
        _originalEnvValue = Environment.GetEnvironmentVariable(TEST_ENV_VAR);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable(TEST_ENV_VAR, _originalEnvValue);
    }

    [Fact]
    public void ResolveIconPath_ExpandsEnvironmentVariables()
    {
        // Arrange
        // We use a safe temporary path for the environment variable
        string testDir = Path.Combine(Path.GetTempPath(), "LaunchboxTest");
        Environment.SetEnvironmentVariable(TEST_ENV_VAR, testDir);

        string urlPath = Path.Combine(testDir, "Test.url");
        string iconName = "icon.ico";
        string iconPathExpanded = Path.Combine(testDir, iconName);

        // Construct the path using the environment variable syntax
        // On Windows: %VAR%\file
        // On Unix: $VAR/file or %VAR%/file (if supported by .NET ExpandEnvironmentVariables)
        // Since the codebase targets Windows (UseWinUI), we stick to %VAR% syntax which is standard for INI files there.
        string separator = Path.DirectorySeparatorChar.ToString();
        string iconPathEnv = $"%{TEST_ENV_VAR}%{separator}{iconName}";

        // Mock the file system with the EXPANDED path because MockFileSystem doesn't expand
        _mockFileSystem.AddFile(urlPath);
        _mockFileSystem.AddFile(iconPathExpanded);

        // Set the INI value with the ENVIRONMENT VARIABLE path
        // This simulates reading from an INI file that uses %SystemRoot% etc.
        _mockFileSystem.SetIniValue(urlPath, "InternetShortcut", "IconFile", iconPathEnv);

        // Act
        string result = _iconService.ResolveIconPath(urlPath);

        // Assert
        // Without the fix, ResolveIconPath calls FileExists with the unexpanded path.
        // MockFileSystem.FileExists checks for exact match.
        // Since we only added the expanded path, FileExists returns false.
        // So ResolveIconPath returns urlPath (fallback).

        // With the fix, ResolveIconPath expands the variable to iconPathExpanded.
        // FileExists(iconPathExpanded) returns true.
        // So ResolveIconPath returns iconPathExpanded.
        Assert.Equal(iconPathExpanded, result);
    }
}
