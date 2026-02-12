using Avalonia3D.Model;
using Avalonia3D.Sandbox.Scenes;
using Serilog;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace Avalonia3D.Sandbox.Services;

public sealed class DefaultSceneCameraPolicy : ISceneCameraPolicy
{
    private static readonly Dictionary<string, CameraPreset> CameraPresets = new(StringComparer.OrdinalIgnoreCase)
    {
        ["default"] = new CameraPreset(12f, -0.3f, 0.6f, 0.1f, 200f),
        ["vehicle"] = new CameraPreset(35f, -0.2f, 0.6f, 0.1f, 1000f)
    };

    public void ApplyDefaults(Scene3D scene3D, ISandboxScene sceneInfo)
    {
        scene3D.Lights.Clear();

        var preset = CameraPresets.TryGetValue(sceneInfo.Id, out var scenePreset)
            ? scenePreset
            : CameraPresets["default"];

        var camera = scene3D.Camera;
        camera.Target = Vector3.Zero;
        camera.Distance = preset.Distance;
        camera.Pitch = preset.Pitch;
        camera.Yaw = preset.Yaw;
        camera.Fov = MathF.PI / 4;
        camera.Near = preset.Near;
        camera.Far = preset.Far;
    }

    public void ApplyPostLoad(Scene3D scene3D, ISandboxScene sceneInfo, SceneLoadOptions loadOptions)
    {
        if (loadOptions.AutoFrameCamera && SceneCameraFramer.TryFrame(scene3D.SceneGraph, scene3D.Camera))
        {
            Log.Information("Camera auto-framed for scene {SceneId}. Target: {Target}, Distance: {Distance:0.00}, Near/Far: {Near:0.00}/{Far:0.00}",
                sceneInfo.Id,
                scene3D.Camera.Target,
                scene3D.Camera.Distance,
                scene3D.Camera.Near,
                scene3D.Camera.Far);
            return;
        }

        if (loadOptions.AutoFrameCamera)
        {
            Log.Warning("Scene {SceneId} has no geometry bounds for auto-frame; using preset camera.", sceneInfo.Id);
            return;
        }

        Log.Information("Scene {SceneId} requested fixed camera. Auto-frame skipped.", sceneInfo.Id);
    }

    private readonly record struct CameraPreset(float Distance, float Pitch, float Yaw, float Near, float Far);
}
