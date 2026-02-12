using Avalonia3D.Lights;
using Avalonia3D.Model;
using Avalonia3D.Sandbox.Services;
using System.IO;
using System.Numerics;

namespace Avalonia3D.Sandbox.Scenes;

public sealed class PbrScene : ISandboxScene
{
    public string Id => "pbr";
    public string Title => "PBR + несколько источников";
    public string Description => "Два материала с разной металличностью и несколькими источниками света.";

    public void Load(Scene3D scene, string assetsRoot)
    {
        var path = Path.Combine(assetsRoot, "PbrScene.gltf");
        scene.LoadScene(path);

        scene.Lights.Add(new Light
        {
            Position = new Vector3(0f, 7f, 7f),
            Color = new Vector3(1f, 1f, 1f),
            Intensity = 0.9f
        });

        scene.Lights.Add(new Light
        {
            Position = new Vector3(-6f, 4f, -4f),
            Color = new Vector3(0.6f, 0.8f, 1f),
            Intensity = 0.7f
        });

        scene.Lights.Add(new Light
        {
            Position = new Vector3(6f, 3f, -2f),
            Color = new Vector3(1f, 0.7f, 0.6f),
            Intensity = 0.6f
        });

        GltfAssetDiagnostics.WriteCompactModelReport(path, scene);
    }
}
