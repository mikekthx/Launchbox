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
