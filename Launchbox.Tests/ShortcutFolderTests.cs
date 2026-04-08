using Launchbox.Models;
using System;
using Xunit;

namespace Launchbox.Tests;

public class ShortcutFolderTests
{
    [Fact]
    public void ExpandedPath_NoEnvVariables_ReturnsOriginalPath()
    {
        var folder = new ShortcutFolder { Path = @"C:\Test\Path", Label = "Test", Order = 0 };

        Assert.Equal(@"C:\Test\Path", folder.ExpandedPath);
    }

    [Fact]
    public void ExpandedPath_WithSingleEnvVariable_ExpandsCorrectly()
    {
        const string varName = "SF_TEST_VAR_SINGLE";
        const string varValue = "ExpandedValue";
        Environment.SetEnvironmentVariable(varName, varValue);
        try
        {
            var folder = new ShortcutFolder { Path = $@"C:\%{varName}%\Path", Label = "Test", Order = 0 };

            Assert.Equal($@"C:\{varValue}\Path", folder.ExpandedPath);
        }
        finally
        {
            Environment.SetEnvironmentVariable(varName, null);
        }
    }

    [Fact]
    public void ExpandedPath_WithMultipleEnvVariables_ExpandsCorrectly()
    {
        const string var1Name = "SF_TEST_VAR_MULT1";
        const string var1Value = "Val1";
        const string var2Name = "SF_TEST_VAR_MULT2";
        const string var2Value = "Val2";
        Environment.SetEnvironmentVariable(var1Name, var1Value);
        Environment.SetEnvironmentVariable(var2Name, var2Value);
        try
        {
            var folder = new ShortcutFolder { Path = $@"C:\%{var1Name}%\%{var2Name}%", Label = "Test", Order = 0 };

            Assert.Equal($@"C:\{var1Value}\{var2Value}", folder.ExpandedPath);
        }
        finally
        {
            Environment.SetEnvironmentVariable(var1Name, null);
            Environment.SetEnvironmentVariable(var2Name, null);
        }
    }

    [Fact]
    public void ExpandedPath_WithNonExistentEnvVariable_ReturnsUnchangedVariable()
    {
        var folder = new ShortcutFolder { Path = @"C:\%SF_NON_EXISTENT_VAR%\Path", Label = "Test", Order = 0 };

        Assert.Equal(@"C:\%SF_NON_EXISTENT_VAR%\Path", folder.ExpandedPath);
    }

    [Fact]
    public void ExpandedPath_UpdateAfterWithExpression_ReflectsNewPath()
    {
        const string varName = "SF_TEST_VAR_WITH";
        const string varValue = "ValueA";
        Environment.SetEnvironmentVariable(varName, varValue);
        try
        {
            var folder = new ShortcutFolder { Path = $@"C:\%{varName}%", Label = "Test", Order = 0 };
            Assert.Equal($@"C:\{varValue}", folder.ExpandedPath);

            var folder2 = folder with { Path = $@"D:\%{varName}%" };

            Assert.Equal($@"D:\{varValue}", folder2.ExpandedPath);
        }
        finally
        {
            Environment.SetEnvironmentVariable(varName, null);
        }
    }
}
