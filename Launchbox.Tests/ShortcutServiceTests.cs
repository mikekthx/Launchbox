using Launchbox.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace Launchbox.Tests;

public class ShortcutServiceTests
{
    private static readonly string SHORTCUT_FOLDER = Path.Combine("Shortcuts");
    private static readonly string[] ALLOWED_EXTENSIONS = [".lnk", ".url"];

    [Fact]
    public void GetShortcutFiles_ReturnsNull_WhenDirectoryDoesNotExist()
    {
        var mockFileSystem = new MockFileSystem();
        var service = new ShortcutService(mockFileSystem);

        var result = service.GetShortcutFiles(SHORTCUT_FOLDER, ALLOWED_EXTENSIONS, TestContext.Current.CancellationToken);

        Assert.Null(result);
    }

    [Fact]
    public void GetShortcutFiles_ReturnsFilteredFiles_WhenDirectoryExists()
    {
        var mockFileSystem = new MockFileSystem();
        mockFileSystem.AddDirectory(SHORTCUT_FOLDER);
        mockFileSystem.AddFile(SHORTCUT_FOLDER, "App1.lnk");
        mockFileSystem.AddFile(SHORTCUT_FOLDER, "App2.url");
        mockFileSystem.AddFile(SHORTCUT_FOLDER, "App3.txt"); // Should be filtered out

        var service = new ShortcutService(mockFileSystem);

        var result = service.GetShortcutFiles(SHORTCUT_FOLDER, ALLOWED_EXTENSIONS, TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Equal(2, result.Length);
        Assert.Contains(result, f => f.EndsWith("App1.lnk"));
        Assert.Contains(result, f => f.EndsWith("App2.url"));
        Assert.DoesNotContain(result, f => f.EndsWith("App3.txt"));
    }

    [Fact]
    public void GetShortcutFiles_HandlesCaseInsensitiveExtensions()
    {
        var mockFileSystem = new MockFileSystem();
        mockFileSystem.AddDirectory(SHORTCUT_FOLDER);
        mockFileSystem.AddFile(SHORTCUT_FOLDER, "App1.LNK"); // Uppercase extension
        mockFileSystem.AddFile(SHORTCUT_FOLDER, "App2.URL"); // Uppercase extension
        mockFileSystem.AddFile(SHORTCUT_FOLDER, "App3.lnk"); // Lowercase extension

        var service = new ShortcutService(mockFileSystem);

        var result = service.GetShortcutFiles(SHORTCUT_FOLDER, ALLOWED_EXTENSIONS, TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Equal(3, result.Length);
        Assert.Contains(result, f => f.EndsWith("App1.LNK"));
        Assert.Contains(result, f => f.EndsWith("App2.URL"));
        Assert.Contains(result, f => f.EndsWith("App3.lnk"));
    }

    [Fact]
    public void GetShortcutFiles_HandlesUppercaseAllowedExtensions()
    {
        var mockFileSystem = new MockFileSystem();
        mockFileSystem.AddDirectory(SHORTCUT_FOLDER);
        mockFileSystem.AddFile(SHORTCUT_FOLDER, "App1.lnk");

        var service = new ShortcutService(mockFileSystem);
        var uppercaseExtensions = new[] { ".LNK" };

        var result = service.GetShortcutFiles(SHORTCUT_FOLDER, uppercaseExtensions, TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Single(result);
        Assert.Contains(result, f => f.EndsWith("App1.lnk"));
    }

    [Fact]
    public void GetShortcutFiles_ReturnsNull_WhenAllowedExtensionsIsNull()
    {
        var mockFileSystem = new MockFileSystem();
        mockFileSystem.AddDirectory(SHORTCUT_FOLDER);
        var service = new ShortcutService(mockFileSystem);

        var result = service.GetShortcutFiles(SHORTCUT_FOLDER, null!, TestContext.Current.CancellationToken);

        Assert.Null(result);
    }

    [Fact]
    public void GetShortcutFiles_ReturnsNull_WhenUnauthorizedAccessExceptionThrown()
    {
        var exception = new UnauthorizedAccessException("Access denied");
        var mockFileSystem = new ExceptionThrowingFileSystem(exception);
        mockFileSystem.AddDirectory(SHORTCUT_FOLDER);
        var service = new ShortcutService(mockFileSystem);

        var result = service.GetShortcutFiles(SHORTCUT_FOLDER, ALLOWED_EXTENSIONS, TestContext.Current.CancellationToken);

        Assert.Null(result);
    }

    [Fact]
    public void GetShortcutFiles_ReturnsNull_WhenIOExceptionThrown()
    {
        var exception = new IOException("I/O error");
        var mockFileSystem = new ExceptionThrowingFileSystem(exception);
        mockFileSystem.AddDirectory(SHORTCUT_FOLDER);
        var service = new ShortcutService(mockFileSystem);

        var result = service.GetShortcutFiles(SHORTCUT_FOLDER, ALLOWED_EXTENSIONS, TestContext.Current.CancellationToken);

        Assert.Null(result);
    }

    private class ExceptionThrowingFileSystem : MockFileSystem
    {
        private readonly Exception _exceptionToThrow;

        public ExceptionThrowingFileSystem(Exception exceptionToThrow)
        {
            _exceptionToThrow = exceptionToThrow;
        }

        public override IEnumerable<string> EnumerateFiles(string path)
        {
            throw _exceptionToThrow;
        }
    }
}
