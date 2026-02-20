namespace Avalonia3D.Sandbox.Rendering;

public static class FramebufferTargetResolver
{
    public static uint Resolve(int incomingFramebufferId, int? lastValidFramebufferId, out int? updatedLastValidFramebufferId)
    {
        if (incomingFramebufferId >= 0)
        {
            updatedLastValidFramebufferId = incomingFramebufferId;
            return (uint)incomingFramebufferId;
        }

        if (lastValidFramebufferId.HasValue)
        {
            updatedLastValidFramebufferId = lastValidFramebufferId;
            return (uint)lastValidFramebufferId.Value;
        }

        updatedLastValidFramebufferId = 0;
        return 0;
    }
}
