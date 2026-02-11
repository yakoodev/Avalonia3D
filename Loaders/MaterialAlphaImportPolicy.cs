using Avalonia3D.Model;
using System;
using System.Collections.Generic;

namespace Avalonia3D.Loaders;

public enum MaterialAlphaImportProfile
{
    Strict,
    Balanced,
    Legacy
}

public static class MaterialAlphaImportConfiguration
{
    private const string ProfileArgPrefix = "--material-alpha-import=";
    private const string ProfileEnv = "AVALONIA3D_MATERIAL_ALPHA_IMPORT";

    public static MaterialAlphaImportProfile CurrentProfile { get; private set; } = MaterialAlphaImportProfile.Balanced;

    public static void Configure(MaterialAlphaImportProfile profile)
    {
        CurrentProfile = profile;
    }

    public static MaterialAlphaImportProfile ResolveFrom(string[]? args, IReadOnlyDictionary<string, string?>? environment = null, MaterialAlphaImportProfile fallback = MaterialAlphaImportProfile.Balanced)
    {
        var argsProfile = TryParseFromArgs(args);
        if (argsProfile.HasValue)
        {
            return argsProfile.Value;
        }

        var envProfile = TryParseFromEnvironment(environment);
        if (envProfile.HasValue)
        {
            return envProfile.Value;
        }

        return fallback;
    }

    private static MaterialAlphaImportProfile? TryParseFromArgs(string[]? args)
    {
        if (args == null)
        {
            return null;
        }

        foreach (var arg in args)
        {
            if (string.IsNullOrWhiteSpace(arg) || !arg.StartsWith(ProfileArgPrefix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var value = arg[ProfileArgPrefix.Length..];
            return TryParseProfile(value);
        }

        return null;
    }

    private static MaterialAlphaImportProfile? TryParseFromEnvironment(IReadOnlyDictionary<string, string?>? environment)
    {
        string? value;
        if (environment != null)
        {
            environment.TryGetValue(ProfileEnv, out value);
        }
        else
        {
            value = Environment.GetEnvironmentVariable(ProfileEnv);
        }

        return TryParseProfile(value);
    }

    private static MaterialAlphaImportProfile? TryParseProfile(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (string.Equals(value, "strict", StringComparison.OrdinalIgnoreCase))
        {
            return MaterialAlphaImportProfile.Strict;
        }

        if (string.Equals(value, "balanced", StringComparison.OrdinalIgnoreCase))
        {
            return MaterialAlphaImportProfile.Balanced;
        }

        if (string.Equals(value, "legacy", StringComparison.OrdinalIgnoreCase))
        {
            return MaterialAlphaImportProfile.Legacy;
        }

        return null;
    }
}

public sealed class MaterialAlphaImportPolicy
{
    private const float AlphaSignalThreshold = 0.999f;
    private const float EmissiveFactorThresholdSq = 0.000001f;
    private const float EmissiveStrengthThreshold = 1.001f;

    public void Apply(Material material, MaterialAlphaImportProfile profile)
    {
        if (material == null || material.AlphaMode != MaterialAlphaMode.Blend)
        {
            return;
        }

        var hasAlphaSignal = material.BaseColorFactor.W < AlphaSignalThreshold || material.HasTextureTransparency;
        if (hasAlphaSignal)
        {
            return;
        }

        if (profile == MaterialAlphaImportProfile.Strict)
        {
            return;
        }

        if (profile == MaterialAlphaImportProfile.Balanced && HasEmissiveIntent(material))
        {
            return;
        }

        material.AlphaMode = MaterialAlphaMode.Opaque;
    }

    private static bool HasEmissiveIntent(Material material)
    {
        if (material.EmissiveTexture != null)
        {
            return true;
        }

        if (material.EmissiveFactor.LengthSquared() > EmissiveFactorThresholdSq)
        {
            return true;
        }

        return material.EmissiveStrength > EmissiveStrengthThreshold;
    }
}
