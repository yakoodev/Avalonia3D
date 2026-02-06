using Avalonia3D.Interfaces;
using Avalonia3D.Model;
using Avalonia3D.Shaders;
using Silk.NET.OpenGL;

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

        var pbrShaderId = ResolvePbrShaderId(material, scene);
        if (pbrShaderId != null)
        {
            var byFeatures = scene.ShaderRegistry.Get(pbrShaderId, gl);
            if (byFeatures != null)
            {
                return byFeatures;
            }
        }

        if (!string.IsNullOrWhiteSpace(scene.ActiveShaderId))
        {
            var bySceneId = scene.ShaderRegistry.Get(scene.ActiveShaderId, gl);
            if (bySceneId != null)
            {
                return bySceneId;
            }
        }

        if (scene.RenderMode != ShaderRenderMode.Default)
        {
            var modeShaderId = scene.GetShaderIdForMode(scene.RenderMode);
            if (modeShaderId != null)
            {
                var byMode = scene.ShaderRegistry.Get(modeShaderId, gl);
                if (byMode != null)
                {
                    return byMode;
                }
            }
        }

        return scene.ShaderRegistry.GetDefault(gl);
    }

    public static string? ResolvePbrShaderId(Material? material, Scene3D scene)
    {
        if (material == null)
        {
            return null;
        }

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
}
