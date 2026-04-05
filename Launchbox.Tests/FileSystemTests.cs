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
            catch (UnauthorizedAccessException)
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
        Skip.IfNot(OperatingSystem.IsWindows(), "GetPrivateProfileString is a Windows API");

        string path = Path.Combine(_tempDirectory, "test.ini");
        File.WriteAllText(path, "[Section]\r\nKey=Value\r\n");

        string result = _fileSystem.GetIniValue(path, "Section", "Key");
        Assert.Equal("Value", result);
    }

    [Fact]
    public void FileSystem_GetIniValue_ReturnsValue_LargeBuffer()
    {
        Skip.IfNot(OperatingSystem.IsWindows(), "GetPrivateProfileString is a Windows API");

        string path = Path.Combine(_tempDirectory, "large.ini");
        string largeValue = new string('A', 1000); // Exceeds 512, falls back to ArrayPool
        File.WriteAllText(path, $"[Section]\r\nKey={largeValue}\r\n");

        string result = _fileSystem.GetIniValue(path, "Section", "Key");
        Assert.Equal(largeValue, result);
    }

    [Fact]
    public void FileSystem_GetIniValue_ReturnsEmpty_ForMissingFile()
    {
        Skip.IfNot(OperatingSystem.IsWindows(), "GetPrivateProfileString is a Windows API");

        string path = Path.Combine(_tempDirectory, "missing.ini");
        string result = _fileSystem.GetIniValue(path, "Section", "Key");
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void FileSystem_GetIniValue_ReturnsEmpty_ForMissingKey()
    {
        Skip.IfNot(OperatingSystem.IsWindows(), "GetPrivateProfileString is a Windows API");

        string path = Path.Combine(_tempDirectory, "nokey.ini");
        File.WriteAllText(path, "[Section]\r\nOtherKey=Value\r\n");
        string result = _fileSystem.GetIniValue(path, "Section", "Key");
        Assert.Equal(string.Empty, result);
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
        Assert.InRange(date, DateTime.Now.AddMinutes(-5), DateTime.Now.AddMinutes(1));
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

        var watcher = _fileSystem.WatchDirectory(_tempDirectory, () =>
        {
            waitEvent.Set();
        });

        Assert.NotNull(watcher);

        try
        {
            // Trigger change
            string path = Path.Combine(_tempDirectory, "watch.txt");
            File.WriteAllText(path, "test");

            // Wait for callback
            bool signalled = waitEvent.Wait(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

            Assert.True(signalled);
        }
        finally
        {
            // Wait briefly for OS handles (like FileSystemWatcher) to be fully released before teardown
            watcher.Dispose();
            Thread.Sleep(50);
        }
    }

    [Fact]
    public void FileSystem_WatchDirectory_ReturnsSentinelDisposable_ForMissingDir()
    {
        string missingDir = Path.Combine(_tempDirectory, "missing");
        using var watcher = _fileSystem.WatchDirectory(missingDir, () => { });
        Assert.NotNull(watcher);
    }
}
