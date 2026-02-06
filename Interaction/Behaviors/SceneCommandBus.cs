using System;
using System.Collections.Generic;

namespace Avalonia3D.Interaction.Behaviors;

public sealed class SceneCommandBus
{
    private readonly List<ISceneCommandHandler> _handlers = [];

    public void RegisterHandler(ISceneCommandHandler handler)
    {
        if (handler == null || _handlers.Contains(handler))
        {
            return;
        }

        _handlers.Add(handler);
    }

    public void UnregisterHandler(ISceneCommandHandler handler)
    {
        if (handler == null)
        {
            return;
        }

        _handlers.Remove(handler);
    }

    public bool Publish(SceneCommand command)
    {
        var handled = false;
        foreach (var handler in _handlers)
        {
            if (!handler.CanHandle(command))
            {
                continue;
            }

            handled |= handler.Handle(command);
        }

        return handled;
    }
}
