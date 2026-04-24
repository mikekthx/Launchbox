using Launchbox.Helpers;
using System;
using System.Windows.Input;
using Windows.System;
using Xunit;

namespace Launchbox.Tests;

public class ListViewBaseExtensionsTests
{
    private class TestCommand : ICommand
    {
        public bool CanExecuteResult { get; set; } = true;
        public object? ExecutedParameter { get; private set; }
        public bool WasExecuted { get; private set; }

        public event EventHandler? CanExecuteChanged { add { } remove { } }

        public bool CanExecute(object? parameter) => CanExecuteResult;

        public void Execute(object? parameter)
        {
            WasExecuted = true;
            ExecutedParameter = parameter;
        }
    }

    [Fact]
    public void TryExecuteEnterCommand_ReturnsFalse_WhenKeyIsNotEnter()
    {
        var command = new TestCommand();
        var dataContext = new object();

        var result = ListViewBaseExtensions.TryExecuteEnterCommand(VirtualKey.Space, command, dataContext);

        Assert.False(result);
        Assert.False(command.WasExecuted);
    }

    [Fact]
    public void TryExecuteEnterCommand_ReturnsFalse_WhenCommandIsNull()
    {
        var dataContext = new object();

        var result = ListViewBaseExtensions.TryExecuteEnterCommand(VirtualKey.Enter, null, dataContext);

        Assert.False(result);
    }

    [Fact]
    public void TryExecuteEnterCommand_ReturnsFalse_WhenDataContextIsNull()
    {
        var command = new TestCommand();

        var result = ListViewBaseExtensions.TryExecuteEnterCommand(VirtualKey.Enter, command, null);

        Assert.False(result);
        Assert.False(command.WasExecuted);
    }

    [Fact]
    public void TryExecuteEnterCommand_ReturnsFalse_WhenCanExecuteIsFalse()
    {
        var command = new TestCommand { CanExecuteResult = false };
        var dataContext = new object();

        var result = ListViewBaseExtensions.TryExecuteEnterCommand(VirtualKey.Enter, command, dataContext);

        Assert.False(result);
        Assert.False(command.WasExecuted);
    }

    [Fact]
    public void TryExecuteEnterCommand_ReturnsTrue_AndExecutesCommand_WhenConditionsAreMet()
    {
        var command = new TestCommand();
        var dataContext = new object();

        var result = ListViewBaseExtensions.TryExecuteEnterCommand(VirtualKey.Enter, command, dataContext);

        Assert.True(result);
        Assert.True(command.WasExecuted);
        Assert.Same(dataContext, command.ExecutedParameter);
    }
}
