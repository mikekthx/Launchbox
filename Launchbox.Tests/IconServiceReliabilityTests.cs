using Launchbox.Services;
using System;
using System.IO;
using Xunit;

namespace Launchbox.Tests;

public class IconServiceReliabilityTests
{
    private class FaultyFileSystem : MockFileSystem
    {
        public bool FailNextGetLastWriteTime { get; set; }
        public string? FailPath { get; set; }

        public override DateTime GetLastWriteTime(string path)
        {
            if (FailNextGetLastWriteTime &&
                FailPath != null &&
                path.Equals(FailPath, StringComparison.OrdinalIgnoreCase))
            {
                FailNextGetLastWriteTime = false; // Fail only once
                throw new UnauthorizedAccessException("Simulated access denied");
            }
            return base.GetLastWriteTime(path);
        }
    }

    [Fact]
    public void ExtractIconBytes_RecoversFromTransientFailure()
    {
        // Arrange
        var mockFs = new FaultyFileSystem();
        var iconService = new IconService(mockFs);
        string shortcutPath = @"C:\Apps\TestApp.lnk";

        mockFs.AddFile(shortcutPath, size: 1024);
        mockFs.FailPath = shortcutPath;
        mockFs.FailNextGetLastWriteTime = true;

        // Act & Assert 1: First call fails
        // Currently, GetCachedLastWriteTime throws, and ExtractIconBytes propagates it.
        // We catch it to proceed to the second part of the test.
        try
        {
            iconService.ExtractIconBytes(shortcutPath);
        }
        catch (UnauthorizedAccessException)
        {
            // Expected
        }

        // Act & Assert 2: Second call should succeed
        // If the exception was cached in Lazy<T>, this call will throw the SAME exception again.
        // If our fix works, it will re-evaluate GetLastWriteTime, which succeeds (FailNextGetLastWriteTime is false).
        // It returns null because there's no real icon, but it shouldn't throw.

        var result = iconService.ExtractIconBytes(shortcutPath);

        Assert.Null(result); // Should be null (no icon), not throw exception
    }
}
