using Avalonia3D.Lights;
using Avalonia3D.Model;
using Avalonia3D.Rendering;
using Avalonia3D.Shaders;
using System;
using System.Numerics;

namespace Avalonia3D.Composition;

public sealed class DefaultSceneBootstrap : ISceneBootstrap
{
    public static DefaultSceneBootstrap Instance { get; } = new();

    private DefaultSceneBootstrap()
    {
    }

    public void Bootstrap(Scene3D scene, GraphicsProfile? profile = null)
    {
        var validatedProfile = (profile ?? GraphicsProfile.Medium).Validate();
        scene.ApplyGraphicsProfile(validatedProfile);
        SceneShaderRegistryBootstrap.Configure(scene, validatedProfile.MaxLights);

        scene.Lights.Add(new Light
        {
            Position = new Vector3(0f, 600f, 600f),
            Color = new Vector3(1f, 1f, 1f),
            Intensity = 0.5f
        });

        scene.Lights.Add(new Light
        {
            Position = new Vector3(100f, 300f, 300f),
            Color = new Vector3(1f, 1f, 1f),
            Intensity = 0.5f
        });

        scene.Camera.Distance = Scene3DDefault.DistantionBase;
        scene.Camera.Pitch = Scene3DDefault.PitchBase;
        scene.Camera.Yaw = Scene3DDefault.YawBase;
        scene.Camera.Fov = MathF.PI / 4;
        scene.Camera.Near = 0.1f;
        scene.Camera.Far = 1400f;
    }
}
