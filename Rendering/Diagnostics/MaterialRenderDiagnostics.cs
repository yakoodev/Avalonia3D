using Avalonia3D.Model;
using Avalonia3D.Shaders;
using Serilog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Numerics;
using System.Text.Json;

namespace Avalonia3D.Rendering.Diagnostics;

public static class MaterialRenderDiagnostics
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false
    };

    private static readonly ConcurrentDictionary<string, string> LastSnapshotByMaterialKey = new(StringComparer.Ordinal);

    public static bool Enabled { get; private set; }

    public static void SetEnabled(bool enabled)
    {
        Enabled = enabled;
        if (enabled)
        {
            LastSnapshotByMaterialKey.Clear();
        }
    }

    public static void DumpIfEnabled(Material? material, Scene3D scene, string materialKey)
    {
        if (!Enabled || material == null)
        {
            return;
        }

        var snapshot = Capture(material, scene);
        var serialized = JsonSerializer.Serialize(snapshot, JsonOptions);

        if (LastSnapshotByMaterialKey.TryGetValue(materialKey, out var previous) && string.Equals(previous, serialized, StringComparison.Ordinal))
        {
            return;
        }

        LastSnapshotByMaterialKey[materialKey] = serialized;
        Log.Information("PBR material diagnostics [{MaterialKey}]: {Snapshot}", materialKey, serialized);
    }

    public static MaterialRenderSnapshot Capture(Material material, Scene3D scene)
    {
        var textureStates = new List<TextureSemanticState>
        {
            CreateTextureState(material, TextureSemantic.BaseColor, material.BaseColorTexture),
            CreateTextureState(material, TextureSemantic.Normal, material.NormalTexture),
            CreateTextureState(material, TextureSemantic.MetallicRoughness, material.MetallicRoughnessTexture),
            CreateTextureState(material, TextureSemantic.Occlusion, material.OcclusionTexture),
            CreateTextureState(material, TextureSemantic.Emissive, material.EmissiveTexture),
            CreateTextureState(material, TextureSemantic.Clearcoat, material.ExtensionTextures.ClearcoatTexture),
            CreateTextureState(material, TextureSemantic.ClearcoatRoughness, material.ExtensionTextures.ClearcoatRoughnessTexture),
            CreateTextureState(material, TextureSemantic.ClearcoatNormal, material.ExtensionTextures.ClearcoatNormalTexture),
            CreateTextureState(material, TextureSemantic.SheenColor, material.ExtensionTextures.SheenColorTexture),
            CreateTextureState(material, TextureSemantic.SheenRoughness, material.ExtensionTextures.SheenRoughnessTexture),
            CreateTextureState(material, TextureSemantic.Specular, material.ExtensionTextures.SpecularTexture),
            CreateTextureState(material, TextureSemantic.SpecularColor, material.ExtensionTextures.SpecularColorTexture),
            CreateTextureState(material, TextureSemantic.Transmission, material.ExtensionTextures.TransmissionTexture),
            CreateTextureState(material, TextureSemantic.VolumeThickness, material.ExtensionTextures.VolumeThicknessTexture)
        };

        return new MaterialRenderSnapshot(
            textureStates,
            material.BaseColorFactor,
            material.MetallicFactor,
            material.RoughnessFactor,
            material.OcclusionStrength,
            ShaderSelectionPolicy.BuildPbrFeatures(material, scene));
    }

    private static TextureSemanticState CreateTextureState(Material material, TextureSemantic semantic, TextureData? texture)
    {
        var uv = material.TextureRuntime.GetOrCreate(semantic);

        return new TextureSemanticState(
            semantic,
            texture != null,
            uv.TexCoordSet,
            uv.UvOffset,
            uv.UvScale,
            uv.UvRotation);
    }

    public sealed record MaterialRenderSnapshot(
        IReadOnlyList<TextureSemanticState> Textures,
        Vector4 BaseColorFactor,
        float MetallicFactor,
        float RoughnessFactor,
        float OcclusionStrength,
        PbrFeatures ComputedPbrFeatures);

    public sealed record TextureSemanticState(
        TextureSemantic Semantic,
        bool HasTexture,
        int TexCoordSet,
        Vector2 UvOffset,
        Vector2 UvScale,
        float UvRotation);
}
