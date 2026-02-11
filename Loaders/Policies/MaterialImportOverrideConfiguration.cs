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

public static class MaterialImportOverrideConfiguration
{
    private const string ConfigArgPrefix = "--material-import-overrides=";
    private const string ConfigEnv = "AVALONIA3D_MATERIAL_IMPORT_OVERRIDES";

    private static readonly Dictionary<string, MaterialSceneImportOverride> _overrides = new(StringComparer.OrdinalIgnoreCase);

    public static string? ConfigPath { get; private set; }

    public static void Configure(IReadOnlyDictionary<string, MaterialSceneImportOverride>? sceneOverrides)
    {
        _overrides.Clear();
        ConfigPath = null;

        if (sceneOverrides == null)
        {
            return;
        }

        foreach (var pair in sceneOverrides)
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
        Configure(parsed);
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
        var normalized = NormalizePath(assetPath);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return null;
        }

        if (_overrides.TryGetValue(normalized, out var direct))
        {
            return direct;
        }

        foreach (var pair in _overrides)
        {
            if (normalized.EndsWith(pair.Key, StringComparison.OrdinalIgnoreCase))
            {
                return pair.Value;
            }
        }

        return null;
    }

    private static IReadOnlyDictionary<string, MaterialSceneImportOverride> ParseJson(string json)
    {
        var result = new Dictionary<string, MaterialSceneImportOverride>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(json))
        {
            return result;
        }

        using var document = JsonDocument.Parse(json);
        if (!document.RootElement.TryGetProperty("scenes", out var scenesElement) || scenesElement.ValueKind != JsonValueKind.Object)
        {
            return result;
        }

        foreach (var sceneProperty in scenesElement.EnumerateObject())
        {
            if (sceneProperty.Value.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var sceneOverride = new MaterialSceneImportOverride
            {
                AlphaProfile = TryReadProfile(sceneProperty.Value, "alphaProfile"),
                ForceAlphaMode = TryReadAlphaMode(sceneProperty.Value, "forceAlphaMode"),
                PreserveBlendWithoutAlphaSignalForEmissive = TryReadBool(sceneProperty.Value, "preserveBlendWithoutAlphaSignalForEmissive"),
                ForceTextureTransparencySignal = TryReadBool(sceneProperty.Value, "forceTextureTransparencySignal")
            };

            result[NormalizePath(sceneProperty.Name)] = sceneOverride;
        }

        return result;
    }

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
