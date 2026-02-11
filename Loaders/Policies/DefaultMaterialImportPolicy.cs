using Avalonia3D.Model;
using Avalonia3D.Rendering;
using System;

namespace Avalonia3D.Loaders.Policies;

public sealed class DefaultMaterialImportPolicy : IMaterialImportPolicy
{
    private const float AlphaSignalThreshold = 0.999f;
    private const float EmissiveFactorThresholdSq = 0.000001f;
    private const float EmissiveStrengthThreshold = 1.001f;

    public MaterialAlphaMode ResolveAlphaMode(Material material, MaterialImportPolicyContext context)
    {
        if (material == null)
        {
            return MaterialAlphaMode.Opaque;
        }

        var sceneOverride = context.SceneOverride;
        if (sceneOverride?.ForceAlphaMode.HasValue == true)
        {
            return sceneOverride.ForceAlphaMode.Value;
        }

        if (material.AlphaMode != MaterialAlphaMode.Blend)
        {
            return material.AlphaMode;
        }

        var textureTransparencySignal = sceneOverride?.ForceTextureTransparencySignal ?? HasMeaningfulTextureTransparency(material.BaseColorTexture);
        var hasAlphaSignal = material.BaseColorFactor.W < AlphaSignalThreshold || textureTransparencySignal;
        material.HasTextureTransparency = textureTransparencySignal;

        if (hasAlphaSignal)
        {
            return MaterialAlphaMode.Blend;
        }

        var alphaProfile = sceneOverride?.AlphaProfile ?? context.AlphaProfile;
        if (alphaProfile == MaterialAlphaImportProfile.Strict)
        {
            return MaterialAlphaMode.Blend;
        }

        if (alphaProfile == MaterialAlphaImportProfile.Balanced && ResolveEmissiveBehavior(material, context) == MaterialEmissiveBehavior.TreatAsTransparencySignal)
        {
            return MaterialAlphaMode.Blend;
        }

        return MaterialAlphaMode.Opaque;
    }

    public MaterialColorSpaceHandling ResolveColorSpaceHandling(Material material, TextureSemantic semantic, MaterialImportPolicyContext context)
    {
        _ = material;
        _ = semantic;
        _ = context;
        return MaterialColorSpaceHandling.Default;
    }

    public MaterialEmissiveBehavior ResolveEmissiveBehavior(Material material, MaterialImportPolicyContext context)
    {
        var preserveOverride = context.SceneOverride?.PreserveBlendWithoutAlphaSignalForEmissive;
        if (preserveOverride.HasValue)
        {
            return preserveOverride.Value
                ? MaterialEmissiveBehavior.TreatAsTransparencySignal
                : MaterialEmissiveBehavior.IgnoreForTransparencyFallback;
        }

        if (material.EmissiveTexture != null)
        {
            return MaterialEmissiveBehavior.TreatAsTransparencySignal;
        }

        if (material.EmissiveFactor.LengthSquared() > EmissiveFactorThresholdSq)
        {
            return MaterialEmissiveBehavior.TreatAsTransparencySignal;
        }

        return material.EmissiveStrength > EmissiveStrengthThreshold
            ? MaterialEmissiveBehavior.TreatAsTransparencySignal
            : MaterialEmissiveBehavior.IgnoreForTransparencyFallback;
    }

    private enum TextureAlphaHeuristicProfile
    {
        Strict,
        Balanced,
        Permissive
    }

    private static class TextureAlphaHeuristics
    {
        public const byte SoftTransparentAlphaThreshold = 253;
        public const byte RegularTransparentAlphaThreshold = 245;
        public const byte DeepTransparentAlphaThreshold = 64;
        public const byte OpaqueAlphaThreshold = 254;

        public const int MaxSamples = 8192;
        public const float MinOpaqueRatio = 0.05f;
        public const float MinDeepTransparentRatio = 0.001f;
        public const float MinRegularTransparentRatio = 0.01f;
        public const float MinSoftTransparentRatio = 0.15f;

        public const float DenseDeepMaskOpaqueRatio = 0.35f;
        public const float StrictDenseDeepMaskRatio = 0.15f;
        public const float BalancedDenseDeepMaskRatio = 0.20f;
        public const float PermissiveDenseDeepMaskRatio = 0.35f;

        public static TextureAlphaHeuristicProfile ActiveProfile { get; set; } = TextureAlphaHeuristicProfile.Balanced;

        public static float GetDenseDeepMaskRatioThreshold()
        {
            return ActiveProfile switch
            {
                TextureAlphaHeuristicProfile.Strict => StrictDenseDeepMaskRatio,
                TextureAlphaHeuristicProfile.Permissive => PermissiveDenseDeepMaskRatio,
                _ => BalancedDenseDeepMaskRatio,
            };
        }
    }

    public static bool HasMeaningfulTextureTransparency(TextureData? texture)
    {
        if (texture?.Data == null || texture.Data.Length < 4)
        {
            return false;
        }

        var data = texture.Data;
        var pixelCount = data.Length / 4;
        if (pixelCount <= 0)
        {
            return false;
        }

        var sampled = 0;
        var softTransparent = 0;
        var regularTransparent = 0;
        var deepTransparent = 0;
        var opaque = 0;

        var stepPixels = Math.Max(1, pixelCount / TextureAlphaHeuristics.MaxSamples);
        var step = stepPixels * 4;

        for (var i = 3; i < data.Length; i += step)
        {
            sampled++;
            var alpha = data[i];

            if (alpha <= TextureAlphaHeuristics.SoftTransparentAlphaThreshold)
            {
                softTransparent++;
            }

            if (alpha <= TextureAlphaHeuristics.RegularTransparentAlphaThreshold)
            {
                regularTransparent++;
            }

            if (alpha <= TextureAlphaHeuristics.DeepTransparentAlphaThreshold)
            {
                deepTransparent++;
            }

            if (alpha >= TextureAlphaHeuristics.OpaqueAlphaThreshold)
            {
                opaque++;
            }
        }

        if (sampled == 0)
        {
            return false;
        }

        var opaqueRatio = opaque / (float)sampled;
        if (opaqueRatio < TextureAlphaHeuristics.MinOpaqueRatio)
        {
            return false;
        }

        var deepTransparentRatio = deepTransparent / (float)sampled;
        var denseDeepMaskThreshold = TextureAlphaHeuristics.GetDenseDeepMaskRatioThreshold();
        if (deepTransparentRatio > denseDeepMaskThreshold &&
            opaqueRatio >= TextureAlphaHeuristics.DenseDeepMaskOpaqueRatio)
        {
            return false;
        }

        if (deepTransparentRatio >= TextureAlphaHeuristics.MinDeepTransparentRatio)
        {
            return true;
        }

        var regularTransparentRatio = regularTransparent / (float)sampled;
        if (regularTransparentRatio >= TextureAlphaHeuristics.MinRegularTransparentRatio)
        {
            return true;
        }

        var softTransparentRatio = softTransparent / (float)sampled;
        return softTransparentRatio >= TextureAlphaHeuristics.MinSoftTransparentRatio;
    }
}
