using Avalonia3D.Lights;
using Avalonia3D.Model;
using Avalonia3D.Sandbox.Services;
using Serilog;
using System;
using System.IO;
using System.Numerics;

namespace Avalonia3D.Sandbox.Scenes;

public sealed class GltfFileScene : ISandboxScene
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

        GltfAssetDiagnostics.WriteCompactModelReport(path, scene);
    }
}
