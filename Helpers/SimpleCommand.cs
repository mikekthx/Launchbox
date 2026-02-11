using System;
using System.Windows.Input;

namespace Launchbox.Helpers;

public class SimpleCommand : ICommand
{
    private readonly Action<object?> _action;

    public SimpleCommand(Action action)
    {
        if (action == null) throw new ArgumentNullException(nameof(action));
        _action = _ => action();
    }

    public SimpleCommand(Action<object?> action)
    {
        _action = action ?? throw new ArgumentNullException(nameof(action));
    }

    public bool CanExecute(object? parameter) => true;

    public void Execute(object? parameter) => _action(parameter);

    public event EventHandler? CanExecuteChanged { add { } remove { } }
}
