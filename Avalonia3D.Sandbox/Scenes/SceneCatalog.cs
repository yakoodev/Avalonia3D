using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Avalonia3D.Sandbox.Scenes;

public static class SceneCatalog
{
    public static IReadOnlyList<ISandboxScene> CreateDefault(string assetsRoot)
    {
        var scenes = new List<ISandboxScene>
        {
            new SimpleScene(),
            new HierarchyScene(),
            new PbrScene(),
            new VehicleScene()
        };

        foreach (var scene in DiscoverGltfScenes(assetsRoot))
        {
            scenes.Add(scene);
        }

        Log.Information("Scene catalog initialized. Total scenes: {Count}", scenes.Count);
        return scenes;
    }

    private static IEnumerable<ISandboxScene> DiscoverGltfScenes(string assetsRoot)
    {
        if (string.IsNullOrWhiteSpace(assetsRoot) || !Directory.Exists(assetsRoot))
        {
            yield break;
        }

        var excludedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "SimpleScene.gltf",
            "HierarchyScene.gltf",
            "PbrScene.gltf",
            "scene.gltf"
        };

        foreach (var path in Directory.EnumerateFiles(assetsRoot, "*.gltf", SearchOption.TopDirectoryOnly)
                     .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase))
        {
            var fileName = Path.GetFileName(path);
            if (excludedNames.Contains(fileName))
            {
                continue;
            }

            yield return new GltfFileScene(fileName);
        }
    }
}
