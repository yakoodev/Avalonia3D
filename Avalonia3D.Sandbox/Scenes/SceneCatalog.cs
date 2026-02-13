using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Avalonia3D.Sandbox.Scenes;

public static class SceneCatalog
{
    public const string ManifestFileName = "models.catalog.json";

    public static IReadOnlyList<ISandboxScene> CreateDefault(string assetsRoot)
    {
        var scenes = new List<ISandboxScene>();

        var manifestPath = Path.Combine(assetsRoot, ManifestFileName);
        if (File.Exists(manifestPath))
        {
            scenes.AddRange(DiscoverFromManifest(manifestPath));
            Log.Information("Scene catalog initialized from manifest {ManifestPath}. Total scenes: {Count}", manifestPath, scenes.Count);
            return scenes;
        }

        scenes.AddRange(DiscoverByDiskScan(assetsRoot));
        Log.Information("Scene catalog initialized by disk scan fallback. Total scenes: {Count}", scenes.Count);
        return scenes;
    }

    private static IEnumerable<ISandboxScene> DiscoverFromManifest(string manifestPath)
    {
        SceneManifestRoot? manifest;

        try
        {
            var json = File.ReadAllText(manifestPath);
            manifest = JsonSerializer.Deserialize<SceneManifestRoot>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to read scene manifest: {ManifestPath}", manifestPath);
            yield break;
        }

        if (manifest?.Models is null)
        {
            yield break;
        }

        foreach (var entry in StableSort(manifest.Models))
        {
            if (string.IsNullOrWhiteSpace(entry.RelativePath))
            {
                continue;
            }

            yield return LazyGltfFileScene.FromDescriptor(entry.ToDescriptor());
        }
    }

    private static IEnumerable<ISandboxScene> DiscoverByDiskScan(string assetsRoot)
    {
        if (string.IsNullOrWhiteSpace(assetsRoot) || !Directory.Exists(assetsRoot))
        {
            yield break;
        }

        var discovered = Directory.EnumerateFiles(assetsRoot, "*.*", SearchOption.AllDirectories)
            .Where(path =>
            {
                var ext = Path.GetExtension(path);
                return ext.Equals(".gltf", StringComparison.OrdinalIgnoreCase) || ext.Equals(".glb", StringComparison.OrdinalIgnoreCase);
            })
            .Select(path => SceneManifestModel.FromPath(Path.GetRelativePath(assetsRoot, path)))
            .ToArray();

        foreach (var model in StableSort(discovered))
        {
            yield return LazyGltfFileScene.FromDescriptor(model.ToDescriptor());
        }
    }

    private static IEnumerable<SceneManifestModel> StableSort(IEnumerable<SceneManifestModel> models)
    {
        return models
            .Select((model, index) => (model, index))
            .OrderBy(static item => item.model.Group ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static item => item.model.Title ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static item => item.model.Id ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static item => item.index)
            .Select(static item => item.model);
    }
}

public sealed record SceneDescriptor(
    string Id,
    string Title,
    string Description,
    string RelativePath,
    string Group,
    IReadOnlyList<string> Tags,
    ScenePreloadHints PreloadHints,
    string FileName,
    string Directory,
    string Extension);

public sealed record ScenePreloadHints(bool Metadata = true, bool Binary = false, bool Cache = true, int? CacheMinutes = 30);

public sealed class SceneManifestRoot
{
    public List<SceneManifestModel> Models { get; init; } = new();
}

public sealed class SceneManifestModel
{
    public string? Id { get; init; }
    public string? Title { get; init; }
    public string? Description { get; init; }
    public string? RelativePath { get; init; }
    public string? Group { get; init; }
    public List<string>? Tags { get; init; }
    public ScenePreloadHints? Preload { get; init; }

    public SceneDescriptor ToDescriptor()
    {
        var normalizedPath = NormalizePath(RelativePath ?? string.Empty);
        var extension = Path.GetExtension(normalizedPath).ToLowerInvariant();
        var fileName = Path.GetFileName(normalizedPath);
        var directory = GltfFileScene.NormalizeDirectory(Path.GetDirectoryName(normalizedPath));
        var id = string.IsNullOrWhiteSpace(Id)
            ? GltfFileScene.BuildId(normalizedPath)
            : Id!;

        return new SceneDescriptor(
            id,
            string.IsNullOrWhiteSpace(Title) ? $"Модель: {Path.GetFileNameWithoutExtension(normalizedPath)}" : Title!,
            string.IsNullOrWhiteSpace(Description) ? $"Сцена из файла {normalizedPath}." : Description!,
            normalizedPath,
            string.IsNullOrWhiteSpace(Group) ? directory : Group!,
            (Tags ?? new List<string>()).Where(static tag => !string.IsNullOrWhiteSpace(tag)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
            Preload ?? new ScenePreloadHints(),
            fileName,
            directory,
            extension);
    }

    public static SceneManifestModel FromPath(string relativePath)
    {
        var normalizedPath = NormalizePath(relativePath);
        var directory = GltfFileScene.NormalizeDirectory(Path.GetDirectoryName(normalizedPath));

        return new SceneManifestModel
        {
            RelativePath = normalizedPath,
            Id = GltfFileScene.BuildId(normalizedPath),
            Title = $"Модель: {Path.GetFileNameWithoutExtension(normalizedPath)}",
            Description = $"Авто-сцена для файла {normalizedPath} из Assets/TestScenes.",
            Group = directory,
            Tags = new List<string> { Path.GetExtension(normalizedPath).TrimStart('.').ToLowerInvariant(), "auto-discovered" },
            Preload = new ScenePreloadHints()
        };
    }

    private static string NormalizePath(string path)
    {
        return path.Replace(Path.DirectorySeparatorChar, '/').Replace(Path.AltDirectorySeparatorChar, '/');
    }
}
