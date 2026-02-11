using System;

namespace Launchbox.Services;

public interface IDispatcher
{
    void TryEnqueue(Action action);
}
