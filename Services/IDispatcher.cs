using System;
using System.Threading.Tasks;

namespace Launchbox.Services;

public interface IDispatcher
{
    void TryEnqueue(Action action);
    Task EnqueueAsync(Func<Task> action);
}
