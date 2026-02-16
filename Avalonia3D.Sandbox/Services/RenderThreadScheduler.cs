using System;
using System.Collections.Concurrent;

namespace Avalonia3D.Sandbox.Services;

public sealed class RenderThreadScheduler : IRenderThreadScheduler
{
    private readonly ConcurrentQueue<Action> _queue = new();
    public event Action? WorkEnqueued;

    public void Enqueue(Action action)
    {
        if (action == null)
        {
            throw new ArgumentNullException(nameof(action));
        }

        _queue.Enqueue(action);
        WorkEnqueued?.Invoke();
    }

    public int ExecutePending()
    {
        var executed = 0;
        while (_queue.TryDequeue(out var action))
        {
            action();
            executed++;
        }

        return executed;
    }
}
