using Launchbox.Services;
using System;
using System.IO;
using Xunit;
using Xunit.Abstractions;

namespace Launchbox.Tests;

public class IconServiceOptimizationTests
{
    private readonly ITestOutputHelper _output;

    public IconServiceOptimizationTests(ITestOutputHelper output)
    {
        _output = output;
    }

    class ProfilingFileSystem : IFileSystem
    {
        private readonly MockFileSystem _inner;
        public int GetLastWriteTimeCount { get; private set; }

        public ProfilingFileSystem(MockFileSystem inner)
        {
            _inner = inner;
        }

        public void CreateDirectory(string path) => _inner.CreateDirectory(path);
        public bool DirectoryExists(string path) => _inner.DirectoryExists(path);
        public bool FileExists(string path) => _inner.FileExists(path);
        public string[] GetFiles(string path) => _inner.GetFiles(path);
        public string GetIniValue(string path, string section, string key) => _inner.GetIniValue(path, section, key);
        public byte[] ReadAllBytes(string path) => _inner.ReadAllBytes(path);
        public Stream OpenRead(string path) => _inner.OpenRead(path);
        public long GetFileSize(string path) => _inner.GetFileSize(path);

        public DateTime GetLastWriteTime(string path)
        {
            GetLastWriteTimeCount++;
            return _inner.GetLastWriteTime(path);
        }
    }

    [Fact]
    public void ExtractIconBytes_RedundantTimestampChecks_Optimized()
    {
        // 1. Setup
        var mockFs = new MockFileSystem();
        string appPath = @"C:\Apps\MyApp.exe";
        string iconsDir = @"C:\Apps\.icons";
        string pngPath = @"C:\Apps\.icons\MyApp.png";

        mockFs.AddFile(appPath, size: 1024, lastWriteTime: DateTime.Now);

        // Add custom icon to avoid system extraction (which might fail in test env or be slow)
        // and to verify that we cache checks for these files too.
        mockFs.AddDirectory(iconsDir);
        mockFs.AddFile(pngPath, content: new byte[] { 1, 2, 3 });

        var profilingFs = new ProfilingFileSystem(mockFs);
        var iconService = new IconService(profilingFs);

        // 2. Execute
        int iterations = 100;
        for (int i = 0; i < iterations; i++)
        {
            iconService.ExtractIconBytes(appPath);
        }

        // 3. Measure
        _output.WriteLine($"GetLastWriteTime calls: {profilingFs.GetLastWriteTimeCount}");

        // With optimization, it should cache the timestamp.
        // It checks appPath, and then checks pngPath/icoPath if directory exists.
        // Initial call:
        // 1. GetLastWriteTime(appPath) -> 1
        // 2. Directory listing -> finds pngPath
        // 3. GetLastWriteTime(pngPath) -> 1
        // Total initial: 2 calls.
        // Subsequent calls: Cached.

        Assert.True(profilingFs.GetLastWriteTimeCount <= 5,
            $"Expected <= 5 calls, got {profilingFs.GetLastWriteTimeCount}");
    }
}
