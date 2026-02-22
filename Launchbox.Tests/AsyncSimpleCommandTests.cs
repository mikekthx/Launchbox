using Launchbox.Helpers;
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Xunit;

namespace Launchbox.Tests;

public class AsyncSimpleCommandTests
{
    [Fact]
    public void Constructor_NullAction_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new AsyncSimpleCommand(null!));
    }

    [Fact]
    public void CanExecute_ReturnsTrue()
    {
        var command = new AsyncSimpleCommand(() => Task.CompletedTask);
        Assert.True(command.CanExecute(null));
        Assert.True(command.CanExecute(new object()));
    }

    [Fact]
    public void CanExecuteChanged_AddRemove_DoesNotThrow()
    {
        var command = new AsyncSimpleCommand(() => Task.CompletedTask);
        EventHandler handler = (s, e) => { };

        // Should not throw
        var exception = Record.Exception(() =>
        {
            command.CanExecuteChanged += handler;
            command.CanExecuteChanged -= handler;
        });

        Assert.Null(exception);
    }

    [Fact]
    public async Task Execute_InvokesAsyncAction()
    {
        var tcs = new TaskCompletionSource();
        var command = new AsyncSimpleCommand(async () =>
        {
            await Task.Yield();
            tcs.SetResult();
        });

        command.Execute(null);

        // Wait for completion with timeout to avoid hanging if it fails
        var completedTask = await Task.WhenAny(tcs.Task, Task.Delay(1000));

        if (completedTask == tcs.Task)
        {
            Assert.True(tcs.Task.IsCompletedSuccessfully);
        }
        else
        {
            Assert.Fail("Async action was not invoked within timeout.");
        }
    }

    [Fact]
    public void Execute_SwallowsException()
    {
        // This test ensures that if the async action throws, the application doesn't crash.
        // Since Execute is async void, an unhandled exception would crash the process.
        // The implementation has a try/catch block to prevent this.

        var command = new AsyncSimpleCommand(() => throw new Exception("Test Exception"));

        // Using Record.Exception on an async void method only catches synchronous exceptions
        // that happen before the first await. Since our action throws immediately, it should be caught.

        var exception = Record.Exception(() => command.Execute(null));

        Assert.Null(exception);
    }

    [Fact]
    public async Task Execute_SwallowsAsyncException()
    {
        // Test async exception
        var tcs = new TaskCompletionSource();
        var command = new AsyncSimpleCommand(async () =>
        {
            await Task.Yield();
            throw new Exception("Async Test Exception");
            // If this exception is not caught inside Execute, it would crash the test runner (process).
            // However, since we can't easily assert on "process did not crash" directly other than the test passing,
            // we rely on the fact that if it crashed, the test run would abort.
        });

        command.Execute(null);

        // Give it a moment to fail if it's going to fail
        await Task.Delay(100);

        // If we reached here, it didn't crash.
        Assert.True(true);
    }
}
