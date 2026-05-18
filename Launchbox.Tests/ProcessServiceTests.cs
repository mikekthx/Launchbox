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
    public void IsProcessRunning_PropagatesException_WhenGetProcessesThrows()
    {
        // Arrange
        var service = new FaultyProcessService();

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => service.IsProcessRunning("TestProcess"));
        Assert.Equal("Simulated exception from GetProcessesByName", exception.Message);
    }

    private class FaultyProcessService : ProcessService
    {
        protected override Process[] GetProcessesByName(string processName)
        {
            throw new InvalidOperationException("Simulated exception from GetProcessesByName");
        }
    }
}
