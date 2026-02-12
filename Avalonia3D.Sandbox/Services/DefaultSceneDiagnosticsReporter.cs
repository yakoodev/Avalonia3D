using Avalonia3D.Model;
using Avalonia3D.Model.StandObjects;
using Avalonia3D.Sandbox.Scenes;
using Serilog;
using System.Collections.Generic;
using System.Linq;

namespace Avalonia3D.Sandbox.Services;

public sealed class DefaultSceneDiagnosticsReporter : ISceneDiagnosticsReporter
{
    public void Report(Scene3D scene3D, ISandboxScene sceneInfo)
    {
        var roots = scene3D.SceneGraph.RootObjects;
        var meshes = CollectMeshes(roots);
        var transparent = meshes.Count(m => m.Material?.IsTransparent == true || m.Opacity < 1f);
        var sourceBlendMaterials = meshes.Count(m => m.Material?.SourceAlphaMode == MaterialAlphaMode.Blend);
        var opaque = meshes.Count - transparent;

        var hasBounds = SceneCameraFramer.TryComputeWorldBounds(scene3D.SceneGraph, out var min, out var max);
        var boundsText = hasBounds
            ? $"Min={min}, Max={max}, Size={max - min}"
            : "no-geometry-bounds";

        Log.Information(
            "Scene diagnostics: {SceneId}. Roots={Roots}, Meshes={Meshes} (opaque={Opaque}, transparent={Transparent}, sourceAlphaBlend={SourceAlphaBlend}), Lights={Lights}, CameraPos={CameraPos}, CameraTarget={CameraTarget}, Bounds={Bounds}",
            sceneInfo.Id,
            roots.Count,
            meshes.Count,
            opaque,
            transparent,
            sourceBlendMaterials,
            scene3D.Lights.Count,
            scene3D.Camera.Position,
            scene3D.Camera.Target,
            boundsText);

        if (scene3D.Lights.Count == 0)
        {
            Log.Warning("Scene {SceneId} has zero lights. Objects can appear black depending on material/shader path.", sceneInfo.Id);
        }
    }

    private static List<MeshObject> CollectMeshes(IReadOnlyList<SceneObject> roots)
    {
        var result = new List<MeshObject>();
        foreach (var root in roots)
        {
            CollectMeshesRecursive(root, result);
        }

        return result;
    }

    private static void CollectMeshesRecursive(SceneObject node, List<MeshObject> result)
    {
        if (node is MeshObject mesh)
        {
            result.Add(mesh);
            return;
        }

        if (node is MeshGroup group)
        {
            foreach (var child in group)
            {
                CollectMeshesRecursive(child, result);
            }
        }
    }
}
