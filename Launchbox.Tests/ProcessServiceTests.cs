using Launchbox.Services;
using System;
using System.Diagnostics;
using Xunit;

namespace Launchbox.Tests;

public class ProcessServiceTests
{
    private readonly ProcessService _processService;

    public ProcessServiceTests()
    {
        _processService = new ProcessService();
    }

    [Fact]
    public void IsProcessRunning_ReturnsTrue_WhenProcessExists()
    {
        // Arrange
        var currentProcessName = Process.GetCurrentProcess().ProcessName;

        // Act
        var isRunning = _processService.IsProcessRunning(currentProcessName);

        // Assert
        Assert.True(isRunning);
    }

    [Fact]
    public void IsProcessRunning_ReturnsFalse_WhenProcessDoesNotExist()
    {
        // Arrange
        var nonExistentProcessName = "ThisProcessShouldNotBeRunning_123456789";

        // Act
        var isRunning = _processService.IsProcessRunning(nonExistentProcessName);

        // Assert
        Assert.False(isRunning);
    }

    [Fact]
    public void IsProcessRunning_ReturnsFalse_WhenGetProcessesThrows()
    {
        // Arrange
        var service = new FaultyProcessService();

        // Act
        var isRunning = service.IsProcessRunning("TestProcess");

        // Assert
        Assert.False(isRunning);
    }

    private class FaultyProcessService : ProcessService
    {
        protected override Process[] GetProcessesByName(string processName)
        {
            throw new InvalidOperationException("Simulated exception from GetProcessesByName");
        }
    }
}
