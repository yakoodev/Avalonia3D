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
         
        };

        //scenes.AddRange(CreateDedicatedPbrRegressionCases(assetsRoot));
        scenes.AddRange(DiscoverGltfScenes(assetsRoot));

        Log.Information("Scene catalog initialized. Total scenes: {Count}", scenes.Count);
        return scenes;
    }



    private static IEnumerable<ISandboxScene> DiscoverGltfScenes(string assetsRoot)
    {
        if (string.IsNullOrWhiteSpace(assetsRoot) || !Directory.Exists(assetsRoot))
        {
            yield break;
        }

        var modelFiles = Directory.EnumerateFiles(assetsRoot, "*.*", SearchOption.AllDirectories)
            .Where(path =>
            {
                var ext = Path.GetExtension(path);
                return ext.Equals(".gltf", StringComparison.OrdinalIgnoreCase) || ext.Equals(".glb", StringComparison.OrdinalIgnoreCase);
            })
            .OrderBy(path => Path.GetRelativePath(assetsRoot, path), StringComparer.OrdinalIgnoreCase);

        foreach (var path in modelFiles)
        {
            var relativePath = Path.GetRelativePath(assetsRoot, path);
            yield return new GltfFileScene(relativePath);
        }
    }
}
