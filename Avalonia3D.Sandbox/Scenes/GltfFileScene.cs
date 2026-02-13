using Avalonia3D.Lights;
using Avalonia3D.Loaders;
using Avalonia3D.Loaders.Policies;
using Avalonia3D.Model;
using Avalonia3D.Sandbox.Services;
using Serilog;
using SharpGLTF.Schema2;
using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;

namespace Avalonia3D.Sandbox.Scenes;

public interface ISceneCatalogMetadata
{
    string RelativePath { get; }
    string FileName { get; }
    string Directory { get; }
    string Extension { get; }
    string Group { get; }
    IReadOnlyList<string> Tags { get; }
    ScenePreloadHints PreloadHints { get; }
}

public sealed class GltfFileScene : ISandboxScene, ISceneAssetCacheKeyProvider, ISceneBackgroundPreparation, ISceneCatalogMetadata
{
    private readonly string _relativePath;

    public GltfFileScene(string relativePath)
        : this(SceneManifestModel.FromPath(relativePath).ToDescriptor())
    {
    }

    public GltfFileScene(SceneDescriptor descriptor)
    {
        _relativePath = descriptor.RelativePath;
        Id = descriptor.Id;
        Title = descriptor.Title;
        Description = descriptor.Description;
        RelativePath = descriptor.RelativePath;
        FileName = descriptor.FileName;
        Directory = descriptor.Directory;
        Extension = descriptor.Extension;
        Group = descriptor.Group;
        Tags = descriptor.Tags;
        PreloadHints = descriptor.PreloadHints;
    }

    public string Id { get; }
    public string Title { get; }
    public string Description { get; }
    public string RelativePath { get; }
    public string FileName { get; }
    public string Directory { get; }
    public string Extension { get; }
    public string Group { get; }
    public IReadOnlyList<string> Tags { get; }
    public ScenePreloadHints PreloadHints { get; }
    public TimeSpan? CacheTtl => PreloadHints.CacheMinutes.HasValue ? TimeSpan.FromMinutes(PreloadHints.CacheMinutes.Value) : null;

    public string BuildCacheKey(string assetsRoot)
    {
        var fullPath = Path.GetFullPath(Path.Combine(assetsRoot, _relativePath));
        var relative = _relativePath.Replace(Path.DirectorySeparatorChar, '/').Replace(Path.AltDirectorySeparatorChar, '/').ToLowerInvariant();

        if (!File.Exists(fullPath))
        {
            return $"gltf:{relative}:missing";
        }

        var fileInfo = new FileInfo(fullPath);
        return $"gltf:{relative}:ticks={fileInfo.LastWriteTimeUtc.Ticks}:len={fileInfo.Length}";
    }

    public object Prepare(string assetsRoot)
    {
        var path = Path.Combine(assetsRoot, _relativePath);
        var modelRoot = ModelRoot.Load(path);
        var importer = new GltfSceneImporter
        {
            ValidationPolicy = ImportValidationConfiguration.CurrentPolicy
        };

        var importResult = importer.ImportWithAnimations(modelRoot);
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
        GltfAssetDiagnostics.LogNodeIdConflicts(scene.SceneGraph, Path.GetFileName(payload.Path));
        GltfAssetDiagnostics.LogAnimationChannelKinds(scene.LastImportReport, Path.GetFileName(payload.Path));

        EnsureDefaultLights(scene);
    }

    public void Load(Scene3D scene, string assetsRoot)
    {
        var path = Path.Combine(assetsRoot, _relativePath);
        Log.Information("Loading auto-discovered GLTF scene from: {Path}", path);
        GltfAssetDiagnostics.LogAssetStatus(path);
        scene.LoadScene(path);
        GltfAssetDiagnostics.LogNodeIdConflicts(scene.SceneGraph, Path.GetFileName(path));
        GltfAssetDiagnostics.LogAnimationChannelKinds(scene.LastImportReport, Path.GetFileName(path));

        EnsureDefaultLights(scene);
    }

    public static string BuildId(string relativePath)
    {
        var normalizedPath = relativePath.Replace(Path.DirectorySeparatorChar, '/').Replace(Path.AltDirectorySeparatorChar, '/');
        var normalizedId = normalizedPath
            .Replace(".gltf", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace(".glb", string.Empty, StringComparison.OrdinalIgnoreCase)
            .ToLowerInvariant();

        return $"gltf:{normalizedId}";
    }

    public static string NormalizeDirectory(string? directory)
    {
        if (string.IsNullOrWhiteSpace(directory))
        {
            return "/";
        }

        return directory.Replace(Path.DirectorySeparatorChar, '/').Replace(Path.AltDirectorySeparatorChar, '/');
    }

    private static void EnsureDefaultLights(Scene3D scene)
    {
        if (scene.Lights.Count != 0)
        {
            return;
        }

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

    private sealed record PreparedGltfPayload(string Path, SceneImportResult ImportResult);
}

public sealed class LazyGltfFileScene : ISandboxScene, ISceneAssetCacheKeyProvider, ISceneBackgroundPreparation, ISceneCatalogMetadata
{
    private readonly SceneDescriptor _descriptor;
    private readonly Lazy<GltfFileScene> _scene;

    private LazyGltfFileScene(SceneDescriptor descriptor)
    {
        _descriptor = descriptor;
        _scene = new Lazy<GltfFileScene>(() => new GltfFileScene(_descriptor), isThreadSafe: true);
    }

    public static LazyGltfFileScene FromDescriptor(SceneDescriptor descriptor) => new(descriptor);

    public string Id => _descriptor.Id;
    public string Title => _descriptor.Title;
    public string Description => _descriptor.Description;
    public string RelativePath => _descriptor.RelativePath;
    public string FileName => _descriptor.FileName;
    public string Directory => _descriptor.Directory;
    public string Extension => _descriptor.Extension;
    public string Group => _descriptor.Group;
    public IReadOnlyList<string> Tags => _descriptor.Tags;
    public ScenePreloadHints PreloadHints => _descriptor.PreloadHints;
    public TimeSpan? CacheTtl => _descriptor.PreloadHints.CacheMinutes.HasValue ? TimeSpan.FromMinutes(_descriptor.PreloadHints.CacheMinutes.Value) : null;

    public string BuildCacheKey(string assetsRoot) => _scene.Value.BuildCacheKey(assetsRoot);

    public object Prepare(string assetsRoot) => _scene.Value.Prepare(assetsRoot);

    public void LoadPrepared(Scene3D scene, string assetsRoot, object preparedPayload) => _scene.Value.LoadPrepared(scene, assetsRoot, preparedPayload);

    public void Load(Scene3D scene, string assetsRoot) => _scene.Value.Load(scene, assetsRoot);
}
