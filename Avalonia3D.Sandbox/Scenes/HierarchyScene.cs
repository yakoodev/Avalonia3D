using Avalonia3D.Lights;
using Avalonia3D.Model;
using Avalonia3D.Sandbox.Services;
using System.IO;
using System.Numerics;

namespace Avalonia3D.Sandbox.Scenes;

public sealed class HierarchyScene : ISandboxScene
{
    public string Id => "hierarchy";
    public string Title => "Иерархия узлов";
    public string Description => "Дверь с дочерней ручкой, чтобы проверить вложенные трансформации.";

    public void Load(Scene3D scene, string assetsRoot)
    {
        var path = Path.Combine(assetsRoot, "HierarchyScene.gltf");
        scene.LoadScene(path);

        scene.Lights.Add(new Light
        {
            Position = new Vector3(-4f, 8f, 6f),
            Color = new Vector3(1f, 0.95f, 0.9f),
            Intensity = 1.1f
        });

        GltfAssetDiagnostics.WriteCompactModelReport(path, scene);
    }
}
