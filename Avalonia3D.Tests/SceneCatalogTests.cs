using Avalonia3D.Sandbox.Scenes;
using System;
using System.IO;
using System.Linq;
using Xunit;

namespace Avalonia3D.Tests;

public class SceneCatalogTests
{
    [Fact]
    public void CreateDefault_ReadsManifest_WhenPresent()
    {
        var root = CreateTempRoot();

        try
        {
            File.WriteAllText(Path.Combine(root, "vehicle.gltf"), "{}");
            File.WriteAllText(Path.Combine(root, SceneCatalog.ManifestFileName),
                """
                {
                  "models": [
                    {
                      "id": "manifest:vehicle",
                      "title": "Vehicle from manifest",
                      "description": "manifest wins",
                      "relativePath": "vehicle.gltf",
                      "group": "from-manifest",
                      "tags": ["tag-a", "tag-b"],
                      "preload": { "metadata": true, "binary": false, "cache": true, "cacheMinutes": 15 }
                    }
                  ]
                }
                """);

            var scenes = SceneCatalog.CreateDefault(root);
            var scene = Assert.Single(scenes);

            Assert.Equal("manifest:vehicle", scene.Id);
            var metadata = Assert.IsAssignableFrom<ISceneCatalogMetadata>(scene);
            Assert.Equal("from-manifest", metadata.Group);
            Assert.Contains("tag-a", metadata.Tags);
            Assert.Equal("vehicle.gltf", metadata.RelativePath);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void CreateDefault_FallsBackToDiskScan_WhenManifestMissing()
    {
        var root = CreateTempRoot();

        try
        {
            var nested = Path.Combine(root, "nested");
            Directory.CreateDirectory(nested);

            File.WriteAllText(Path.Combine(root, "vehicle.gltf"), "{}");
            File.WriteAllText(Path.Combine(nested, "wheel.glb"), "glb");
            File.WriteAllText(Path.Combine(root, "ignore.obj"), "obj");

            var scenes = SceneCatalog.CreateDefault(root);
            var ids = scenes.Select(s => s.Id).ToArray();

            Assert.Contains("gltf:vehicle", ids);
            Assert.Contains("gltf:nested/wheel", ids);
            Assert.DoesNotContain(ids, id => id.Contains("ignore", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void CreateDefault_UsesStableSorting_ForManifestEntries()
    {
        var root = CreateTempRoot();

        try
        {
            File.WriteAllText(Path.Combine(root, "a.gltf"), "{}");
            File.WriteAllText(Path.Combine(root, "b.gltf"), "{}");
            File.WriteAllText(Path.Combine(root, "c.gltf"), "{}");

            File.WriteAllText(Path.Combine(root, SceneCatalog.ManifestFileName),
                """
                {
                  "models": [
                    { "id": "m2", "title": "Beta", "description": "", "relativePath": "b.gltf", "group": "g" },
                    { "id": "m1", "title": "Alpha", "description": "", "relativePath": "a.gltf", "group": "g" },
                    { "id": "m3", "title": "Alpha", "description": "", "relativePath": "c.gltf", "group": "g" }
                  ]
                }
                """);

            var scenes = SceneCatalog.CreateDefault(root).ToArray();

            Assert.Equal(new[] { "m1", "m3", "m2" }, scenes.Select(x => x.Id).ToArray());
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    private static string CreateTempRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "avalonia3d-scene-catalog-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }
}
