using Avalonia3D.Loaders;
using Avalonia3D.Model;
using Avalonia3D.Model.StandObjects;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

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
        var preflight = GltfDependencyInspector.ReadPreflight(gltfPath);
        var dependencies = preflight.ExternalUris;

        foreach (var warning in preflight.Warnings)
        {
            Log.Debug("GLTF preflight warning for {File}: {Warning}", Path.GetFileName(gltfPath), warning);
        }

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

    public static string WriteCompactModelReport(string gltfPath, Scene3D scene)
    {
        if (string.IsNullOrWhiteSpace(gltfPath) || scene == null)
        {
            return string.Empty;
        }

        var fullPath = Path.GetFullPath(gltfPath);
        var preflight = GltfDependencyInspector.ReadPreflight(fullPath);
        var dependencies = preflight.ExternalUris;
        var missingDependencies = CountMissingDependencies(fullPath, dependencies);

        var meshes = CollectMeshes(scene.SceneGraph).ToArray();
        var materials = meshes
            .Select(mesh => mesh.Material)
            .Where(material => material != null)
            .Cast<Material>()
            .ToArray();

        var meshesWithMaterial = meshes.Count(mesh => mesh.Material != null);
        var meshesWithoutMaterial = meshes.Length - meshesWithMaterial;
        var emissiveTextureMaterials = materials.Count(material => material.EmissiveTexture != null);
        var maxEmissiveFactor = materials.Length == 0
            ? 0f
            : materials.Max(material => material.EmissiveFactor.Length());
        var maxEmissiveIntensity = materials.Length == 0
            ? 0f
            : materials.Max(material => material.EmissiveIntensity);

        var reportDirectory = Path.Combine(AppContext.BaseDirectory, "ModelDiagnostics");
        Directory.CreateDirectory(reportDirectory);

        var reportPath = Path.Combine(reportDirectory, BuildReportFileName(fullPath));

        var issuesPreview = scene.LastImportReport.Issues.Take(3).ToArray();
        var report = new StringBuilder(512)
            .AppendLine($"file={Path.GetFileName(fullPath)}")
            .AppendLine($"fullPath={fullPath}")
            .AppendLine($"importStatus={scene.LastImportReport.Status}")
            .AppendLine($"importIssues={scene.LastImportReport.Issues.Count}")
            .AppendLine($"objectsRoot={scene.SceneGraph.RootObjects.Count}")
            .AppendLine($"objectsMesh={meshes.Length}")
            .AppendLine($"meshesWithMaterial={meshesWithMaterial}")
            .AppendLine($"meshesWithoutMaterial={meshesWithoutMaterial}")
            .AppendLine($"lights={scene.Lights.Count}")
            .AppendLine($"materials={materials.Length}")
            .AppendLine($"materialsWithEmissiveTexture={emissiveTextureMaterials}")
            .AppendLine($"maxEmissiveFactorLength={maxEmissiveFactor:0.###}")
            .AppendLine($"maxEmissiveIntensity={maxEmissiveIntensity:0.###}")
            .AppendLine($"depsExternal={dependencies.Count}")
            .AppendLine($"depsMissing={missingDependencies}")
            .AppendLine($"renderMode={scene.RenderMode}")
            .AppendLine($"pbrDebugView={scene.PbrDebugViewMode}")
            .AppendLine($"backgroundRgb={scene.ActiveGraphicsProfile.Background.Red:0.###},{scene.ActiveGraphicsProfile.Background.Green:0.###},{scene.ActiveGraphicsProfile.Background.Blue:0.###}")
            .AppendLine($"timeUtc={DateTime.UtcNow:O}");

        if (issuesPreview.Length > 0)
        {
            report.AppendLine($"issuesPreview={string.Join(" | ", issuesPreview)}");
        }

        var materialPreview = meshes
            .Take(3)
            .Select(mesh => !string.IsNullOrWhiteSpace(mesh.MaterialKey) ? mesh.MaterialKey : (mesh.Material != null ? "<material-without-key>" : "<null>"))
            .ToArray();

        report.AppendLine($"materialPreview={string.Join(",", materialPreview)}");

        if (meshesWithoutMaterial > 0)
        {
            Log.Warning("Compact model report: {WithoutMaterial} mesh(es) have no Material. File={File}", meshesWithoutMaterial, Path.GetFileName(fullPath));
        }

        File.WriteAllText(reportPath, report.ToString());
        Log.Information("Compact model report written: {Path}", reportPath);
        return reportPath;
    }


    private static IEnumerable<MeshObject> CollectMeshes(SceneGraph graph)
    {
        foreach (var root in graph.RootObjects)
        {
            foreach (var mesh in CollectMeshesRecursive(root))
            {
                yield return mesh;
            }
        }
    }

    private static IEnumerable<MeshObject> CollectMeshesRecursive(SceneObject node)
    {
        if (node is MeshObject mesh)
        {
            yield return mesh;
            yield break;
        }

        if (node is MeshGroup group)
        {
            foreach (var child in group)
            {
                foreach (var childMesh in CollectMeshesRecursive(child))
                {
                    yield return childMesh;
                }
            }
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

    public static void LogAnimationChannelKinds(SceneImportReport report, string sceneLabel)
    {
        if (report.AnimationChannelKinds.Count == 0)
        {
            return;
        }

        var groupedByClip = report.AnimationChannelKinds
            .GroupBy(summary => summary.ClipName, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal);

        foreach (var clipGroup in groupedByClip)
        {
            var clipSummary = string.Join(", ",
                clipGroup
                    .OrderBy(item => item.Kind)
                    .Select(item => $"{item.Kind}={item.ChannelCount}"));

            Log.Information(
                "GLTF animation channel kinds in {Scene}. Clip={Clip}, Summary=[{Summary}]",
                sceneLabel,
                clipGroup.Key,
                clipSummary);
        }
    }

    private static int CountMissingDependencies(string gltfPath, IReadOnlyList<string> dependencies)
    {
        var baseDir = Path.GetDirectoryName(gltfPath) ?? string.Empty;
        var missing = 0;
        foreach (var uri in dependencies)
        {
            var fullPath = Path.GetFullPath(Path.Combine(baseDir, uri));
            if (!File.Exists(fullPath))
            {
                missing++;
            }
        }

        return missing;
    }

    private static string BuildReportFileName(string fullPath)
    {
        var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(fullPath);
        var safeFileName = string.Concat(fileNameWithoutExtension.Select(ch => char.IsLetterOrDigit(ch) ? ch : '_'));

        var hash = Convert.ToHexString(SHA1.HashData(Encoding.UTF8.GetBytes(fullPath))).ToLowerInvariant();
        return $"{safeFileName}_{hash[..8]}.log";
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
