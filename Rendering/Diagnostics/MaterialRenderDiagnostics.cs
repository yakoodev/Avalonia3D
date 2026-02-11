using Avalonia3D.Model;
using Avalonia3D.Shaders;
using Serilog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
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

    public static void DumpIfEnabled(Material? material, RenderResources? resources, Scene3D scene, string materialKey)
    {
        if (!Enabled || material == null)
        {
            return;
        }

        var snapshot = Capture(material, resources, scene);
        var serialized = JsonSerializer.Serialize(snapshot, JsonOptions);

        if (LastSnapshotByMaterialKey.TryGetValue(materialKey, out var previous) && string.Equals(previous, serialized, StringComparison.Ordinal))
        {
            return;
        }

        LastSnapshotByMaterialKey[materialKey] = serialized;
        Log.Information("PBR material diagnostics [{MaterialKey}]: {Snapshot}", materialKey, serialized);
    }

    public static MaterialRenderSnapshot Capture(Material material, RenderResources? resources, Scene3D scene)
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
            material.EmissiveFactor,
            material.MetallicFactor,
            material.RoughnessFactor,
            material.OcclusionStrength,
            material.EmissiveStrength,
            material.EmissiveIntensity,
            material.AlphaMode,
            material.AlphaCutoff,
            material.SurfaceAdvanced.HasTransmission,
            material.TransmissionFactor,
            material.Ior,
            BuildMaterialWarnings(material),
            ShaderSelectionPolicy.BuildPbrFeatures(material, scene),
            scene.PbrDebugViewMode,
            BuildGpuSnapshot(resources));
    }


    private static IReadOnlyList<string> BuildMaterialWarnings(Material material)
    {
        var warnings = new List<string>();

        if (material.BaseColorFactor.X > 1f || material.BaseColorFactor.Y > 1f || material.BaseColorFactor.Z > 1f || material.BaseColorFactor.W > 1f)
        {
            warnings.Add("BaseColorFactor has component(s) > 1.0");
        }

        if (material.EmissiveFactor.X > 1f || material.EmissiveFactor.Y > 1f || material.EmissiveFactor.Z > 1f)
        {
            warnings.Add("EmissiveFactor has component(s) > 1.0");
        }

        if (material.EmissiveStrength > 8f)
        {
            warnings.Add("EmissiveStrength is high (>8.0)");
        }

        if (material.MetallicFactor < 0f || material.MetallicFactor > 1f)
        {
            warnings.Add("MetallicFactor is outside [0..1]");
        }

        if (material.RoughnessFactor < 0f || material.RoughnessFactor > 1f)
        {
            warnings.Add("RoughnessFactor is outside [0..1]");
        }

        return warnings;
    }

    private static MaterialGpuSnapshot BuildGpuSnapshot(RenderResources? resources)
    {
        if (resources == null)
        {
            return new MaterialGpuSnapshot(Array.Empty<TextureBindingStateSnapshot>());
        }

        var bindings = resources.TextureBindings
            .OrderBy(kvp => kvp.Key)
            .Select(kvp => TextureBindingStateSnapshot.From(kvp.Value))
            .ToArray();

        return new MaterialGpuSnapshot(bindings);
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
        Vector3 EmissiveFactor,
        float MetallicFactor,
        float RoughnessFactor,
        float OcclusionStrength,
        float EmissiveStrength,
        float EmissiveIntensity,
        MaterialAlphaMode AlphaMode,
        float AlphaCutoff,
        bool HasTransmission,
        float TransmissionFactor,
        float Ior,
        IReadOnlyList<string> MaterialWarnings,
        PbrFeatures ComputedPbrFeatures,
        PbrDebugViewMode ActivePbrDebugViewMode,
        MaterialGpuSnapshot GpuSnapshot);

    public sealed record TextureSemanticState(
        TextureSemantic Semantic,
        bool HasTexture,
        int TexCoordSet,
        Vector2 UvOffset,
        Vector2 UvScale,
        float UvRotation);

    public sealed record MaterialGpuSnapshot(
        IReadOnlyList<TextureBindingStateSnapshot> Bindings);

    public sealed record TextureBindingStateSnapshot(
        TextureSemantic Semantic,
        uint TextureId,
        bool IsLoaded,
        TextureColorFlags FormatFlags,
        string? PreferredInternalFormat,
        string? UsedInternalFormat,
        string SourceColorFormat,
        ColorDecodeMode DecodeMode,
        string? DecodeFallbackReason,
        string GlError,
        int Width,
        int Height,
        bool WasBoundToGpu,
        int? LastBoundTextureUnit)
    {
        public static TextureBindingStateSnapshot From(TextureBindingState state)
            => new(
                state.Semantic,
                state.TextureId,
                state.IsLoaded,
                state.FormatFlags,
                state.PreferredInternalFormat?.ToString(),
                state.UsedInternalFormat?.ToString(),
                state.SourceColorFormat,
                state.DecodeMode,
                state.DecodeFallbackReason,
                state.GlError.ToString(),
                state.Width,
                state.Height,
                state.WasBoundToGpu,
                state.LastBoundTextureUnit);
    }
}
