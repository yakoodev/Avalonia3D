using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;

namespace Avalonia3D.Loaders;

public static class GltfDependencyInspector
{
    private const uint GlbMagic = 0x46546C67; // 'glTF'
    private const uint JsonChunkType = 0x4E4F534A; // 'JSON'

    public static IReadOnlyList<string> GetMissingDependencies(string gltfPath)
    {
        if (string.IsNullOrWhiteSpace(gltfPath) || !File.Exists(gltfPath))
        {
            return [];
        }

        var preflight = ReadPreflight(gltfPath);
        var dependencies = preflight.ExternalUris;
        var baseDir = Path.GetDirectoryName(gltfPath) ?? string.Empty;
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
        => ReadPreflight(gltfPath).ExternalUris;

    public static GltfPreflightResult ReadPreflight(string gltfPath)
    {
        if (string.IsNullOrWhiteSpace(gltfPath) || !File.Exists(gltfPath))
        {
            return GltfPreflightResult.Empty;
        }

        var warnings = new List<string>();
        var result = new List<string>();

        try
        {
            var containerKind = DetectContainerKind(gltfPath);
            switch (containerKind)
            {
                case GltfContainerKind.GltfJson:
                    using (var stream = File.OpenRead(gltfPath))
                    using (var doc = JsonDocument.Parse(stream))
                    {
                        CollectUris(doc.RootElement, "buffers", result);
                        CollectUris(doc.RootElement, "images", result);
                    }

                    return new GltfPreflightResult(
                        containerKind,
                        result,
                        warnings,
                        ExternalDependencyScanSupported: true);

                case GltfContainerKind.GlbBinary:
                    using (var stream = File.OpenRead(gltfPath))
                    using (var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: false))
                    {
                        if (!TryReadGlbJsonChunk(reader, out var glbRoot))
                        {
                            warnings.Add("GLB JSON chunk was not found; external dependency scan skipped.");
                            return new GltfPreflightResult(
                                containerKind,
                                result,
                                warnings,
                                ExternalDependencyScanSupported: false);
                        }

                        CollectUris(glbRoot, "buffers", result);
                        CollectUris(glbRoot, "images", result);
                    }

                    return new GltfPreflightResult(
                        containerKind,
                        result,
                        warnings,
                        ExternalDependencyScanSupported: true);

                default:
                    warnings.Add("Unknown glTF container type; external dependency scan skipped.");
                    Log.Debug("GLTF preflight skipped: unknown container type for {File}", Path.GetFileName(gltfPath));
                    return new GltfPreflightResult(
                        containerKind,
                        result,
                        warnings,
                        ExternalDependencyScanSupported: false);
            }
        }
        catch (Exception ex)
        {
            warnings.Add($"Failed to parse GLTF dependencies: {ex.Message}");
            Log.Warning(ex, "Failed to parse GLTF dependencies: {File}", Path.GetFileName(gltfPath));
            return new GltfPreflightResult(
                GltfContainerKind.Unknown,
                result,
                warnings,
                ExternalDependencyScanSupported: false);
        }
    }

    private static bool TryReadGlbJsonChunk(BinaryReader reader, out JsonElement root)
    {
        root = default;

        if (reader.BaseStream.Length < 12)
        {
            return false;
        }

        var magic = reader.ReadUInt32();
        var version = reader.ReadUInt32();
        var fileLength = reader.ReadUInt32();

        if (magic != GlbMagic || version < 2 || fileLength > reader.BaseStream.Length)
        {
            return false;
        }

        while (reader.BaseStream.Position + 8 <= reader.BaseStream.Length)
        {
            var chunkLength = reader.ReadUInt32();
            var chunkType = reader.ReadUInt32();
            if (chunkLength > int.MaxValue || reader.BaseStream.Position + chunkLength > reader.BaseStream.Length)
            {
                return false;
            }

            var chunkBytes = reader.ReadBytes((int)chunkLength);
            if (chunkType != JsonChunkType)
            {
                continue;
            }

            using var doc = JsonDocument.Parse(chunkBytes);
            root = doc.RootElement.Clone();
            return true;
        }

        return false;
    }

    private static GltfContainerKind DetectContainerKind(string gltfPath)
    {
        var extension = Path.GetExtension(gltfPath);
        if (string.Equals(extension, ".gltf", StringComparison.OrdinalIgnoreCase))
        {
            return GltfContainerKind.GltfJson;
        }

        if (string.Equals(extension, ".glb", StringComparison.OrdinalIgnoreCase))
        {
            return IsGlbSignature(gltfPath)
                ? GltfContainerKind.GlbBinary
                : GltfContainerKind.Unknown;
        }

        if (IsGlbSignature(gltfPath))
        {
            return GltfContainerKind.GlbBinary;
        }

        return GltfContainerKind.GltfJson;
    }

    private static bool IsGlbSignature(string path)
    {
        using var stream = File.OpenRead(path);
        if (stream.Length < 4)
        {
            return false;
        }

        Span<byte> magic = stackalloc byte[4];
        var read = stream.Read(magic);
        return read == 4
            && magic[0] == (byte)'g'
            && magic[1] == (byte)'l'
            && magic[2] == (byte)'T'
            && magic[3] == (byte)'F';
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
