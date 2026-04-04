using Launchbox.Services;
using System;
using System.Threading.Tasks;

namespace Launchbox.Tests;

public class MockDispatcher : IDispatcher
{
    public virtual void TryEnqueue(Action action)
    {
        action();
    }

    public Task EnqueueAsync(Func<Task> action)
    {
        return action();
    }
}

/// <summary>
/// A <see cref="MockDispatcher"/> that invokes a callback each time <see cref="TryEnqueue"/> is called,
/// allowing tests to assert that collection mutations were marshalled through the dispatcher.
/// </summary>
public class TrackingMockDispatcher(Action onEnqueue) : MockDispatcher
{
    public override void TryEnqueue(Action action)
    {
        onEnqueue();
        base.TryEnqueue(action);
    }
}
