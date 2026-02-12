using Avalonia3D.Interfaces;
using Avalonia3D.Model;
using Avalonia3D.Shaders;
using Serilog;
using Silk.NET.OpenGL;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace Avalonia3D.Rendering;

public sealed class ShaderSelectionPolicy
{
    private readonly IPbrVariantReducer _pbrVariantReducer;
    private readonly IRuntimePbrShaderFactory _runtimePbrShaderFactory;

    public ShaderSelectionPolicy(IPbrVariantReducer? pbrVariantReducer = null, IRuntimePbrShaderFactory? runtimePbrShaderFactory = null)
    {
        _pbrVariantReducer = pbrVariantReducer ?? new DefaultPbrVariantReducer();
        _runtimePbrShaderFactory = runtimePbrShaderFactory ?? new RuntimePbrShaderFactory();
    }

    public IShader3D? Select(Material? material, Scene3D scene, GL? gl)
    {
        if (material?.Shader is IShader3D explicitShader)
        {
            return explicitShader;
        }

        if (material?.ShaderId is { Length: > 0 } materialShaderId)
        {
            var byMaterialId = scene.ShaderRegistry.Get(materialShaderId, gl);
            if (byMaterialId != null)
            {
                return byMaterialId;
            }
        }

        var requestedShaderId = ResolveRequestedShaderId(scene);
        if (requestedShaderId != null)
        {
            if (IsPbrShaderId(requestedShaderId) && material != null)
            {
                var featureShaderId = ResolvePbrShaderId(material, scene);
                var byFeatures = scene.ShaderRegistry.Get(featureShaderId, gl);
                if (byFeatures != null)
                {
                    return byFeatures;
                }

                var dynamicPbrShader = TryCreateRuntimePbrVariant(featureShaderId, scene, gl, _pbrVariantReducer, _runtimePbrShaderFactory);
                if (dynamicPbrShader != null)
                {
                    return dynamicPbrShader;
                }
            }

            var byRequestedId = scene.ShaderRegistry.Get(requestedShaderId, gl);
            if (byRequestedId != null)
            {
                return byRequestedId;
            }
        }

        if (material != null)
        {
            var featureShaderId = ResolvePbrShaderId(material, scene);
            var byFeatures = scene.ShaderRegistry.Get(featureShaderId, gl);
            if (byFeatures != null)
            {
                return byFeatures;
            }

            var dynamicPbrShader = TryCreateRuntimePbrVariant(featureShaderId, scene, gl, _pbrVariantReducer, _runtimePbrShaderFactory);
            if (dynamicPbrShader != null)
            {
                return dynamicPbrShader;
            }
        }

        return scene.ShaderRegistry.GetDefault(gl);
    }

    public static string ResolvePbrShaderId(Material material, Scene3D scene)
    {
        var features = BuildPbrFeatures(material, scene);

        if (TryResolveLegacyPbrShaderId(features, out var legacyShaderId))
        {
            return legacyShaderId;
        }

        return features == PbrFeatures.None
            ? ShaderIds.Pbr
            : ShaderIds.CreatePbrVariantId(features);
    }

    public static PbrFeatures BuildPbrFeatures(Material material, Scene3D scene)
    {
        var features = material.Features;

        if (material.BaseColorTexture != null)
        {
            features |= PbrFeatures.BaseColorMap;
        }

        if (material.NormalTexture != null)
        {
            features |= PbrFeatures.NormalMap;
        }

        if (material.MetallicRoughnessTexture != null)
        {
            features |= PbrFeatures.MetallicRoughnessMap;
        }

        if (material.OcclusionTexture != null)
        {
            features |= PbrFeatures.OcclusionMap;
        }

        if (material.EmissiveTexture != null)
        {
            features |= PbrFeatures.EmissiveMap;
        }

        if (scene.EnvironmentLighting.ReflectionsEnabled && scene.EnvironmentLighting.ReflectionMode == ReflectionMode.IBL)
        {
            features |= PbrFeatures.ReflectionsIbl;
        }

        if (material.HasTransmission && material.TransmissionFactor > 0.001f)
        {
            features |= PbrFeatures.Transmission;
        }

        if (material.ClearcoatFactor > 0.001f)
        {
            features |= PbrFeatures.Clearcoat;
        }

        if (material.SheenColorFactor.LengthSquared() > 0.0001f || material.SheenRoughnessFactor > 0.001f)
        {
            features |= PbrFeatures.Sheen;
        }

        if (Math.Abs(material.SpecularFactor - 1f) > 0.0001f || Vector3.DistanceSquared(material.SpecularColorFactor, Vector3.One) > 0.0001f)
        {
            features |= PbrFeatures.Specular;
        }

        if (Math.Abs(material.Ior - 1.5f) > 0.0001f)
        {
            features |= PbrFeatures.Ior;
        }

        if (Math.Abs(material.EmissiveStrength - 1f) > 0.0001f)
        {
            features |= PbrFeatures.EmissiveStrength;
        }

        if (material.ExtensionTextures.ClearcoatTexture != null)
        {
            features |= PbrFeatures.ClearcoatMap;
        }

        if (material.ExtensionTextures.ClearcoatRoughnessTexture != null)
        {
            features |= PbrFeatures.ClearcoatRoughnessMap;
        }

        if (material.ExtensionTextures.ClearcoatNormalTexture != null)
        {
            features |= PbrFeatures.ClearcoatNormalMap;
        }

        if (material.ExtensionTextures.SheenColorTexture != null)
        {
            features |= PbrFeatures.SheenColorMap;
        }

        if (material.ExtensionTextures.SheenRoughnessTexture != null)
        {
            features |= PbrFeatures.SheenRoughnessMap;
        }

        if (material.ExtensionTextures.SpecularTexture != null)
        {
            features |= PbrFeatures.SpecularMap;
        }

        if (material.ExtensionTextures.SpecularColorTexture != null)
        {
            features |= PbrFeatures.SpecularColorMap;
        }

        if (material.ExtensionTextures.TransmissionTexture != null)
        {
            features |= PbrFeatures.TransmissionMap;
        }

        if (material.ExtensionTextures.VolumeThicknessTexture != null)
        {
            features |= PbrFeatures.VolumeThicknessMap;
        }

        return features;
    }

    private static bool TryResolveLegacyPbrShaderId(PbrFeatures features, out string shaderId)
    {
        shaderId = ShaderIds.Pbr;

        var legacyMask = PbrFeatures.BaseColorMap |
                         PbrFeatures.NormalMap |
                         PbrFeatures.MetallicRoughnessMap |
                         PbrFeatures.OcclusionMap |
                         PbrFeatures.EmissiveMap |
                         PbrFeatures.ReflectionsIbl |
                         PbrFeatures.Transmission;

        var legacyFeatures = features & legacyMask;
        var normalizedLegacyFeatures = NormalizeLegacyFeaturesForStaticShaders(legacyFeatures);
        var isTransmission = normalizedLegacyFeatures.HasFlag(PbrFeatures.Transmission);
        var hasCoreMaps = HasAll(normalizedLegacyFeatures, PbrFeatures.BaseColorMap, PbrFeatures.NormalMap, PbrFeatures.MetallicRoughnessMap);
        var hasAoEmissive = HasAll(normalizedLegacyFeatures, PbrFeatures.OcclusionMap, PbrFeatures.EmissiveMap);
        var hasIbl = normalizedLegacyFeatures.HasFlag(PbrFeatures.ReflectionsIbl);

        if (isTransmission && hasCoreMaps && hasAoEmissive && hasIbl)
        {
            shaderId = ShaderIds.PbrFullTransmission;
            return true;
        }

        if (isTransmission)
        {
            shaderId = ShaderIds.PbrTransmission;
            return true;
        }

        shaderId = normalizedLegacyFeatures switch
        {
            PbrFeatures.None => ShaderIds.Pbr,
            PbrFeatures.BaseColorMap => ShaderIds.PbrBaseColor,
            PbrFeatures.BaseColorMap | PbrFeatures.NormalMap => ShaderIds.PbrBaseColorNormal,
            PbrFeatures.BaseColorMap | PbrFeatures.NormalMap | PbrFeatures.MetallicRoughnessMap => ShaderIds.PbrBaseColorNormalMetallicRoughness,
            PbrFeatures.BaseColorMap | PbrFeatures.NormalMap | PbrFeatures.MetallicRoughnessMap | PbrFeatures.OcclusionMap | PbrFeatures.EmissiveMap => ShaderIds.PbrBaseColorNormalMetallicRoughnessAoEmissive,
            PbrFeatures.BaseColorMap | PbrFeatures.NormalMap | PbrFeatures.MetallicRoughnessMap | PbrFeatures.OcclusionMap | PbrFeatures.EmissiveMap | PbrFeatures.ReflectionsIbl => ShaderIds.PbrFull,
            _ => string.Empty
        };

        if (string.IsNullOrEmpty(shaderId) && hasCoreMaps)
        {
            shaderId = hasIbl
                ? ShaderIds.PbrFull
                : ShaderIds.PbrBaseColorNormalMetallicRoughnessAoEmissive;
        }

        return !string.IsNullOrEmpty(shaderId);
    }


    private static PbrFeatures NormalizeLegacyFeaturesForStaticShaders(PbrFeatures features)
    {
        var hasBaseMetallic = features.HasFlag(PbrFeatures.BaseColorMap) && features.HasFlag(PbrFeatures.MetallicRoughnessMap);
        if (hasBaseMetallic && !features.HasFlag(PbrFeatures.NormalMap))
        {
            // Static legacy registry has no "base+metallic without normal" variant.
            // Reuse the normal-capable shader and let uHasNormalMap=0 disable sampling at runtime.
            features |= PbrFeatures.NormalMap;
        }

        return features;
    }
    private static IShader3D? TryCreateRuntimePbrVariant(string shaderId, Scene3D scene, GL? gl, IPbrVariantReducer pbrVariantReducer, IRuntimePbrShaderFactory runtimePbrShaderFactory)
    {
        if (!ShaderIds.TryParsePbrVariantId(shaderId, out var features))
        {
            return null;
        }

        var maxLights = scene.ActiveGraphicsProfile.MaxLights;

        foreach (var candidate in EnumerateRuntimeCandidates(features, pbrVariantReducer))
        {
            var candidateShaderId = candidate == features ? shaderId : ShaderIds.CreatePbrVariantId(candidate);
            var cached = scene.ShaderRegistry.Get(candidateShaderId, gl);
            if (cached != null)
            {
                if (candidate != features)
                {
                    Log.Warning("Using reduced runtime PBR shader variant '{ShaderId}' instead of requested '{RequestedShaderId}'. requestedFeatures={RequestedFeatures}, reducedFeatures={ReducedFeatures}, maxLights={MaxLights}", candidateShaderId, shaderId, features, candidate, maxLights);
                }

                return cached;
            }

            try
            {
                var shader = runtimePbrShaderFactory.Create(gl, candidate, maxLights);
                scene.ShaderRegistry.RegisterInstance(candidateShaderId, shader);

                if (candidate != features)
                {
                    Log.Warning("Runtime PBR shader fallback succeeded. requestedShaderId={RequestedShaderId}, fallbackShaderId={FallbackShaderId}, requestedFeatures={RequestedFeatures}, fallbackFeatures={FallbackFeatures}, maxLights={MaxLights}", shaderId, candidateShaderId, features, candidate, maxLights);
                }

                return shader;
            }
            catch (Exception exception)
            {
                Log.Warning(exception, "Runtime PBR shader creation failed. shaderId={ShaderId}, features={Features}, maxLights={MaxLights}", candidateShaderId, candidate, maxLights);
            }
        }

        var pbrFallback = scene.ShaderRegistry.Get(ShaderIds.Pbr, gl);
        if (pbrFallback != null)
        {
            Log.Warning("Using static PBR fallback after runtime shader compilation failures. requestedShaderId={RequestedShaderId}, features={Features}, maxLights={MaxLights}", shaderId, features, maxLights);
            return pbrFallback;
        }

        Log.Warning("Using default shader fallback after runtime shader compilation failures. requestedShaderId={RequestedShaderId}, features={Features}, maxLights={MaxLights}", shaderId, features, maxLights);
        return scene.ShaderRegistry.GetDefault(gl);
    }

    private static IEnumerable<PbrFeatures> EnumerateRuntimeCandidates(PbrFeatures features, IPbrVariantReducer reducer)
    {
        yield return features;

        foreach (var reduced in reducer.GetReductionChain(features))
        {
            if (reduced == features)
            {
                continue;
            }

            yield return reduced;
        }
    }

    private static string? ResolveRequestedShaderId(Scene3D scene)
    {
        if (scene.RenderMode != ShaderRenderMode.Default)
        {
            return scene.GetShaderIdForMode(scene.RenderMode);
        }

        return string.IsNullOrWhiteSpace(scene.ActiveShaderId) ? null : scene.ActiveShaderId;
    }

    private static bool IsPbrShaderId(string shaderId)
    {
        return shaderId.StartsWith(ShaderIds.Pbr, StringComparison.Ordinal);
    }

    private static bool HasAll(PbrFeatures value, params PbrFeatures[] flags)
    {
        foreach (var flag in flags)
        {
            if (!value.HasFlag(flag))
            {
                return false;
            }
        }

        return true;
    }
}
