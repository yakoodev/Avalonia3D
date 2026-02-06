using System;

namespace Avalonia3D.Sandbox.Services;

public interface IRenderThreadScheduler
{
    void Enqueue(Action action);
}

