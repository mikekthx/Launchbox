using System;
using System.Windows.Input;

namespace Launchbox;

public class SimpleCommand : ICommand
{
    private readonly Action _action;

    public SimpleCommand(Action action)
    {
        _action = action ?? throw new ArgumentNullException(nameof(action));
    }

    public bool CanExecute(object? parameter) => true;

    public void Execute(object? parameter) => _action();

    public event EventHandler? CanExecuteChanged { add { } remove { } }
}
