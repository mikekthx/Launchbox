using Launchbox.Services;
using System;
using Xunit;

namespace Launchbox.Tests;

public class FileSystemSecurityTests
{
    private readonly FileSystem _fileSystem;

    public FileSystemSecurityTests()
    {
        _fileSystem = new FileSystem();
    }

    [Theory]
    [InlineData(@"\\attacker\share\file.txt")]
    [InlineData(@"//attacker/share/file.txt")]
    [InlineData(@"/\attacker/share/file.txt")]
    [InlineData(@"\/attacker/share/file.txt")]
    [InlineData(@"\\?\UNC\attacker\share\file.txt")]
    [InlineData(@"\??\UNC\attacker\share\file.txt")]
    [InlineData("path|with|pipe")]
    public void FileSystem_DirectoryExists_ReturnsFalse_ForUnsafePath(string unsafePath)
    {
        bool result = _fileSystem.DirectoryExists(unsafePath);
        Assert.False(result);
    }

    [Theory]
    [InlineData(@"\\attacker\share\file.txt")]
    [InlineData(@"//attacker/share/file.txt")]
    public void FileSystem_FileExists_ReturnsFalse_ForUnsafePath(string unsafePath)
    {
        bool result = _fileSystem.FileExists(unsafePath);
        Assert.False(result);
    }

    [Theory]
    [InlineData(@"\\attacker\share\file.txt")]
    [InlineData(@"//attacker/share/file.txt")]
    public void FileSystem_GetFiles_ReturnsEmptyArray_ForUnsafePath(string unsafePath)
    {
        string[] result = _fileSystem.GetFiles(unsafePath);
        Assert.Empty(result);
    }

    [Theory]
    [InlineData(@"\\attacker\share\file.txt")]
    [InlineData(@"//attacker/share/file.txt")]
    public void FileSystem_GetIniValue_ReturnsEmptyString_ForUnsafePath(string unsafePath)
    {
        string result = _fileSystem.GetIniValue(unsafePath, "Section", "Key");
        Assert.Equal(string.Empty, result);
    }

    [Theory]
    [InlineData(@"\\attacker\share\file.txt")]
    [InlineData(@"//attacker/share/file.txt")]
    public void FileSystem_CreateDirectory_ThrowsUnauthorizedAccessException_ForUnsafePath(string unsafePath)
    {
        Assert.Throws<UnauthorizedAccessException>(() => _fileSystem.CreateDirectory(unsafePath));
    }

    [Theory]
    [InlineData(@"\\attacker\share\file.txt")]
    [InlineData(@"//attacker/share/file.txt")]
    public void FileSystem_ReadAllBytes_ThrowsUnauthorizedAccessException_ForUnsafePath(string unsafePath)
    {
        Assert.Throws<UnauthorizedAccessException>(() => _fileSystem.ReadAllBytes(unsafePath));
    }

    [Theory]
    [InlineData(@"\\attacker\share\file.txt")]
    [InlineData(@"//attacker/share/file.txt")]
    public void FileSystem_OpenRead_ThrowsUnauthorizedAccessException_ForUnsafePath(string unsafePath)
    {
        Assert.Throws<UnauthorizedAccessException>(() => _fileSystem.OpenRead(unsafePath));
    }

    [Theory]
    [InlineData(@"\\attacker\share\file.txt")]
    [InlineData(@"//attacker/share/file.txt")]
    public void FileSystem_GetLastWriteTime_ThrowsUnauthorizedAccessException_ForUnsafePath(string unsafePath)
    {
        Assert.Throws<UnauthorizedAccessException>(() => _fileSystem.GetLastWriteTime(unsafePath));
    }

    [Theory]
    [InlineData(@"\\attacker\share\file.txt")]
    [InlineData(@"//attacker/share/file.txt")]
    public void FileSystem_GetFileSize_ThrowsUnauthorizedAccessException_ForUnsafePath(string unsafePath)
    {
        Assert.Throws<UnauthorizedAccessException>(() => _fileSystem.GetFileSize(unsafePath));
    }
}
