using Avalonia3D.Lights;
using Avalonia3D.Model;
using Avalonia3D.Sandbox.Services;
using Serilog;
using System.IO;
using System.Numerics;

namespace Avalonia3D.Sandbox.Scenes;

public sealed class VehicleScene : ISandboxScene
{
    public string Id => "vehicle";
    public string Title => "Новая модель (scene.gltf)";
    public string Description => "Загрузка добавленной модели scene.gltf с текстурами.";

    public void Load(Scene3D scene, string assetsRoot)
    {
        var path = Path.Combine(assetsRoot, "scene.gltf");
        Log.Information("Loading custom scene from: {Path}", path);
        GltfAssetDiagnostics.LogAssetStatus(path);
        scene.LoadScene(path);
        GltfAssetDiagnostics.LogNodeIdConflicts(scene.SceneGraph, Path.GetFileName(path));
        GltfAssetDiagnostics.LogAnimationChannelKinds(scene.LastImportReport, Path.GetFileName(path));

        scene.Lights.Add(new Light
        {
            Position = new Vector3(0f, 5f, 8f),
            Color = new Vector3(1f, 1f, 1f),
            Intensity = 1.1f
        });

        scene.Lights.Add(new Light
        {
            Position = new Vector3(-5f, 4f, -3f),
            Color = new Vector3(0.8f, 0.9f, 1f),
            Intensity = 0.7f
        });

        GltfAssetDiagnostics.WriteCompactModelReport(path, scene);
    }
}
