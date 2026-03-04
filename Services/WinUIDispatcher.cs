using Microsoft.UI.Dispatching;
using System;
using System.Threading.Tasks;

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
        TryEnqueueInternal(() => action());
    }

    public Task EnqueueAsync(Func<Task> action)
    {
        var tcs = new TaskCompletionSource();

        if (!TryEnqueueInternal(async () =>
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

    protected virtual bool TryEnqueueInternal(Action callback)
    {
        return _dispatcherQueue?.TryEnqueue(new Microsoft.UI.Dispatching.DispatcherQueueHandler(callback)) ?? false;
    }
}
