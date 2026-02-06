using Avalonia3D.Interaction.CameraController;
using Avalonia3D.Model;
using System;

namespace Avalonia3D.Sandbox.Services;

public static class SceneCameraFramer
{
    public static bool TryFrame(SceneGraph sceneGraph, Camera camera, float minDistance = 3f)
    {
        if (sceneGraph == null || camera == null)
        {
            return false;
        }

        var controller = new CameraController(camera, sceneGraph);
        return controller.FrameAll(minDistance);
    }

    public static bool TryComputeWorldBounds(SceneGraph sceneGraph, out System.Numerics.Vector3 min, out System.Numerics.Vector3 max)
    {
        return CameraBoundsCalculator.TryComputeWorldBounds(sceneGraph, out min, out max);
    }
}
