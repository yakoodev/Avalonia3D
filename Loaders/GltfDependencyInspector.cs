using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Avalonia3D.Loaders;

public static class GltfDependencyInspector
{
    public static IReadOnlyList<string> GetMissingDependencies(string gltfPath)
    {
        if (string.IsNullOrWhiteSpace(gltfPath) || !File.Exists(gltfPath))
        {
            return [];
        }

        var baseDir = Path.GetDirectoryName(gltfPath) ?? string.Empty;
        var dependencies = ReadExternalUris(gltfPath);
        var missing = new List<string>();

        foreach (var uri in dependencies)
        {
            var fullPath = Path.GetFullPath(Path.Combine(baseDir, uri));
            if (!File.Exists(fullPath))
            {
                missing.Add(uri);
            }
        }

        return missing;
    }

    public static IReadOnlyList<string> ReadExternalUris(string gltfPath)
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
