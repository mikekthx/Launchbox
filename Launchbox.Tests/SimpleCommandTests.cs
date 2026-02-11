using Xunit;
using Launchbox;
using System;

namespace Launchbox.Tests;

/// <summary>
/// Verifies the behavior of SimpleCommand to ensure core command logic is reliable.
/// </summary>
public class SimpleCommandTests
{
    [Fact]
    public void Execute_InvokesAction()
    {
        bool executed = false;
        var command = new SimpleCommand(() => executed = true);

        command.Execute(null);

        Assert.True(executed);
    }

    [Fact]
    public void CanExecute_ReturnsTrue()
    {
        var command = new SimpleCommand(() => { });
        Assert.True(command.CanExecute(null));
        Assert.True(command.CanExecute(new object()));
    }

    [Fact]
    public void CanExecuteChanged_AddRemove_DoesNotThrow()
    {
        var command = new SimpleCommand(() => { });
        var handler = new EventHandler((s, e) => { });

        var exception = Record.Exception(() =>
        {
            command.CanExecuteChanged += handler;
            command.CanExecuteChanged -= handler;
        });

        Assert.Null(exception);
    }

    [Fact]
    public void Constructor_NullAction_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new SimpleCommand((Action)null!));
        Assert.Throws<ArgumentNullException>(() => new SimpleCommand((Action<object?>)null!));
    }
}
