using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

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
        var dependencies = ReadExternalUris(gltfPath);

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

    private static List<string> ReadExternalUris(string gltfPath)
    {
        var result = new List<string>();
        try
        {
            using var stream = File.OpenRead(gltfPath);
            using var doc = JsonDocument.Parse(stream);

            CollectUris(doc.RootElement, "buffers", result);
            CollectUris(doc.RootElement, "images", result);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to parse GLTF dependencies: {File}", Path.GetFileName(gltfPath));
        }

        return result;
    }

    private static void CollectUris(JsonElement root, string propertyName, List<string> target)
    {
        if (!root.TryGetProperty(propertyName, out var list) || list.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        foreach (var item in list.EnumerateArray())
        {
            if (!item.TryGetProperty("uri", out var uriProp) || uriProp.ValueKind != JsonValueKind.String)
            {
                continue;
            }

            var uri = uriProp.GetString();
            if (string.IsNullOrWhiteSpace(uri))
            {
                continue;
            }

            if (uri.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            target.Add(uri);
        }
    }
}
