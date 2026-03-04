using Launchbox.Services;
using System;
using System.Threading.Tasks;
using Xunit;

namespace Launchbox.Tests;

public class WinUIDispatcherTests
{
    private class TestableWinUIDispatcher : WinUIDispatcher
    {
        public bool SimulateEnqueueFailure { get; set; }
        public Action? CapturedCallback { get; private set; }

        public TestableWinUIDispatcher() : base(null!) // Pass null for DispatcherQueue
        {
        }

        protected override bool TryEnqueueInternal(Action callback)
        {
            if (SimulateEnqueueFailure)
            {
                return false;
            }

            CapturedCallback = callback;
            // Execute the callback synchronously for testing
            callback();
            return true;
        }
    }

    [Fact]
    public async Task EnqueueAsync_ExecutesActionSuccessfully()
    {
        // Arrange
        var dispatcher = new TestableWinUIDispatcher();
        var actionExecuted = false;

        // Act
        await dispatcher.EnqueueAsync(() =>
        {
            actionExecuted = true;
            return Task.CompletedTask;
        });

        // Assert
        Assert.True(actionExecuted);
    }

    [Fact]
    public async Task EnqueueAsync_ActionThrowsException_SetsExceptionOnTask()
    {
        // Arrange
        var dispatcher = new TestableWinUIDispatcher();
        var expectedException = new InvalidOperationException("Test exception");

        // Act
        var task = dispatcher.EnqueueAsync(() => throw expectedException);

        // Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => task);
        Assert.Equal(expectedException.Message, exception.Message);
    }

    [Fact]
    public async Task EnqueueAsync_EnqueueFails_ReturnsFailedTask()
    {
        // Arrange
        var dispatcher = new TestableWinUIDispatcher { SimulateEnqueueFailure = true };

        // Act
        var task = dispatcher.EnqueueAsync(() => Task.CompletedTask);

        // Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => task);
        Assert.Equal("Failed to enqueue operation.", exception.Message);
    }

    [Fact]
    public void TryEnqueue_ExecutesActionSuccessfully()
    {
        // Arrange
        var dispatcher = new TestableWinUIDispatcher();
        var actionExecuted = false;

        // Act
        dispatcher.TryEnqueue(() => actionExecuted = true);

        // Assert
        Assert.True(actionExecuted);
    }
}
