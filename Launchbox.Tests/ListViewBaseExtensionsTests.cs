using System;
using System.Windows.Input;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Launchbox.Helpers;
using Xunit;
using Windows.System;
using Microsoft.UI.Xaml;

namespace Launchbox.Tests;

public class ListViewBaseExtensionsTests
{
    private class TestCommand : ICommand
    {
        public bool CanExecuteResult { get; set; } = true;
        public object? ExecutedParameter { get; private set; }
        public bool WasExecuted { get; private set; }

        public event EventHandler? CanExecuteChanged { add {} remove {} }

        public bool CanExecute(object? parameter) => CanExecuteResult;

        public void Execute(object? parameter)
        {
            WasExecuted = true;
            ExecutedParameter = parameter;
        }
    }

    // Note: A full functional test of OnListViewBaseKeyDown requires a running
    // WinUI dispatcher and a constructed visual tree to satisfy FocusManager.GetFocusedElement.
    // In a unit test context without UI threads, we can at least test property registration
    // and basic getter/setter structure.

    [Fact]
    public void SetEnterCommand_SetsProperty()
    {
        var listView = new ListView();
        var command = new TestCommand();

        ListViewBaseExtensions.SetEnterCommand(listView, command);

        var result = ListViewBaseExtensions.GetEnterCommand(listView);
        Assert.Same(command, result);
    }
}
