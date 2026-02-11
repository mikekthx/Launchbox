using Microsoft.UI.Dispatching;
using System;

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
}
