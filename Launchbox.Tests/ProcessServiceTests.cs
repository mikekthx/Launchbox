using Launchbox.Services;
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
    public void IsProcessRunning_ExecutesFinallySafely_WhenGetProcessesThrows()
    {
        // Arrange
        var service = new FaultyProcessService();

        // Act & Assert
        // We verify that the exception is propagated correctly,
        // and that it does NOT throw a NullReferenceException in the finally block.
        var exception = Assert.Throws<System.InvalidOperationException>(() => service.IsProcessRunning("TestProcess"));
        Assert.Equal("Simulated exception from GetProcessesByName", exception.Message);
    }

    private class FaultyProcessService : ProcessService
    {
        protected override Process[] GetProcessesByName(string processName)
        {
            throw new System.InvalidOperationException("Simulated exception from GetProcessesByName");
        }
    }
}
