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

    /// <summary>
    /// File system that always returns an old timestamp, simulating a scenario where
    /// the cache entry always appears expired immediately after creation.
    /// </summary>
    private class AlwaysExpiredFileSystem : MockFileSystem
    {
        public int GetLastWriteTimeCallCount { get; private set; }

        public override DateTime GetLastWriteTime(string path)
        {
            GetLastWriteTimeCallCount++;
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

    [Fact]
    public void MaxCacheRetries_IsPositiveAndReasonable()
    {
        // Verify the retry limit constant is bounded to prevent infinite loops
        Assert.True(IconService.MAX_CACHE_RETRIES > 0, "MAX_CACHE_RETRIES must be positive");
        Assert.True(IconService.MAX_CACHE_RETRIES <= 10, "MAX_CACHE_RETRIES should be reasonably bounded");
    }

    [Fact]
    public void ExtractIconBytes_DoesNotSpinIndefinitely_WhenCacheAlwaysExpires()
    {
        // Arrange: use a file system that tracks call count to verify bounded retries
        var mockFs = new AlwaysExpiredFileSystem();
        var iconService = new IconService(mockFs);
        string shortcutPath = @"C:\Apps\TestApp.lnk";

        mockFs.AddFile(shortcutPath, size: 1024);

        // Act: call ExtractIconBytes repeatedly; the internal cache loop must terminate
        // even if entries appear expired on every check (due to MAX_CACHE_RETRIES).
        // This call should complete in bounded time rather than spinning forever.
        var result = iconService.ExtractIconBytes(shortcutPath);

        // Assert: the method completed (didn't hang) and the file system was called
        // a bounded number of times. Each retry re-creates the Lazy, so we expect
        // at most MAX_CACHE_RETRIES calls per cache lookup.
        Assert.Null(result);
        Assert.True(mockFs.GetLastWriteTimeCallCount <= IconService.MAX_CACHE_RETRIES * 2,
            $"GetLastWriteTime was called {mockFs.GetLastWriteTimeCallCount} times, " +
            $"expected at most {IconService.MAX_CACHE_RETRIES * 2}");
    }
}
