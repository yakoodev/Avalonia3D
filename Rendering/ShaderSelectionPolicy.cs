using Avalonia3D.Interfaces;
using Avalonia3D.Model;
using Avalonia3D.Shaders;
using Silk.NET.OpenGL;
using System;

namespace Avalonia3D.Rendering;

public sealed class ShaderSelectionPolicy
{
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
        }

        return scene.ShaderRegistry.GetDefault(gl);
    }

    public static string ResolvePbrShaderId(Material material, Scene3D scene)
    {
        var features = BuildPbrFeatures(material, scene);
        return features switch
        {
            PbrFeatures.BaseColorMap => ShaderIds.PbrBaseColor,
            PbrFeatures.BaseColorMap | PbrFeatures.NormalMap => ShaderIds.PbrBaseColorNormal,
            PbrFeatures.BaseColorMap | PbrFeatures.NormalMap | PbrFeatures.MetallicRoughnessMap => ShaderIds.PbrBaseColorNormalMetallicRoughness,
            PbrFeatures.BaseColorMap | PbrFeatures.NormalMap | PbrFeatures.MetallicRoughnessMap | PbrFeatures.OcclusionMap | PbrFeatures.EmissiveMap => ShaderIds.PbrBaseColorNormalMetallicRoughnessAoEmissive,
            PbrFeatures.BaseColorMap | PbrFeatures.NormalMap | PbrFeatures.MetallicRoughnessMap | PbrFeatures.OcclusionMap | PbrFeatures.EmissiveMap | PbrFeatures.ReflectionsIbl => ShaderIds.PbrFull,
            _ => ShaderIds.Pbr
        };
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

        return features;
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
