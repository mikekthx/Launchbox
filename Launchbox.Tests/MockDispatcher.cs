using System;
using System.Threading.Tasks;
using Launchbox.Services;

namespace Launchbox.Tests;

public class MockDispatcher : IDispatcher
{
    public void TryEnqueue(Action action)
    {
        action();
    }

    public Task EnqueueAsync(Func<Task> action)
    {
        return action();
    }
}
