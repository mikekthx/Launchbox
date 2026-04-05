using Launchbox.Services;
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Xunit;

namespace Launchbox.Tests;

public class FileSystemTests : IDisposable
{
    private readonly FileSystem _fileSystem;
    private readonly string _tempDirectory;

    public FileSystemTests()
    {
        _fileSystem = new FileSystem();
        _tempDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(_tempDirectory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            try
            {
                Directory.Delete(_tempDirectory, true);
            }
            catch (IOException)
            {
                // Ignore transient file lock errors during teardown
            }
        }
    }

    [Fact]
    public void FileSystem_CreateDirectory_CreatesDirectory()
    {
        string path = Path.Combine(_tempDirectory, "NewDir");
        _fileSystem.CreateDirectory(path);

        Assert.True(Directory.Exists(path));
    }

    [Fact]
    public void FileSystem_DirectoryExists_ReturnsTrueForExisting()
    {
        Assert.True(_fileSystem.DirectoryExists(_tempDirectory));
    }

    [Fact]
    public void FileSystem_DirectoryExists_ReturnsFalseForNonExisting()
    {
        string path = Path.Combine(_tempDirectory, "MissingDir");
        Assert.False(_fileSystem.DirectoryExists(path));
    }

    [Fact]
    public void FileSystem_FileExists_ReturnsTrueForExisting()
    {
        string path = Path.Combine(_tempDirectory, "file.txt");
        File.WriteAllText(path, "test");
        Assert.True(_fileSystem.FileExists(path));
    }

    [Fact]
    public void FileSystem_FileExists_ReturnsFalseForNonExisting()
    {
        string path = Path.Combine(_tempDirectory, "missing.txt");
        Assert.False(_fileSystem.FileExists(path));
    }

    [Fact]
    public void FileSystem_EnumerateFiles_ReturnsFiles()
    {
        string file1 = Path.Combine(_tempDirectory, "file1.txt");
        string file2 = Path.Combine(_tempDirectory, "file2.txt");
        File.WriteAllText(file1, "test");
        File.WriteAllText(file2, "test");

        var files = _fileSystem.EnumerateFiles(_tempDirectory).ToList();

        Assert.Contains(file1, files);
        Assert.Contains(file2, files);
        Assert.Equal(2, files.Count);
    }

    [Fact]
    public void FileSystem_GetIniValue_ReturnsValue_SmallBuffer()
    {
        if (!OperatingSystem.IsWindows()) return;

        string path = Path.Combine(_tempDirectory, "test.ini");
        File.WriteAllText(path, "[Section]\r\nKey=Value\r\n");

        string result = _fileSystem.GetIniValue(path, "Section", "Key");
        Assert.Equal("Value", result);
    }

    [Fact]
    public void FileSystem_GetIniValue_ReturnsValue_LargeBuffer()
    {
        if (!OperatingSystem.IsWindows()) return;

        string path = Path.Combine(_tempDirectory, "large.ini");
        string largeValue = new string('A', 1000); // Exceeds 512, falls back to ArrayPool
        File.WriteAllText(path, $"[Section]\r\nKey={largeValue}\r\n");

        string result = _fileSystem.GetIniValue(path, "Section", "Key");
        Assert.Equal(largeValue, result);
    }

    [Fact]
    public void FileSystem_GetIniValue_ReturnsEmpty_ForMissingKeyOrFile()
    {
        if (!OperatingSystem.IsWindows()) return;

        string path = Path.Combine(_tempDirectory, "missing.ini");
        string result = _fileSystem.GetIniValue(path, "Section", "Key");
        Assert.Equal(string.Empty, result);

        File.WriteAllText(path, "[Section]\r\nOtherKey=Value\r\n");
        string result2 = _fileSystem.GetIniValue(path, "Section", "Key");
        Assert.Equal(string.Empty, result2);
    }

    [Fact]
    public void FileSystem_ReadAllBytes_ReadsCorrectly()
    {
        string path = Path.Combine(_tempDirectory, "bytes.bin");
        byte[] expected = [1, 2, 3];
        File.WriteAllBytes(path, expected);

        byte[] result = _fileSystem.ReadAllBytes(path);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void FileSystem_OpenRead_OpensStream()
    {
        string path = Path.Combine(_tempDirectory, "stream.txt");
        File.WriteAllText(path, "Hello");

        using var stream = _fileSystem.OpenRead(path);
        Assert.NotNull(stream);
        Assert.True(stream.CanRead);
    }

    [Fact]
    public void FileSystem_GetLastWriteTime_GetsValidDate()
    {
        string path = Path.Combine(_tempDirectory, "date.txt");
        File.WriteAllText(path, "Hello");

        var date = _fileSystem.GetLastWriteTime(path);
        Assert.True(date >= DateTime.UtcNow.AddMinutes(-5));
    }

    [Fact]
    public void FileSystem_GetFileSize_ReturnsCorrectSize()
    {
        string path = Path.Combine(_tempDirectory, "size.txt");
        string content = "12345";
        File.WriteAllText(path, content);

        long size = _fileSystem.GetFileSize(path);
        Assert.Equal(Encoding.UTF8.GetByteCount(content), size);
    }

    [Fact]
    public void FileSystem_WatchDirectory_FiresCallbackOnChange()
    {
        var waitEvent = new ManualResetEventSlim(false);
        int callbackCount = 0;

        var watcher = _fileSystem.WatchDirectory(_tempDirectory, () =>
        {
            callbackCount++;
            waitEvent.Set();
        });

        Assert.NotNull(watcher);

        // Trigger change
        string path = Path.Combine(_tempDirectory, "watch.txt");
        File.WriteAllText(path, "test");

        // Wait for callback
        bool signalled = waitEvent.Wait(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        Assert.True(signalled);
        Assert.True(callbackCount > 0);

        // Wait briefly for OS handles (like FileSystemWatcher) to be fully released before teardown
        watcher.Dispose();
        Thread.Sleep(50);
    }

    [Fact]
    public void FileSystem_WatchDirectory_ReturnsSentinelDisposable_ForMissingDir()
    {
        string missingDir = Path.Combine(_tempDirectory, "missing");
        using var watcher = _fileSystem.WatchDirectory(missingDir, () => { });
        Assert.NotNull(watcher);
    }
}
