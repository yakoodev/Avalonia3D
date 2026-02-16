using Avalonia3D.Lights;
using Avalonia3D.Loaders;
using Avalonia3D.Loaders.Policies;
using Avalonia3D.Model;
using Avalonia3D.Model.StandObjects;
using Avalonia3D.Sandbox.Services;
using Serilog;
using System;
using System.IO;
using System.Numerics;
using SharpGLTF.Schema2;

namespace Avalonia3D.Sandbox.Scenes;

public sealed class GltfFileScene : ISandboxScene, ISceneAssetCacheKeyProvider, ISceneBackgroundPreparation
{
    private readonly string _relativePath;

    public GltfFileScene(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
            throw new ArgumentException("Model relative path is required.", nameof(relativePath));

        _relativePath = relativePath;
        FileName = Path.GetFileName(relativePath);
        Extension = Path.GetExtension(relativePath).ToLowerInvariant();
        Directory = NormalizeDirectory(Path.GetDirectoryName(relativePath));

        var shortName = Path.GetFileNameWithoutExtension(relativePath);
        var normalizedPath = relativePath.Replace(Path.DirectorySeparatorChar, '/').Replace(Path.AltDirectorySeparatorChar, '/');
        var normalizedId = normalizedPath
            .Replace(".gltf", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace(".glb", string.Empty, StringComparison.OrdinalIgnoreCase)
            .ToLowerInvariant();

        Id = $"gltf:{normalizedId}";
        Title = $"Модель: {shortName}";
        Description = $"Авто-сцена для файла {normalizedPath} из Assets/TestScenes.";
    }

    public string Id { get; }
    public string Title { get; }
    public string Description { get; }
    public string FileName { get; }
    public string Directory { get; }
    public string Extension { get; }
    public TimeSpan? CacheTtl => TimeSpan.FromMinutes(30);

    public string BuildCacheKey(string assetsRoot)
    {
        var path = Path.Combine(assetsRoot, _relativePath);
        return ImportCacheKeyBuilder.Build(path);
    }


    public object Prepare(string assetsRoot)
    {
        var path = Path.Combine(assetsRoot, _relativePath);
        var cacheKey = ImportCacheKeyBuilder.Build(path);
        var importCache = CacheCoordinator.Current.SceneImportResultCache;

        if (importCache.TryGet(cacheKey, out var cachedResult))
        {
            Log.Information("Scene import result cache hit: {Path}", path);
            return new PreparedGltfPayload(path, cachedResult);
        }

        var modelRoot = ModelRoot.Load(path);
        var importer = new GltfSceneImporter
        {
            ValidationPolicy = ImportValidationConfiguration.CurrentPolicy
        };

        var importResult = importer.ImportWithAnimations(modelRoot);
        var intermediatePayload = modelRoot.WriteGLB();
        importCache.Set(cacheKey, importResult, intermediatePayload);
        Log.Information("Scene import result cache miss: {Path}", path);
        return new PreparedGltfPayload(path, importResult);
    }

    public void LoadPrepared(Scene3D scene, string assetsRoot, object preparedPayload)
    {
        if (preparedPayload is not PreparedGltfPayload payload)
        {
            Load(scene, assetsRoot);
            return;
        }

        Log.Information("Loading prepared GLTF scene from: {Path}", payload.Path);
        GltfAssetDiagnostics.LogAssetStatus(payload.Path);
        scene.LoadPrepared(payload.ImportResult);
        ApplySceneVisibilityFixes(scene, payload.Path);
        GltfAssetDiagnostics.LogNodeIdConflicts(scene.SceneGraph, Path.GetFileName(payload.Path));
        GltfAssetDiagnostics.LogAnimationChannelKinds(scene.LastImportReport, Path.GetFileName(payload.Path));

        if (scene.Lights.Count == 0)
        {
            scene.Lights.Add(new Light
            {
                Position = new Vector3(0f, 8f, 10f),
                Color = new Vector3(1f, 1f, 1f),
                Intensity = 1.0f
            });

            scene.Lights.Add(new Light
            {
                Position = new Vector3(-8f, 5f, -6f),
                Color = new Vector3(0.8f, 0.9f, 1f),
                Intensity = 0.65f
            });
        }
    }
    private static string NormalizeDirectory(string? directory)
    {
        if (string.IsNullOrWhiteSpace(directory))
        {
            return "/";
        }

        return directory.Replace(Path.DirectorySeparatorChar, '/').Replace(Path.AltDirectorySeparatorChar, '/');
    }

    public void Load(Scene3D scene, string assetsRoot)
    {
        var path = Path.Combine(assetsRoot, _relativePath);
        Log.Information("Loading auto-discovered GLTF scene from: {Path}", path);
        GltfAssetDiagnostics.LogAssetStatus(path);
        scene.LoadScene(path);
        ApplySceneVisibilityFixes(scene, path);
        GltfAssetDiagnostics.LogNodeIdConflicts(scene.SceneGraph, Path.GetFileName(path));
        GltfAssetDiagnostics.LogAnimationChannelKinds(scene.LastImportReport, Path.GetFileName(path));

        if (scene.Lights.Count == 0)
        {
            // Единая дефолтная схема света для внешних моделей.
            scene.Lights.Add(new Light
            {
                Position = new Vector3(0f, 8f, 10f),
                Color = new Vector3(1f, 1f, 1f),
                Intensity = 1.0f
            });

            scene.Lights.Add(new Light
            {
                Position = new Vector3(-8f, 5f, -6f),
                Color = new Vector3(0.8f, 0.9f, 1f),
                Intensity = 0.65f
            });
        }
    }

    private sealed record PreparedGltfPayload(string Path, SceneImportResult ImportResult);

    private static void ApplySceneVisibilityFixes(Scene3D scene, string scenePath)
    {
        if (scene == null || string.IsNullOrWhiteSpace(scenePath))
        {
            return;
        }

        var normalizedPath = scenePath.Replace('\\', '/');
        if (!normalizedPath.EndsWith("/droid/scene.gltf", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var hiddenCount = 0;
        foreach (var root in scene.SceneGraph.RootObjects)
        {
            hiddenCount += HideByNameRecursive(root);
        }

        if (hiddenCount > 0)
        {
            Log.Information("Applied droid visibility fix. Hidden scene objects: {HiddenCount}", hiddenCount);
        }
    }

    private static int HideByNameRecursive(SceneObject? sceneObject)
    {
        if (sceneObject == null)
        {
            return 0;
        }

        var hidden = 0;
        if (ShouldHideNode(sceneObject.Name))
        {
            sceneObject.IsVisible = false;
            hidden++;
        }

        if (sceneObject is MeshGroup group)
        {
            foreach (var child in group)
            {
                hidden += HideByNameRecursive(child);
            }
        }

        return hidden;
    }

    private static bool ShouldHideNode(string? nodeName)
    {
        if (string.IsNullOrWhiteSpace(nodeName))
        {
            return false;
        }

        return string.Equals(nodeName, "Env", StringComparison.OrdinalIgnoreCase)
            || string.Equals(nodeName, "Scheibe", StringComparison.OrdinalIgnoreCase)
            || string.Equals(nodeName, "Scheibe_Boden_0", StringComparison.OrdinalIgnoreCase)
            || string.Equals(nodeName, "Himmel", StringComparison.OrdinalIgnoreCase);
    }
}
