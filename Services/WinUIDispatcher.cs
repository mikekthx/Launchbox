using System;
using System.Threading.Tasks;
using Microsoft.UI.Dispatching;

namespace Launchbox.Services;

public class WinUIDispatcher : IDispatcher
{
    private readonly DispatcherQueue _dispatcherQueue;

    public WinUIDispatcher(DispatcherQueue dispatcherQueue)
    {
        _dispatcherQueue = dispatcherQueue;
    }

    public void TryEnqueue(Action action)
    {
        _dispatcherQueue.TryEnqueue(() => action());
    }

    public Task EnqueueAsync(Func<Task> action)
    {
        var tcs = new TaskCompletionSource();

        if (!_dispatcherQueue.TryEnqueue(async () =>
        {
            try
            {
                await action();
                tcs.SetResult();
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        }))
        {
            tcs.SetException(new InvalidOperationException("Failed to enqueue operation."));
        }

        return tcs.Task;
    }
}
