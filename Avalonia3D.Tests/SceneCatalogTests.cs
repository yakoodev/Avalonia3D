using Avalonia3D.Sandbox.Scenes;
using System;
using System.IO;
using System.Linq;
using Xunit;

namespace Avalonia3D.Tests;

public class SceneCatalogTests
{
    [Fact]
    public void CreateDefault_DiscoversGltfAndGlbScenes()
    {
        var root = Path.Combine(Path.GetTempPath(), "avalonia3d-scene-catalog-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

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
            Assert.Contains("pbr-regression", ids);
            Assert.Contains("gltf:nested/wheel", ids);
            Assert.DoesNotContain(ids, id => id.Contains("ignore", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }
}
