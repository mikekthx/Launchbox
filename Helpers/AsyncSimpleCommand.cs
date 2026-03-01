using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows.Input;

namespace Launchbox.Helpers;

public class AsyncSimpleCommand : ICommand
{
    private readonly Func<Task> _asyncAction;

    public AsyncSimpleCommand(Func<Task> asyncAction)
    {
        _asyncAction = asyncAction ?? throw new ArgumentNullException(nameof(asyncAction));
    }

    public bool CanExecute(object? parameter) => true;

    public async void Execute(object? parameter)
    {
        try
        {
            await _asyncAction();
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"AsyncSimpleCommand failed: {ex.Message}");
        }
    }

    public event EventHandler? CanExecuteChanged { add { } remove { } }
}

public class AsyncSimpleCommand<T> : ICommand
{
    private readonly Func<T?, Task> _asyncAction;

    public AsyncSimpleCommand(Func<T?, Task> asyncAction)
    {
        _asyncAction = asyncAction ?? throw new ArgumentNullException(nameof(asyncAction));
    }

    public bool CanExecute(object? parameter) => true;

    public async void Execute(object? parameter)
    {
        try
        {
            T? val = parameter is T typedParameter ? typedParameter : default;
            await _asyncAction(val);
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"AsyncSimpleCommand<{typeof(T).Name}> failed: {ex.Message}");
        }
    }

    public event EventHandler? CanExecuteChanged { add { } remove { } }
}
