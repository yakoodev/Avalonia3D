namespace Avalonia3D.Sandbox.Rendering;

public static class FramebufferTargetResolver
{
    public static bool TryResolve(int incomingFramebufferId, int? lastValidFramebufferId, out uint outputFramebufferId, out int? updatedLastValidFramebufferId)
    {
        if (incomingFramebufferId >= 0)
        {
            outputFramebufferId = (uint)incomingFramebufferId;
            updatedLastValidFramebufferId = incomingFramebufferId;
            return true;
        }

        if (lastValidFramebufferId == 0)
        {
            outputFramebufferId = 0;
            updatedLastValidFramebufferId = lastValidFramebufferId;
            return true;
        }

        outputFramebufferId = 0;
        updatedLastValidFramebufferId = lastValidFramebufferId;
        return false;
    }
}
