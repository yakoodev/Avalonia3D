using Avalonia3D.Model;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Avalonia3D.Loaders.Policies;

public sealed class MaterialImportPolicyContext
{
    public static MaterialImportPolicyContext Default { get; } = new();

    public string? AssetPath { get; init; }
    public MaterialAlphaImportProfile AlphaProfile { get; init; } = MaterialAlphaImportProfile.Balanced;
    public MaterialSceneImportOverride? SceneOverride { get; init; }
    public MaterialAlphaMode SourceAlphaMode { get; init; } = MaterialAlphaMode.Opaque;
    public string? MaterialName { get; init; }
    public string? MeshName { get; init; }
    public string? NodeName { get; init; }
    public string? NodeStableId { get; init; }
    public bool IsAnimatedMaterial { get; init; }
}

public sealed class MaterialSceneImportOverride
{
    public MaterialAlphaImportProfile? AlphaProfile { get; init; }
    public MaterialAlphaMode? ForceAlphaMode { get; init; }
    public bool? PreserveBlendWithoutAlphaSignalForEmissive { get; init; }
    public bool? ForceTextureTransparencySignal { get; init; }
}

public sealed class MaterialAssetImportOverride
{
    public MaterialAlphaImportProfile? AlphaProfile { get; init; }
    public MaterialAlphaMode? ForceAlphaMode { get; init; }
    public bool? PreserveBlendWithoutAlphaSignalForEmissive { get; init; }
    public bool? ForceTextureTransparencySignal { get; init; }
    public IReadOnlyDictionary<string, MaterialSceneImportOverride> Materials { get; init; } = new Dictionary<string, MaterialSceneImportOverride>(StringComparer.OrdinalIgnoreCase);
}

public static class MaterialImportOverrideConfiguration
{
    private const string ConfigArgPrefix = "--material-import-overrides=";
    private const string ConfigEnv = "AVALONIA3D_MATERIAL_IMPORT_OVERRIDES";

    private static readonly Dictionary<string, MaterialAssetImportOverride> _overrides = new(StringComparer.OrdinalIgnoreCase);

    public static string? ConfigPath { get; private set; }

    public static void Configure(IReadOnlyDictionary<string, MaterialSceneImportOverride>? sceneOverrides)
    {
        if (sceneOverrides == null)
        {
            ConfigureAssetOverrides(null);
            return;
        }

        var converted = new Dictionary<string, MaterialAssetImportOverride>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in sceneOverrides)
        {
            if (pair.Value == null)
            {
                continue;
            }

            converted[pair.Key] = new MaterialAssetImportOverride
            {
                AlphaProfile = pair.Value.AlphaProfile,
                ForceAlphaMode = pair.Value.ForceAlphaMode,
                PreserveBlendWithoutAlphaSignalForEmissive = pair.Value.PreserveBlendWithoutAlphaSignalForEmissive,
                ForceTextureTransparencySignal = pair.Value.ForceTextureTransparencySignal
            };
        }

        ConfigureAssetOverrides(converted);
    }

    public static void ConfigureAssetOverrides(IReadOnlyDictionary<string, MaterialAssetImportOverride>? assetOverrides)
    {
        _overrides.Clear();
        ConfigPath = null;

        if (assetOverrides == null)
        {
            return;
        }

        foreach (var pair in assetOverrides)
        {
            var normalized = NormalizePath(pair.Key);
            if (string.IsNullOrWhiteSpace(normalized) || pair.Value == null)
            {
                continue;
            }

            _overrides[normalized] = pair.Value;
        }
    }

    public static void ConfigureFromPath(string? configPath)
    {
        Configure(null);

        if (string.IsNullOrWhiteSpace(configPath) || !File.Exists(configPath))
        {
            return;
        }

        var json = File.ReadAllText(configPath);
        var parsed = ParseJson(json);
        ConfigureAssetOverrides(parsed);
        ConfigPath = configPath;
    }

    public static string? ResolveConfigPath(string[]? args, IReadOnlyDictionary<string, string?>? environment = null)
    {
        var argsPath = TryParseFromArgs(args);
        if (!string.IsNullOrWhiteSpace(argsPath))
        {
            return argsPath;
        }

        if (environment != null && environment.TryGetValue(ConfigEnv, out var value))
        {
            return value;
        }

        return Environment.GetEnvironmentVariable(ConfigEnv);
    }

    public static MaterialSceneImportOverride? ResolveForAsset(string? assetPath)
    {
        return ResolveForMaterial(assetPath, null);
    }

    public static MaterialSceneImportOverride? ResolveForMaterial(string? assetPath, string? materialName)
    {
        var normalized = NormalizePath(assetPath);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return null;
        }

        MaterialAssetImportOverride? assetOverride = null;

        _overrides.TryGetValue(normalized, out assetOverride);

        if (assetOverride == null)
        {
            foreach (var pair in _overrides)
            {
                if (normalized.EndsWith(pair.Key, StringComparison.OrdinalIgnoreCase))
                {
                    assetOverride = pair.Value;
                    break;
                }
            }
        }

        if (assetOverride == null)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(materialName) && assetOverride.Materials.TryGetValue(materialName, out var materialOverride) && materialOverride != null)
        {
            return MergeOverrides(ToSceneOverride(assetOverride), materialOverride);
        }

        return ToSceneOverride(assetOverride);
    }

    private static IReadOnlyDictionary<string, MaterialAssetImportOverride> ParseJson(string json)
    {
        var result = new Dictionary<string, MaterialAssetImportOverride>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(json))
        {
            return result;
        }

        using var document = JsonDocument.Parse(json);
        if (document.RootElement.TryGetProperty("assets", out var assetsElement) && assetsElement.ValueKind == JsonValueKind.Object)
        {
            ParseAssetsObject(assetsElement, result);
        }

        // Backward compatibility with old "scenes" layout.
        if (document.RootElement.TryGetProperty("scenes", out var scenesElement) && scenesElement.ValueKind == JsonValueKind.Object)
        {
            ParseAssetsObject(scenesElement, result);
        }

        return result;
    }

    private static void ParseAssetsObject(JsonElement assetsElement, Dictionary<string, MaterialAssetImportOverride> result)
    {
        if (assetsElement.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        foreach (var sceneProperty in assetsElement.EnumerateObject())
        {
            if (sceneProperty.Value.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var materials = new Dictionary<string, MaterialSceneImportOverride>(StringComparer.OrdinalIgnoreCase);
            if (sceneProperty.Value.TryGetProperty("materials", out var materialsElement) && materialsElement.ValueKind == JsonValueKind.Object)
            {
                foreach (var materialProperty in materialsElement.EnumerateObject())
                {
                    if (materialProperty.Value.ValueKind != JsonValueKind.Object)
                    {
                        continue;
                    }

                    materials[materialProperty.Name] = ParseSceneOverride(materialProperty.Value);
                }
            }

            var sceneOverride = new MaterialAssetImportOverride
            {
                AlphaProfile = TryReadProfile(sceneProperty.Value, "alphaProfile"),
                ForceAlphaMode = TryReadAlphaMode(sceneProperty.Value, "forceAlphaMode"),
                PreserveBlendWithoutAlphaSignalForEmissive = TryReadBool(sceneProperty.Value, "preserveBlendWithoutAlphaSignalForEmissive"),
                ForceTextureTransparencySignal = TryReadBool(sceneProperty.Value, "forceTextureTransparencySignal"),
                Materials = materials
            };

            result[NormalizePath(sceneProperty.Name)] = sceneOverride;
        }
    }

    private static MaterialSceneImportOverride ParseSceneOverride(JsonElement source) =>
        new()
        {
            AlphaProfile = TryReadProfile(source, "alphaProfile"),
            ForceAlphaMode = TryReadAlphaMode(source, "forceAlphaMode"),
            PreserveBlendWithoutAlphaSignalForEmissive = TryReadBool(source, "preserveBlendWithoutAlphaSignalForEmissive"),
            ForceTextureTransparencySignal = TryReadBool(source, "forceTextureTransparencySignal")
        };

    private static MaterialSceneImportOverride ToSceneOverride(MaterialAssetImportOverride source) =>
        new()
        {
            AlphaProfile = source.AlphaProfile,
            ForceAlphaMode = source.ForceAlphaMode,
            PreserveBlendWithoutAlphaSignalForEmissive = source.PreserveBlendWithoutAlphaSignalForEmissive,
            ForceTextureTransparencySignal = source.ForceTextureTransparencySignal
        };

    private static MaterialSceneImportOverride MergeOverrides(MaterialSceneImportOverride assetOverride, MaterialSceneImportOverride materialOverride) =>
        new()
        {
            AlphaProfile = materialOverride.AlphaProfile ?? assetOverride.AlphaProfile,
            ForceAlphaMode = materialOverride.ForceAlphaMode ?? assetOverride.ForceAlphaMode,
            PreserveBlendWithoutAlphaSignalForEmissive = materialOverride.PreserveBlendWithoutAlphaSignalForEmissive ?? assetOverride.PreserveBlendWithoutAlphaSignalForEmissive,
            ForceTextureTransparencySignal = materialOverride.ForceTextureTransparencySignal ?? assetOverride.ForceTextureTransparencySignal
        };

    private static string? TryParseFromArgs(string[]? args)
    {
        if (args == null)
        {
            return null;
        }

        foreach (var arg in args)
        {
            if (string.IsNullOrWhiteSpace(arg) || !arg.StartsWith(ConfigArgPrefix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            return arg[ConfigArgPrefix.Length..];
        }

        return null;
    }

    private static MaterialAlphaImportProfile? TryReadProfile(JsonElement source, string propertyName)
    {
        if (!source.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        var raw = property.GetString();
        if (string.Equals(raw, "strict", StringComparison.OrdinalIgnoreCase))
        {
            return MaterialAlphaImportProfile.Strict;
        }

        if (string.Equals(raw, "balanced", StringComparison.OrdinalIgnoreCase))
        {
            return MaterialAlphaImportProfile.Balanced;
        }

        if (string.Equals(raw, "legacy", StringComparison.OrdinalIgnoreCase))
        {
            return MaterialAlphaImportProfile.Legacy;
        }

        return null;
    }

    private static MaterialAlphaMode? TryReadAlphaMode(JsonElement source, string propertyName)
    {
        if (!source.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        var raw = property.GetString();
        if (string.Equals(raw, "opaque", StringComparison.OrdinalIgnoreCase))
        {
            return MaterialAlphaMode.Opaque;
        }

        if (string.Equals(raw, "mask", StringComparison.OrdinalIgnoreCase))
        {
            return MaterialAlphaMode.Mask;
        }

        if (string.Equals(raw, "blend", StringComparison.OrdinalIgnoreCase))
        {
            return MaterialAlphaMode.Blend;
        }

        return null;
    }

    private static bool? TryReadBool(JsonElement source, string propertyName)
    {
        if (!source.TryGetProperty(propertyName, out var property) ||
            (property.ValueKind != JsonValueKind.True && property.ValueKind != JsonValueKind.False))
        {
            return null;
        }

        return property.GetBoolean();
    }

    private static string NormalizePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        return path.Trim().Replace('\\', '/');
    }
}
