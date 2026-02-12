using Avalonia3D.Lights;
using Avalonia3D.Model;
using Avalonia3D.Sandbox.Services;
using System.IO;
using System.Numerics;

namespace Avalonia3D.Sandbox.Scenes;

public sealed class SimpleScene : ISandboxScene
{
    public string Id => "simple";
    public string Title => "Простейшая сцена";
    public string Description => "Один меш и одиночный источник света.";

    public void Load(Scene3D scene, string assetsRoot)
    {
        var path = Path.Combine(assetsRoot, "SimpleScene.gltf");
        scene.LoadScene(path);

        scene.Lights.Add(new Light
        {
            Position = new Vector3(0f, 6f, 8f),
            Color = new Vector3(1f, 1f, 1f),
            Intensity = 1.2f
        });

        GltfAssetDiagnostics.WriteCompactModelReport(path, scene);
    }
}
