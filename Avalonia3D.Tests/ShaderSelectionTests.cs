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
        var materialShader = new StubShader();
        var material = new Material { Shader = materialShader, ShaderId = "material-shader" };

        var selected = scene.ShaderSelectionPolicy.Select(material, scene, gl: null);

        Assert.Same(materialShader, selected);
    }

    [Fact]
    public void Select_UsesRenderModeShader_WhenRenderModeIsNotPbr()
    {
        var scene = CreateScene();
        scene.BindRenderMode(ShaderRenderMode.Unlit, ShaderIds.Unlit);
        scene.RenderMode = ShaderRenderMode.Unlit;

        var material = new Material
        {
            BaseColorTexture = new TextureData(),
            NormalTexture = new TextureData(),
            MetallicRoughnessTexture = new TextureData()
        };

        var selected = scene.ShaderSelectionPolicy.Select(material, scene, gl: null);

        Assert.Same(scene.ShaderRegistry.Get(ShaderIds.Unlit), selected);
    }

    [Fact]
    public void Select_UsesMaterialFeatureShader_WhenPbrModeAndTextureCombinationMatchesVariant()
    {
        var scene = CreateScene();
        scene.BindRenderMode(ShaderRenderMode.Pbr, ShaderIds.Pbr);
        scene.RenderMode = ShaderRenderMode.Pbr;

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
    public void Select_UsesTransmissionShader_WhenMaterialHasTransmission()
    {
        var scene = CreateScene();
        scene.BindRenderMode(ShaderRenderMode.Pbr, ShaderIds.Pbr);
        scene.RenderMode = ShaderRenderMode.Pbr;

        var material = new Material
        {
            HasTransmission = true,
            TransmissionFactor = 0.9f
        };

        var selected = scene.ShaderSelectionPolicy.Select(material, scene, gl: null);

        Assert.Same(scene.ShaderRegistry.Get(ShaderIds.PbrTransmission), selected);
    }

    [Fact]
    public void Select_UsesFullTransmissionShader_WhenMaterialHasTransmissionAndFullFeatureSet()
    {
        var scene = CreateScene();
        scene.EnvironmentLighting = scene.EnvironmentLighting with { ReflectionsEnabled = true, ReflectionMode = ReflectionMode.IBL };

        var material = new Material
        {
            BaseColorTexture = new TextureData(),
            NormalTexture = new TextureData(),
            MetallicRoughnessTexture = new TextureData(),
            OcclusionTexture = new TextureData(),
            EmissiveTexture = new TextureData(),
            HasTransmission = true,
            TransmissionFactor = 1f
        };

        var selected = scene.ShaderSelectionPolicy.Select(material, scene, gl: null);

        Assert.Same(scene.ShaderRegistry.Get(ShaderIds.PbrFullTransmission), selected);
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
    public void ResolvePbrShaderId_ReturnsDynamicVariant_ForEmissiveAndMetallicWithoutNormalMap()
    {
        var scene = CreateScene();
        var material = new Material
        {
            BaseColorTexture = new TextureData(),
            MetallicRoughnessTexture = new TextureData(),
            EmissiveTexture = new TextureData()
        };

        var shaderId = ShaderSelectionPolicy.ResolvePbrShaderId(material, scene);

        Assert.StartsWith(ShaderIds.PbrVariantPrefix, shaderId);
        Assert.True(ShaderIds.TryParsePbrVariantId(shaderId, out var features));
        Assert.True(features.HasFlag(PbrFeatures.MetallicRoughnessMap));
        Assert.True(features.HasFlag(PbrFeatures.EmissiveMap));
        Assert.False(features.HasFlag(PbrFeatures.NormalMap));
    }

    [Fact]
    public void ResolvePbrShaderId_ReturnsDynamicVariant_ForEmissiveStrengthExtension()
    {
        var scene = CreateScene();
        var material = new Material
        {
            EmissiveStrength = 2.5f
        };

        var shaderId = ShaderSelectionPolicy.ResolvePbrShaderId(material, scene);

        Assert.StartsWith(ShaderIds.PbrVariantPrefix, shaderId);
        Assert.True(ShaderIds.TryParsePbrVariantId(shaderId, out var features));
        Assert.True(features.HasFlag(PbrFeatures.EmissiveStrength));
    }



    [Fact]
    public void ResolvePbrShaderId_DoesNotEnableEmissiveStrengthFeature_ForEmissiveIntensityOnly()
    {
        var scene = CreateScene();
        var material = new Material
        {
            EmissiveIntensity = 2.5f
        };

        var shaderId = ShaderSelectionPolicy.ResolvePbrShaderId(material, scene);

        Assert.Equal(ShaderIds.Pbr, shaderId);
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
        scene.ShaderRegistry.RegisterInstance(ShaderIds.PbrTransmission, new StubShader());
        scene.ShaderRegistry.RegisterInstance(ShaderIds.PbrFullTransmission, new StubShader());
        scene.ShaderRegistry.RegisterInstance(ShaderIds.Unlit, new StubShader());
        scene.ShaderRegistry.SetDefault("fallback");
        scene.ActiveShaderId = ShaderIds.Pbr;
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
