using Avalonia3D.Interfaces;
using Avalonia3D.Model;
using Avalonia3D.Model.StandObjects;
using Avalonia3D.Rendering;
using Avalonia3D.Shaders;
using System.Numerics;
using Xunit;

namespace Avalonia3D.Tests;

[Trait("TestTarget", "DomainLogic")]
public class ShaderSelectionTests
{
    [Fact]
    public void Select_UsesMaterialOverride_AsHighestPriority()
    {
        var scene = CreateScene();
        scene.ActiveShaderId = "scene-default";
        var materialShader = new StubShader();
        var material = new Material { Shader = materialShader, ShaderId = "material-shader" };

        var selected = scene.ShaderSelectionPolicy.Select(material, scene, gl: null);

        Assert.Same(materialShader, selected);
    }

    [Fact]
    public void Select_UsesMaterialFeatureShader_WhenTextureCombinationMatchesRegisteredVariant()
    {
        var scene = CreateScene();
        var material = new Material
        {
            BaseColorTexture = new TextureData(),
            NormalTexture = new TextureData(),
            MetallicRoughnessTexture = new TextureData()
        };

        var selected = scene.ShaderSelectionPolicy.Select(material, scene, gl: null);

        Assert.Same(scene.ShaderRegistry.Get(ShaderIds.PbrBaseColorNormalMetallicRoughness), selected);
    }

    [Fact]
    public void Select_UsesFullFeatureShader_WhenAllMapsAndIblEnabled()
    {
        var scene = CreateScene();
        scene.EnvironmentLighting = scene.EnvironmentLighting with { ReflectionsEnabled = true, ReflectionMode = ReflectionMode.IBL };

        var material = new Material
        {
            BaseColorTexture = new TextureData(),
            NormalTexture = new TextureData(),
            MetallicRoughnessTexture = new TextureData(),
            OcclusionTexture = new TextureData(),
            EmissiveTexture = new TextureData()
        };

        var selected = scene.ShaderSelectionPolicy.Select(material, scene, gl: null);

        Assert.Same(scene.ShaderRegistry.Get(ShaderIds.PbrFull), selected);
    }

    [Fact]
    public void Select_FallsBackToSceneDefault_WhenFeatureShaderMissing()
    {
        var scene = new Scene3D();
        scene.ShaderRegistry.RegisterInstance("fallback", new StubShader());
        scene.ShaderRegistry.RegisterInstance("scene-default", new StubShader());
        scene.ShaderRegistry.SetDefault("fallback");
        scene.ActiveShaderId = "scene-default";

        var material = new Material
        {
            BaseColorTexture = new TextureData(),
            NormalTexture = new TextureData(),
            MetallicRoughnessTexture = new TextureData()
        };

        var selected = scene.ShaderSelectionPolicy.Select(material, scene, gl: null);

        Assert.Same(scene.ShaderRegistry.Get("scene-default"), selected);
    }

    [Fact]
    public void Select_UsesFallback_WhenNoMaterialAndNoSceneDefault()
    {
        var scene = CreateScene();
        scene.ActiveShaderId = null;

        var selected = scene.ShaderSelectionPolicy.Select(material: null, scene, gl: null);

        Assert.Same(scene.ShaderRegistry.GetDefault(), selected);
    }

    private static Scene3D CreateScene()
    {
        var scene = new Scene3D();
        scene.EnvironmentLighting = scene.EnvironmentLighting with { ReflectionsEnabled = false, ReflectionMode = ReflectionMode.Off };
        scene.ShaderRegistry.RegisterInstance("fallback", new StubShader());
        scene.ShaderRegistry.RegisterInstance("material-shader", new StubShader());
        scene.ShaderRegistry.RegisterInstance("scene-default", new StubShader());
        scene.ShaderRegistry.RegisterInstance(ShaderIds.Pbr, new StubShader());
        scene.ShaderRegistry.RegisterInstance(ShaderIds.PbrBaseColor, new StubShader());
        scene.ShaderRegistry.RegisterInstance(ShaderIds.PbrBaseColorNormal, new StubShader());
        scene.ShaderRegistry.RegisterInstance(ShaderIds.PbrBaseColorNormalMetallicRoughness, new StubShader());
        scene.ShaderRegistry.RegisterInstance(ShaderIds.PbrBaseColorNormalMetallicRoughnessAoEmissive, new StubShader());
        scene.ShaderRegistry.RegisterInstance(ShaderIds.PbrFull, new StubShader());
        scene.ShaderRegistry.SetDefault("fallback");
        return scene;
    }

    private sealed class StubShader : IShader3D
    {
        public uint Handle => 0;
        public void BindMaterial(RenderResources resources, Material? material, uint? shadowMapId = null) { }
        public void Dispose() { }
        public void SetUniforms(IRenderContext renderContext, SceneObject sceneObject, Matrix4x4 lightSpaceMatrix = default) { }
        public void Use() { }
    }
}
