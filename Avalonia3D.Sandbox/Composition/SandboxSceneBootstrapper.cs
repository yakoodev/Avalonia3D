using Avalonia3D.Composition;
using Avalonia3D.Interaction.Behaviors;
using Avalonia3D.Model;
using Avalonia3D.Plugins.Wheel;
using Avalonia3D.Rendering;
using System;
using System.IO;

namespace Avalonia3D.Sandbox.Composition;

public sealed class SandboxSceneBootstrapper : ISceneBootstrap
{
    private readonly ISceneBootstrap _innerBootstrap;
    private readonly WheelSceneModule _wheelModule = new();

    public SandboxSceneBootstrapper(ISceneBootstrap? innerBootstrap = null)
    {
        _innerBootstrap = innerBootstrap ?? DefaultSceneBootstrap.Instance;
    }

    public void Bootstrap(Scene3D scene, GraphicsProfile? profile = null)
    {
        _innerBootstrap.Bootstrap(scene, profile);
        scene.RegisterModule(_wheelModule);

        scene.RegisterBehavior(new DoorBehavior("door.main"));
        scene.RegisterBehavior(new WheelRotationBehavior("wheel.front.left", radiansPerSecond: 2.5f));

        var wheelAssetsPath = Path.Combine(AppContext.BaseDirectory, "Assets", "gltf");
        if (!Directory.Exists(wheelAssetsPath))
        {
            return;
        }

        _wheelModule.Load(wheelAssetsPath, scene.Importer);
    }
}
