using Avalonia3D.Model;
using Avalonia3D.Rendering;
using Avalonia3D.Shaders;

namespace Avalonia3D.Composition;

public static class SceneShaderRegistryBootstrap
{
    public static void Configure(Scene3D scene, int maxLights)
    {
        scene.ShaderRegistry.Register(ShaderIds.Pbr, glContext => GLShader.Create(glContext, PbrFeatures.None, maxLights));
        scene.ShaderRegistry.Register(ShaderIds.PbrBaseColor, glContext => GLShader.Create(glContext, PbrFeatures.BaseColorMap, maxLights));
        scene.ShaderRegistry.Register(ShaderIds.PbrBaseColorNormal, glContext => GLShader.Create(glContext, PbrFeatures.BaseColorMap | PbrFeatures.NormalMap, maxLights));
        scene.ShaderRegistry.Register(ShaderIds.PbrBaseColorNormalMetallicRoughness, glContext => GLShader.Create(glContext, PbrFeatures.BaseColorMap | PbrFeatures.NormalMap | PbrFeatures.MetallicRoughnessMap, maxLights));
        scene.ShaderRegistry.Register(ShaderIds.PbrBaseColorNormalMetallicRoughnessAoEmissive, glContext => GLShader.Create(glContext, PbrFeatures.BaseColorMap | PbrFeatures.NormalMap | PbrFeatures.MetallicRoughnessMap | PbrFeatures.OcclusionMap | PbrFeatures.EmissiveMap, maxLights));
        scene.ShaderRegistry.Register(ShaderIds.PbrFull, glContext => GLShader.Create(glContext, PbrFeatures.BaseColorMap | PbrFeatures.NormalMap | PbrFeatures.MetallicRoughnessMap | PbrFeatures.OcclusionMap | PbrFeatures.EmissiveMap | PbrFeatures.ReflectionsIbl, maxLights));
        scene.ShaderRegistry.Register(ShaderIds.PbrTransmission, glContext => GLShader.Create(glContext, PbrFeatures.Transmission, maxLights));
        scene.ShaderRegistry.Register(ShaderIds.PbrFullTransmission, glContext => GLShader.Create(glContext, PbrFeatures.BaseColorMap | PbrFeatures.NormalMap | PbrFeatures.MetallicRoughnessMap | PbrFeatures.OcclusionMap | PbrFeatures.EmissiveMap | PbrFeatures.ReflectionsIbl | PbrFeatures.Transmission, maxLights));
        scene.ShaderRegistry.Register(ShaderIds.Unlit, UnlitShader.Create);
        scene.ShaderRegistry.Register(ShaderIds.NormalsDebug, NormalsDebugShader.Create);
        scene.ShaderRegistry.SetDefault(ShaderIds.Pbr);
        scene.ActiveShaderId = ShaderIds.Pbr;
        scene.BindRenderMode(ShaderRenderMode.Pbr, ShaderIds.Pbr);
        scene.BindRenderMode(ShaderRenderMode.Unlit, ShaderIds.Unlit);
        scene.BindRenderMode(ShaderRenderMode.NormalsDebug, ShaderIds.NormalsDebug);
    }
}
