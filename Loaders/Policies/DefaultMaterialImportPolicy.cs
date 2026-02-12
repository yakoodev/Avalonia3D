using Avalonia3D.Model;
using Avalonia3D.Rendering;
using Serilog;
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

        var sourceAlphaMode = context.SourceAlphaMode;
        var sceneOverride = context.SceneOverride;
        MaterialAlphaMode resolved;
        var reasonCode = "default";

        if (sceneOverride?.ForceAlphaMode.HasValue == true)
        {
            resolved = sceneOverride.ForceAlphaMode.Value;
            reasonCode = "scene_force_alpha_mode";
            LogAlphaDecision(material, context, sourceAlphaMode, resolved, reasonCode);
            return resolved;
        }

        if (material.AlphaMode != MaterialAlphaMode.Blend)
        {
            resolved = material.AlphaMode;
            reasonCode = "source_non_blend";
            LogAlphaDecision(material, context, sourceAlphaMode, resolved, reasonCode);
            return resolved;
        }

        var alphaProfile = sceneOverride?.AlphaProfile ?? context.AlphaProfile;
        var textureSignal = ResolveTextureSignal(material.BaseColorTexture, sourceAlphaMode, sceneOverride, alphaProfile);
        var hasAlphaSignal = material.BaseColorFactor.W < AlphaSignalThreshold || textureSignal != TextureAlphaSignal.None;
        material.HasTextureTransparency = textureSignal != TextureAlphaSignal.None;

        if (hasAlphaSignal)
        {
            resolved = textureSignal == TextureAlphaSignal.MaskCutout
                ? MaterialAlphaMode.Mask
                : MaterialAlphaMode.Blend;
            reasonCode = material.BaseColorFactor.W < AlphaSignalThreshold
                ? "base_color_alpha_signal"
                : textureSignal == TextureAlphaSignal.MaskCutout
                    ? "texture_alpha_mask_cutout"
                    : "texture_alpha_signal";
            LogAlphaDecision(material, context, sourceAlphaMode, resolved, reasonCode);
            return resolved;
        }

        if (alphaProfile == MaterialAlphaImportProfile.Strict)
        {
            resolved = MaterialAlphaMode.Blend;
            reasonCode = "profile_strict_preserve_blend";
            LogAlphaDecision(material, context, sourceAlphaMode, resolved, reasonCode);
            return resolved;
        }

        var emissiveBehavior = ResolveEmissiveBehavior(material, context);
        if (emissiveBehavior == MaterialEmissiveBehavior.TreatAsTransparencySignal)
        {
            resolved = MaterialAlphaMode.Blend;
            reasonCode = context.IsAnimatedMaterial
                ? "animated_emissive_contract"
                : $"profile_{alphaProfile.ToString().ToLowerInvariant()}_emissive_signal";
            LogAlphaDecision(material, context, sourceAlphaMode, resolved, reasonCode);
            return resolved;
        }

        resolved = MaterialAlphaMode.Opaque;
        reasonCode = "fallback_opaque";
        LogAlphaDecision(material, context, sourceAlphaMode, resolved, reasonCode);
        return resolved;
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

    private static void LogAlphaDecision(
        Material material,
        MaterialImportPolicyContext context,
        MaterialAlphaMode sourceAlphaMode,
        MaterialAlphaMode resolvedAlphaMode,
        string reasonCode)
    {
        var hasEmissiveTexture = material.EmissiveTexture != null;
        var hasElevatedEmissiveStrength = material.EmissiveStrength > EmissiveStrengthThreshold;
        var debugLabel = BuildDebugLabel(context);

        Log.Debug(
            "Material import alpha policy: {DebugLabel}. sourceAlphaMode={SourceAlphaMode}, resolvedAlphaMode={ResolvedAlphaMode}, hasTextureTransparency={HasTextureTransparency}, hasEmissiveTexture={HasEmissiveTexture}, hasElevatedEmissiveStrength={HasElevatedEmissiveStrength}, reasonCode={ReasonCode}",
            debugLabel,
            sourceAlphaMode,
            resolvedAlphaMode,
            material.HasTextureTransparency,
            hasEmissiveTexture,
            hasElevatedEmissiveStrength,
            reasonCode);
    }

    private static string BuildDebugLabel(MaterialImportPolicyContext context)
    {
        if (!context.IsAnimatedMaterial &&
            !ContainsDroid(context.AssetPath) &&
            !ContainsDroid(context.MaterialName) &&
            !ContainsDroid(context.MeshName) &&
            !ContainsDroid(context.NodeName))
        {
            return string.Empty;
        }

        var material = string.IsNullOrWhiteSpace(context.MaterialName) ? "material:?" : $"material:{context.MaterialName}";
        var mesh = string.IsNullOrWhiteSpace(context.MeshName) ? "mesh:?" : $"mesh:{context.MeshName}";
        var node = string.IsNullOrWhiteSpace(context.NodeStableId)
            ? (string.IsNullOrWhiteSpace(context.NodeName) ? "node:?" : $"node:{context.NodeName}")
            : context.NodeStableId.StartsWith("node:", StringComparison.OrdinalIgnoreCase)
                ? context.NodeStableId
                : $"node:{context.NodeStableId}";
        var type = context.IsAnimatedMaterial ? "animated" : "droid";

        return $"[{type}] {material}, {mesh}, {node}";
    }

    private static bool ContainsDroid(string? value)
    {
        return !string.IsNullOrWhiteSpace(value) && value.Contains("droid", StringComparison.OrdinalIgnoreCase);
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
        public const float StrictMinSoftTransparentRatio = 0.15f;
        public const float BalancedMinSoftTransparentRatio = 0.01f;
        public const float PermissiveMinSoftTransparentRatio = 0.001f;

        public const float DenseDeepMaskOpaqueRatio = 0.35f;
        public const float DenseDeepMaskSoftTransparentCeiling = 0.03f;
        public const float DenseDeepMaskRegularTransparentCeiling = 0.03f;
        public const float StrictDenseDeepMaskRatio = 0.15f;
        public const float BalancedDenseDeepMaskRatio = 0.20f;
        public const float PermissiveDenseDeepMaskRatio = 0.35f;

        public static float GetDenseDeepMaskRatioThreshold(TextureAlphaHeuristicProfile profile)
        {
            return profile switch
            {
                TextureAlphaHeuristicProfile.Strict => StrictDenseDeepMaskRatio,
                TextureAlphaHeuristicProfile.Permissive => PermissiveDenseDeepMaskRatio,
                _ => BalancedDenseDeepMaskRatio,
            };
        }

        public static float GetMinSoftTransparentRatio(TextureAlphaHeuristicProfile profile)
        {
            return profile switch
            {
                TextureAlphaHeuristicProfile.Strict => StrictMinSoftTransparentRatio,
                TextureAlphaHeuristicProfile.Permissive => PermissiveMinSoftTransparentRatio,
                _ => BalancedMinSoftTransparentRatio,
            };
        }
    }


    private enum TextureAlphaSignal
    {
        None,
        Blend,
        MaskCutout
    }

    private static TextureAlphaSignal ResolveTextureSignal(
        TextureData? texture,
        MaterialAlphaMode sourceAlphaMode,
        MaterialSceneImportOverride? sceneOverride,
        MaterialAlphaImportProfile alphaProfile)
    {
        if (sceneOverride?.ForceTextureTransparencySignal.HasValue == true)
        {
            return sceneOverride.ForceTextureTransparencySignal.Value
                ? TextureAlphaSignal.Blend
                : TextureAlphaSignal.None;
        }

        if (sourceAlphaMode != MaterialAlphaMode.Blend)
        {
            return HasMeaningfulTextureTransparency(texture)
                ? TextureAlphaSignal.Blend
                : TextureAlphaSignal.None;
        }

        return ClassifyBlendSourceTextureTransparency(texture, ToHeuristicProfile(alphaProfile));
    }

    private static TextureAlphaSignal ClassifyBlendSourceTextureTransparency(
        TextureData? texture,
        TextureAlphaHeuristicProfile heuristicProfile)
    {
        if (texture?.Data == null || texture.Data.Length < 4)
        {
            return TextureAlphaSignal.None;
        }

        var data = texture.Data;
        var sampled = 0;
        var softTransparent = 0;
        var regularTransparent = 0;
        var deepTransparent = 0;
        var opaque = 0;
        var anyTransparent = 0;

        var pixelCount = data.Length / 4;
        var stepPixels = Math.Max(1, pixelCount / TextureAlphaHeuristics.MaxSamples);
        var step = stepPixels * 4;

        for (var i = 3; i < data.Length; i += step)
        {
            sampled++;
            var alpha = data[i];

            if (alpha <= TextureAlphaHeuristics.DeepTransparentAlphaThreshold)
            {
                deepTransparent++;
            }
            else
            {
                if (alpha <= TextureAlphaHeuristics.RegularTransparentAlphaThreshold)
                {
                    regularTransparent++;
                }

                if (alpha <= TextureAlphaHeuristics.SoftTransparentAlphaThreshold)
                {
                    softTransparent++;
                }
            }

            if (alpha >= TextureAlphaHeuristics.OpaqueAlphaThreshold)
            {
                opaque++;
            }
            else
            {
                anyTransparent++;
            }
        }

        if (sampled == 0)
        {
            return TextureAlphaSignal.None;
        }

        var opaqueRatio = opaque / (float)sampled;
        var softTransparentRatio = softTransparent / (float)sampled;
        if (softTransparentRatio < TextureAlphaHeuristics.GetMinSoftTransparentRatio(heuristicProfile) &&
            anyTransparent == 0)
        {
            return TextureAlphaSignal.None;
        }

        var deepTransparentRatio = deepTransparent / (float)sampled;
        var denseDeepMaskThreshold = TextureAlphaHeuristics.GetDenseDeepMaskRatioThreshold(heuristicProfile);

        var regularTransparentRatio = regularTransparent / (float)sampled;

        var shouldTreatAsCutout =
            deepTransparentRatio > denseDeepMaskThreshold &&
            opaqueRatio >= TextureAlphaHeuristics.MinOpaqueRatio &&
            softTransparentRatio <= TextureAlphaHeuristics.DenseDeepMaskSoftTransparentCeiling &&
            regularTransparentRatio <= TextureAlphaHeuristics.DenseDeepMaskRegularTransparentCeiling;

        if (shouldTreatAsCutout)
        {
            return TextureAlphaSignal.MaskCutout;
        }

        return deepTransparentRatio >= TextureAlphaHeuristics.MinDeepTransparentRatio ||
               regularTransparentRatio >= TextureAlphaHeuristics.MinRegularTransparentRatio ||
               anyTransparent > 0
            ? TextureAlphaSignal.Blend
            : TextureAlphaSignal.None;
    }

    private static TextureAlphaHeuristicProfile ToHeuristicProfile(MaterialAlphaImportProfile alphaProfile)
    {
        return alphaProfile switch
        {
            MaterialAlphaImportProfile.Strict => TextureAlphaHeuristicProfile.Strict,
            MaterialAlphaImportProfile.Legacy => TextureAlphaHeuristicProfile.Permissive,
            _ => TextureAlphaHeuristicProfile.Balanced,
        };
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

            if (alpha <= TextureAlphaHeuristics.DeepTransparentAlphaThreshold)
            {
                deepTransparent++;
            }
            else
            {
                if (alpha <= TextureAlphaHeuristics.RegularTransparentAlphaThreshold)
                {
                    regularTransparent++;
                }

                if (alpha <= TextureAlphaHeuristics.SoftTransparentAlphaThreshold)
                {
                    softTransparent++;
                }
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
        var denseDeepMaskThreshold = TextureAlphaHeuristics.GetDenseDeepMaskRatioThreshold(TextureAlphaHeuristicProfile.Balanced);
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
        return softTransparentRatio >= TextureAlphaHeuristics.GetMinSoftTransparentRatio(TextureAlphaHeuristicProfile.Balanced);
    }
}
