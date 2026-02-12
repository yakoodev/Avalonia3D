using Serilog;
using SharpGLTF.Schema2;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace Avalonia3D.Loaders;

public sealed record MaterialExtensionData(
    MaterialClearcoatData Clearcoat,
    MaterialSheenData Sheen,
    MaterialSpecularData Specular,
    MaterialIorData Ior,
    MaterialEmissiveStrengthData EmissiveStrength,
    MaterialTransmissionData Transmission,
    MaterialExtensionTextureChannels TextureChannels)
{
    public static MaterialExtensionData Default { get; } = new(
        Clearcoat: new MaterialClearcoatData(0f, 0f),
        Sheen: new MaterialSheenData(Vector3.Zero, 0f),
        Specular: new MaterialSpecularData(1f, Vector3.One),
        Ior: new MaterialIorData(1.5f),
        EmissiveStrength: new MaterialEmissiveStrengthData(1f),
        Transmission: new MaterialTransmissionData(0f, 0f, 1.5f, float.PositiveInfinity, Vector3.One),
        TextureChannels: MaterialExtensionTextureChannels.None);
}

public readonly record struct MaterialClearcoatData(float Factor, float Roughness);
public readonly record struct MaterialSheenData(Vector3 ColorFactor, float RoughnessFactor);
public readonly record struct MaterialSpecularData(float Factor, Vector3 ColorFactor);
public readonly record struct MaterialIorData(float Value);
public readonly record struct MaterialEmissiveStrengthData(float Value);
public readonly record struct MaterialTransmissionData(float Factor, float Thickness, float Ior, float AttenuationDistance, Vector3 AttenuationColor);

public sealed record MaterialExtensionTextureChannels(
    object? Clearcoat,
    object? ClearcoatRoughness,
    object? ClearcoatNormal,
    object? SheenColor,
    object? SheenRoughness,
    object? Specular,
    object? SpecularColor,
    object? Transmission,
    object? VolumeThickness)
{
    public static MaterialExtensionTextureChannels None { get; } = new(
        Clearcoat: null,
        ClearcoatRoughness: null,
        ClearcoatNormal: null,
        SheenColor: null,
        SheenRoughness: null,
        Specular: null,
        SpecularColor: null,
        Transmission: null,
        VolumeThickness: null);
}

public sealed class GltfMaterialExtensionsReader
{
    private static readonly HashSet<string> SupportedExtensionNames = new(StringComparer.Ordinal)
    {
        "KHR_materials_clearcoat",
        "KHR_materials_sheen",
        "KHR_materials_specular",
        "KHR_materials_ior",
        "KHR_materials_emissive_strength",
        "KHR_materials_transmission",
        "KHR_materials_volume"
    };

    public MaterialExtensionData Read(Material material)
    {
        if (material == null)
        {
            return MaterialExtensionData.Default;
        }

        LogUnknownExtensions(material);

        var emissiveStrength = ReadScalar(FindChannel(material, "Emissive"), MaterialExtensionData.Default.EmissiveStrength.Value, min: 0f);
        var transmissionFactor = ReadScalar(FindChannel(material, "Transmission"), MaterialExtensionData.Default.Transmission.Factor, min: 0f, max: 1f);
        var transmissionThickness = ReadScalar(FindChannel(material, "VolumeThickness", "Thickness"), MaterialExtensionData.Default.Transmission.Thickness, min: 0f);
        var transmissionAttenuationDistance = ReadScalar(FindChannel(material, "VolumeAttenuation", "Attenuation"), MaterialExtensionData.Default.Transmission.AttenuationDistance, min: float.Epsilon, allowInfinityFallback: true, scalarFromVectorW: true);
        var transmissionAttenuationColor = ReadVector3(FindChannel(material, "VolumeAttenuation", "Attenuation"), MaterialExtensionData.Default.Transmission.AttenuationColor);

        var clearcoatFactor = ReadScalar(FindChannel(material, "ClearCoat", "Clearcoat"), MaterialExtensionData.Default.Clearcoat.Factor, min: 0f, max: 1f);
        var clearcoatRoughness = ReadScalar(FindChannel(material, "ClearCoatRoughness", "ClearcoatRoughness"), MaterialExtensionData.Default.Clearcoat.Roughness, min: 0f, max: 1f);

        var sheenColor = ReadVector3(FindChannel(material, "SheenColor"), MaterialExtensionData.Default.Sheen.ColorFactor);
        var sheenRoughness = ReadScalar(FindChannel(material, "SheenRoughness"), MaterialExtensionData.Default.Sheen.RoughnessFactor, min: 0f, max: 1f);

        var specularFactor = ReadScalar(FindChannel(material, "SpecularFactor"), MaterialExtensionData.Default.Specular.Factor, min: 0f, max: 1f);
        var specularColor = ReadVector3(FindChannel(material, "SpecularColor"), MaterialExtensionData.Default.Specular.ColorFactor);

        var ior = Math.Clamp(material.IndexOfRefraction, 1f, 3f);

        var clearcoatChannel = FindChannel(material, "ClearCoat", "Clearcoat");
        var clearcoatRoughnessChannel = FindChannel(material, "ClearCoatRoughness", "ClearcoatRoughness");
        var clearcoatNormalChannel = FindChannel(material, "ClearCoatNormal", "ClearcoatNormal");
        var sheenColorChannel = FindChannel(material, "SheenColor");
        var sheenRoughnessChannel = FindChannel(material, "SheenRoughness");
        var specularChannel = FindChannel(material, "SpecularFactor");
        var specularColorChannel = FindChannel(material, "SpecularColor");
        var transmissionChannel = FindChannel(material, "Transmission");
        var volumeThicknessChannel = FindChannel(material, "VolumeThickness", "Thickness");

        return new MaterialExtensionData(
            Clearcoat: new MaterialClearcoatData(clearcoatFactor, clearcoatRoughness),
            Sheen: new MaterialSheenData(sheenColor, sheenRoughness),
            Specular: new MaterialSpecularData(specularFactor, specularColor),
            Ior: new MaterialIorData(ior),
            EmissiveStrength: new MaterialEmissiveStrengthData(emissiveStrength),
            Transmission: new MaterialTransmissionData(transmissionFactor, transmissionThickness, ior, transmissionAttenuationDistance, transmissionAttenuationColor),
            TextureChannels: new MaterialExtensionTextureChannels(
                Clearcoat: clearcoatChannel,
                ClearcoatRoughness: clearcoatRoughnessChannel,
                ClearcoatNormal: clearcoatNormalChannel,
                SheenColor: sheenColorChannel,
                SheenRoughness: sheenRoughnessChannel,
                Specular: specularChannel,
                SpecularColor: specularColorChannel,
                Transmission: transmissionChannel,
                VolumeThickness: volumeThicknessChannel));
    }


    private static object? FindChannel(Material material, params string[] names)
    {
        foreach (var name in names)
        {
            var channel = material.FindChannel(name);
            if (channel != null)
            {
                return channel;
            }
        }

        return null;
    }

    private static void LogUnknownExtensions(Material material)
    {
        foreach (var extension in material.Extensions)
        {
            var extensionTypeName = extension.GetType().Name;
            var extensionName = extensionTypeName switch
            {
                "MaterialClearCoat" => "KHR_materials_clearcoat",
                "MaterialSheen" => "KHR_materials_sheen",
                "MaterialSpecular" => "KHR_materials_specular",
                "MaterialIOR" => "KHR_materials_ior",
                "MaterialEmissiveStrength" => "KHR_materials_emissive_strength",
                "MaterialTransmission" => "KHR_materials_transmission",
                "MaterialVolume" => "KHR_materials_volume",
                _ => TryGetDynamicExtensionName(extension) ?? extension.GetType().FullName ?? extensionTypeName
            };

            if (!SupportedExtensionNames.Contains(extensionName))
            {
                Log.Warning("GLTF material '{MaterialName}' contains unsupported extension '{ExtensionName}'. Extension will be ignored.", material.Name ?? "<unnamed>", extensionName);
            }
        }
    }

    private static string? TryGetDynamicExtensionName(object extension)
    {
        var nameProperty = extension.GetType().GetProperty("Name");
        if (nameProperty?.GetValue(extension) is string extensionName && !string.IsNullOrWhiteSpace(extensionName))
        {
            return extensionName;
        }

        return null;
    }

    private static float ReadScalar(object? channel, float fallback, float? min = null, float? max = null, bool allowInfinityFallback = false, bool scalarFromVectorW = false)
    {
        if (channel == null)
        {
            return fallback;
        }

        var value = ReadScalarParameter(channel, fallback, scalarFromVectorW);

        if (allowInfinityFallback && value <= 0f)
        {
            return float.PositiveInfinity;
        }

        if (min.HasValue)
        {
            value = Math.Max(min.Value, value);
        }

        if (max.HasValue)
        {
            value = Math.Min(max.Value, value);
        }

        return value;
    }

    private static Vector3 ReadVector3(object? channel, Vector3 fallback)
    {
        if (channel == null)
        {
            return fallback;
        }

        if (TryReadNamedParameter(channel, "AttenuationColor", out var attenuationColor) && TryConvertVector3(attenuationColor, out var attenuationRgb))
        {
            return attenuationRgb;
        }

        if (TryReadNamedParameter(channel, "RGB", out var rgbValue) && TryConvertVector3(rgbValue, out var rgb))
        {
            return rgb;
        }

        var p = ReadChannelParameter(channel, new Vector4(fallback, 0f));
        return new Vector3(p.X, p.Y, p.Z);
    }

    private static float ReadScalarParameter(object channel, float fallback, bool preferW)
    {
        if (TryReadNamedParameter(channel, "EmissiveStrength", out var emissiveStrength) && TryConvertFloat(emissiveStrength, out var es))
        {
            return es;
        }

        if (TryReadNamedParameter(channel, "TransmissionFactor", out var transmissionFactor) && TryConvertFloat(transmissionFactor, out var tf))
        {
            return tf;
        }

        if (TryReadNamedParameter(channel, "ThicknessFactor", out var thickness) && TryConvertFloat(thickness, out var th))
        {
            return th;
        }

        if (TryReadNamedParameter(channel, "AttenuationDistance", out var attenuationDistance) && TryConvertFloat(attenuationDistance, out var ad))
        {
            return ad;
        }

        if (TryReadNamedParameter(channel, "ClearCoatFactor", out var clearcoat) && TryConvertFloat(clearcoat, out var cc))
        {
            return cc;
        }

        if (TryReadNamedParameter(channel, "RoughnessFactor", out var roughness) && TryConvertFloat(roughness, out var roughnessValue))
        {
            return roughnessValue;
        }

        if (TryReadNamedParameter(channel, "SpecularFactor", out var specular) && TryConvertFloat(specular, out var sf))
        {
            return sf;
        }

        var rawValue = ReadChannelParameter(channel, new Vector4(fallback, fallback, fallback, 0f));
        return preferW && rawValue.W != 0f ? rawValue.W : rawValue.X;
    }


    private static bool TryConvertVector3(object? value, out Vector3 result)
    {
        switch (value)
        {
            case Vector3 v3:
                result = v3;
                return true;
            case Vector4 v4:
                result = new Vector3(v4.X, v4.Y, v4.Z);
                return true;
            case float[] f when f.Length >= 3:
                result = new Vector3(f[0], f[1], f[2]);
                return true;
            case double[] d when d.Length >= 3:
                result = new Vector3((float)d[0], (float)d[1], (float)d[2]);
                return true;
            default:
                result = default;
                return false;
        }
    }

    private static bool TryReadNamedParameter(object channel, string parameterName, out object? value)
    {
        var parametersProperty = channel.GetType().GetProperty("Parameters");
        if (parametersProperty?.GetValue(channel) is System.Collections.IEnumerable parameters)
        {
            foreach (var parameter in parameters)
            {
                var name = parameter?.GetType().GetProperty("Name")?.GetValue(parameter) as string;
                if (!string.Equals(name, parameterName, StringComparison.Ordinal))
                {
                    continue;
                }

                value = parameter?.GetType().GetProperty("Value")?.GetValue(parameter);
                return value != null;
            }
        }

        value = null;
        return false;
    }

    private static bool TryConvertFloat(object? value, out float result)
    {
        switch (value)
        {
            case float f:
                result = f;
                return true;
            case double d:
                result = (float)d;
                return true;
            case int i:
                result = i;
                return true;
            default:
                result = 0f;
                return false;
        }
    }

    private static Vector4 ReadChannelParameter(object channel, Vector4 fallback)
    {
        var property = channel.GetType().GetProperty("Parameter");
        if (property?.GetValue(channel) is Vector4 value)
        {
            return value;
        }

        return fallback;
    }
}
