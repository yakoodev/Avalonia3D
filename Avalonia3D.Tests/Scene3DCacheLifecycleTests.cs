using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Avalonia3D.Loaders;
using Avalonia3D.Model;
using Xunit;

namespace Avalonia3D.Tests;

[Trait("TestTarget", "DomainLogic")]
public sealed class Scene3DCacheLifecycleTests
{
    [Fact]
    public void LoadScene_WhenLoadingSameAssetTwice_DoesNotClearGlobalModelLoaderCaches()
    {
        ModelLoader.ClearAllCaches();
        var cache = GetMaterialIndexMapCache();
        cache["sentinel-entry"] = new Dictionary<(int MeshIndex, int PrimitiveIndex), int>();

        var scene = new Scene3D();
        var modelPath = ResolveSandboxBoxScenePath();

        scene.LoadScene(modelPath);
        scene.LoadScene(modelPath);

        Assert.True(cache.ContainsKey("sentinel-entry"));

        ModelLoader.ClearAllCaches();
    }

    private static Dictionary<string, Dictionary<(int MeshIndex, int PrimitiveIndex), int>> GetMaterialIndexMapCache()
    {
        var field = typeof(ModelLoader).GetField("_materialIndexMapCache", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(field);

        var cache = field!.GetValue(null) as Dictionary<string, Dictionary<(int MeshIndex, int PrimitiveIndex), int>>;
        Assert.NotNull(cache);
        return cache!;
    }

    private static string ResolveSandboxBoxScenePath()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            var candidate = Path.Combine(directory.FullName, "Avalonia3D.Sandbox", "Assets", "TestScenes", "Box.gltf");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException("Не удалось найти тестовую модель Box.gltf.");
    }
}
