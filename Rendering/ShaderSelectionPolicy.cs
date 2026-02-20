using Avalonia3D.Interfaces;
using Avalonia3D.Model;
using Avalonia3D.Shaders;
using Serilog;
using Silk.NET.OpenGL;
using System;
using System.Collections.Generic;
using System.Linq;
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
                var materialFeatures = BuildMaterialFeatureSet(material, scene);
                var featureShaderId = ShaderIds.CreatePbrVariantId(materialFeatures.ToPbrFeatures());
                var byFeatures = scene.ShaderRegistry.Get(featureShaderId, gl);
                if (byFeatures != null)
                {
                    return byFeatures;
                }

                var dynamicPbrShader = TryCreateRuntimePbrVariant(featureShaderId, materialFeatures, scene, gl, _pbrVariantReducer, _runtimePbrShaderFactory);
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
            var materialFeatures = BuildMaterialFeatureSet(material, scene);
            var featureShaderId = ShaderIds.CreatePbrVariantId(materialFeatures.ToPbrFeatures());
            var byFeatures = scene.ShaderRegistry.Get(featureShaderId, gl);
            if (byFeatures != null)
            {
                return byFeatures;
            }

            var dynamicPbrShader = TryCreateRuntimePbrVariant(featureShaderId, materialFeatures, scene, gl, _pbrVariantReducer, _runtimePbrShaderFactory);
            if (dynamicPbrShader != null)
            {
                return dynamicPbrShader;
            }
        }

        return scene.ShaderRegistry.GetDefault(gl);
    }

    public static string ResolvePbrShaderId(Material material, Scene3D scene)
    {
        var features = BuildMaterialFeatureSet(material, scene);
        return ShaderIds.CreatePbrVariantId(features.ToPbrFeatures());
    }

    public static PbrFeatures BuildPbrFeatures(Material material, Scene3D scene)
    {
        return BuildMaterialFeatureSet(material, scene).ToPbrFeatures();
    }

    public static MaterialFeatureSet BuildMaterialFeatureSet(Material material, Scene3D scene)
    {
        var features = material.Features.ToMaterialFeatureSet();

        if (material.BaseColorTexture != null)
        {
            features |= MaterialFeatureSet.BaseColorMap;
        }

        if (material.NormalTexture != null)
        {
            features |= MaterialFeatureSet.NormalMap;
        }

        if (material.MetallicRoughnessTexture != null)
        {
            features |= MaterialFeatureSet.MetallicRoughnessMap;
        }

        if (material.OcclusionTexture != null)
        {
            features |= MaterialFeatureSet.OcclusionMap;
        }

        if (material.EmissiveTexture != null)
        {
            features |= MaterialFeatureSet.EmissiveMap;
        }

        if (scene.EnvironmentLighting.ReflectionsEnabled && scene.EnvironmentLighting.ReflectionMode == ReflectionMode.IBL)
        {
            features |= MaterialFeatureSet.ReflectionsIbl;
        }

        if (material.HasTransmission && material.TransmissionFactor > 0.001f)
        {
            features |= MaterialFeatureSet.Transmission;
        }

        if (material.ClearcoatFactor > 0.001f)
        {
            features |= MaterialFeatureSet.Clearcoat;
        }

        if (material.SheenColorFactor.LengthSquared() > 0.0001f || material.SheenRoughnessFactor > 0.001f)
        {
            features |= MaterialFeatureSet.Sheen;
        }

        if (Math.Abs(material.SpecularFactor - 1f) > 0.0001f || Vector3.DistanceSquared(material.SpecularColorFactor, Vector3.One) > 0.0001f)
        {
            features |= MaterialFeatureSet.Specular;
        }

        if (Math.Abs(material.Ior - 1.5f) > 0.0001f)
        {
            features |= MaterialFeatureSet.Ior;
        }

        if (Math.Abs(material.EmissiveStrength - 1f) > 0.0001f)
        {
            features |= MaterialFeatureSet.EmissiveStrength;
        }

        if (material.ExtensionTextures.ClearcoatTexture != null)
        {
            features |= MaterialFeatureSet.ClearcoatMap;
        }

        if (material.ExtensionTextures.ClearcoatRoughnessTexture != null)
        {
            features |= MaterialFeatureSet.ClearcoatRoughnessMap;
        }

        if (material.ExtensionTextures.ClearcoatNormalTexture != null)
        {
            features |= MaterialFeatureSet.ClearcoatNormalMap;
        }

        if (material.ExtensionTextures.SheenColorTexture != null)
        {
            features |= MaterialFeatureSet.SheenColorMap;
        }

        if (material.ExtensionTextures.SheenRoughnessTexture != null)
        {
            features |= MaterialFeatureSet.SheenRoughnessMap;
        }

        if (material.ExtensionTextures.SpecularTexture != null)
        {
            features |= MaterialFeatureSet.SpecularMap;
        }

        if (material.ExtensionTextures.SpecularColorTexture != null)
        {
            features |= MaterialFeatureSet.SpecularColorMap;
        }

        if (material.ExtensionTextures.TransmissionTexture != null)
        {
            features |= MaterialFeatureSet.TransmissionMap;
        }

        if (material.ExtensionTextures.VolumeThicknessTexture != null)
        {
            features |= MaterialFeatureSet.VolumeThicknessMap;
        }

        return features;
    }

    private static IShader3D? TryCreateRuntimePbrVariant(string shaderId, MaterialFeatureSet requestedMaterialFeatures, Scene3D scene, GL? gl, IPbrVariantReducer pbrVariantReducer, IRuntimePbrShaderFactory runtimePbrShaderFactory)
    {
        if (!ShaderIds.TryParsePbrVariantId(shaderId, out var features))
        {
            return null;
        }

        var maxLights = scene.ActiveGraphicsProfile.MaxLights;
        var capabilities = scene.ActiveGraphicsProfile.RenderCapabilities;

        foreach (var candidate in EnumerateRuntimeCandidates(features, pbrVariantReducer))
        {
            var candidateMaterialFeatures = candidate.ToMaterialFeatureSet();
            var unsupportedByProfile = candidateMaterialFeatures & ~capabilities.SupportedMaterialFeatures;
            if (unsupportedByProfile != MaterialFeatureSet.None)
            {
                LogFeatureMismatch(requestedMaterialFeatures, capabilities.SupportedMaterialFeatures, unsupportedByProfile, shaderId, ShaderIds.CreatePbrVariantId(candidate));
                continue;
            }

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
                var shader = runtimePbrShaderFactory.Create(gl, candidateMaterialFeatures, maxLights, capabilities);
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

    private static void LogFeatureMismatch(MaterialFeatureSet requestedFeatures, MaterialFeatureSet supportedFeatures, MaterialFeatureSet unsupportedFeatures, string requestedShaderId, string attemptedShaderId)
    {
        var names = Enum.GetValues<MaterialFeatureSet>()
            .Where(flag => flag != MaterialFeatureSet.None && unsupportedFeatures.HasFlag(flag))
            .Select(flag => flag.ToString())
            .ToArray();

        Log.Warning("Shader feature mismatch: материал требует feature {UnsupportedFeatures}, профиль/шейдер не поддерживает. requestedShaderId={RequestedShaderId}, attemptedShaderId={AttemptedShaderId}, requestedFeatures={RequestedFeatures}, supportedFeatures={SupportedFeatures}", string.Join(", ", names), requestedShaderId, attemptedShaderId, requestedFeatures, supportedFeatures);
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
}
