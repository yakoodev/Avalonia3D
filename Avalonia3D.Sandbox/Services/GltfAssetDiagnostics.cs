using Avalonia3D.Model;
using Avalonia3D.Loaders;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;

namespace Avalonia3D.Sandbox.Services;

public static class GltfAssetDiagnostics
{
    public static void LogAssetStatus(string gltfPath)
    {
        if (string.IsNullOrWhiteSpace(gltfPath))
        {
            Log.Warning("GLTF diagnostics skipped: path is empty.");
            return;
        }

        if (!File.Exists(gltfPath))
        {
            Log.Warning("GLTF diagnostics: file not found: {Path}", gltfPath);
            return;
        }

        var baseDir = Path.GetDirectoryName(gltfPath) ?? string.Empty;
        var dependencies = GltfDependencyInspector.ReadExternalUris(gltfPath);

        if (dependencies.Count == 0)
        {
            Log.Information("GLTF diagnostics: {File} has no external buffer/image dependencies.", Path.GetFileName(gltfPath));
            return;
        }

        var missing = new List<string>();
        foreach (var uri in dependencies)
        {
            var fullPath = Path.GetFullPath(Path.Combine(baseDir, uri));
            if (!File.Exists(fullPath))
            {
                missing.Add(uri);
            }
        }

        Log.Information(
            "GLTF diagnostics: {File}. External dependencies: {Total}, missing: {MissingCount}",
            Path.GetFileName(gltfPath),
            dependencies.Count,
            missing.Count);

        foreach (var uri in missing)
        {
            Log.Warning("GLTF dependency missing: {Uri} (referenced by {File})", uri, Path.GetFileName(gltfPath));
        }
    }

    public static void LogNodeIdConflicts(SceneGraph graph, string sceneLabel)
    {
        if (graph == null)
        {
            return;
        }

        LogDuplicateIds(graph, sceneLabel, node => node.SemanticId, "semantic");
        LogDuplicateIds(graph, sceneLabel, node => node.StableId, "stable");
        LogDuplicateIds(graph, sceneLabel, node => node.ExternalId, "external");
    }

    private static void LogDuplicateIds(SceneGraph graph, string sceneLabel, Func<SceneNode, string?> selector, string idKind)
    {
        var map = new Dictionary<string, List<SceneNode>>(StringComparer.Ordinal);
        CollectIds(graph.Root, selector, map);

        foreach (var pair in map)
        {
            if (pair.Value.Count < 2)
            {
                continue;
            }

            var nodes = string.Join(", ", pair.Value.ConvertAll(n => n.GetPath()));
            Log.Warning(
                "GLTF ID conflict in {Scene}. Kind={IdKind}, Id={Id}, Nodes=[{Nodes}]",
                sceneLabel,
                idKind,
                pair.Key,
                nodes);
        }
    }

    private static void CollectIds(SceneNode node, Func<SceneNode, string?> selector, Dictionary<string, List<SceneNode>> map)
    {
        var id = selector(node);
        if (!string.IsNullOrWhiteSpace(id))
        {
            if (!map.TryGetValue(id, out var nodes))
            {
                nodes = new List<SceneNode>();
                map[id] = nodes;
            }

            nodes.Add(node);
        }

        foreach (var child in node.Children)
        {
            CollectIds(child, selector, map);
        }
    }

}
