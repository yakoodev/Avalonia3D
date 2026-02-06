using System;
using System.Collections.Concurrent;

namespace Avalonia3D.Sandbox.Services;

public sealed class RenderThreadScheduler : IRenderThreadScheduler
{
    private readonly ConcurrentQueue<Action> _queue = new();

    public void Enqueue(Action action)
    {
        if (action == null)
        {
            throw new ArgumentNullException(nameof(action));
        }

        _queue.Enqueue(action);
    }

    public void ExecutePending()
    {
        while (_queue.TryDequeue(out var action))
        {
            action();
        }
    }
}
