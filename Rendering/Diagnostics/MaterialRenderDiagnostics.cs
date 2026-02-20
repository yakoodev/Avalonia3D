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
            material.MetallicFactor,
            material.RoughnessFactor,
            material.OcclusionStrength,
            ShaderSelectionPolicy.BuildPbrFeatures(material, scene),
            ShaderSelectionPolicy.BuildMaterialFeatureSet(material, scene),
            scene.PbrDebugViewMode,
            BuildGpuSnapshot(resources));
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
        float MetallicFactor,
        float RoughnessFactor,
        float OcclusionStrength,
        PbrFeatures ComputedPbrFeatures,
        MaterialFeatureSet ComputedMaterialFeatures,
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
                state.GlError.ToString(),
                state.Width,
                state.Height,
                state.WasBoundToGpu,
                state.LastBoundTextureUnit);
    }
}
