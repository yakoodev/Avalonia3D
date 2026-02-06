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
        var scenes = DiscoverGltfScenes(assetsRoot).ToList();

        Log.Information("Scene catalog initialized. Total scenes: {Count}", scenes.Count);
        return scenes;
    }

    private static IEnumerable<ISandboxScene> DiscoverGltfScenes(string assetsRoot)
    {
        if (string.IsNullOrWhiteSpace(assetsRoot) || !Directory.Exists(assetsRoot))
        {
            yield break;
        }

        foreach (var path in Directory.EnumerateFiles(assetsRoot, "*.gltf", SearchOption.AllDirectories)
                     .OrderBy(path => Path.GetRelativePath(assetsRoot, path), StringComparer.OrdinalIgnoreCase))
        {
            var relativePath = Path.GetRelativePath(assetsRoot, path);
            yield return new GltfFileScene(relativePath);
        }
    }
}
