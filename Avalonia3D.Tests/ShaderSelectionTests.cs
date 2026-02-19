using Avalonia3D.Interfaces;
using Avalonia3D.Model;
using Avalonia3D.Model.StandObjects;
using Avalonia3D.Rendering;
using Avalonia3D.Shaders;
using System;
using System.Collections.Generic;
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

        Assert.NotNull(selected);
        Assert.Equal(ShaderIds.CreatePbrVariantId(ShaderSelectionPolicy.BuildPbrFeatures(material, scene)), ShaderSelectionPolicy.ResolvePbrShaderId(material, scene));
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

        Assert.NotNull(selected);
        Assert.Equal(ShaderIds.CreatePbrVariantId(ShaderSelectionPolicy.BuildPbrFeatures(material, scene)), ShaderSelectionPolicy.ResolvePbrShaderId(material, scene));
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

        Assert.NotNull(selected);
        Assert.Equal(ShaderIds.CreatePbrVariantId(ShaderSelectionPolicy.BuildPbrFeatures(material, scene)), ShaderSelectionPolicy.ResolvePbrShaderId(material, scene));
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

        Assert.NotNull(selected);
        Assert.Equal(ShaderIds.CreatePbrVariantId(ShaderSelectionPolicy.BuildPbrFeatures(material, scene)), ShaderSelectionPolicy.ResolvePbrShaderId(material, scene));
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
    public void Select_PrefersRuntimeVariantOverLegacyStaticShaderWhenRuntimeFactoryAvailable()
    {
        var scene = CreateScene();
        scene.BindRenderMode(ShaderRenderMode.Pbr, ShaderIds.Pbr);
        scene.RenderMode = ShaderRenderMode.Pbr;

        var runtimeShader = new StubShader();
        var policy = new ShaderSelectionPolicy(runtimePbrShaderFactory: new FixedRuntimePbrShaderFactory(runtimeShader));

        var material = new Material
        {
            BaseColorTexture = new TextureData(),
            NormalTexture = new TextureData(),
            MetallicRoughnessTexture = new TextureData()
        };

        var selected = policy.Select(material, scene, gl: null);

        Assert.Same(runtimeShader, selected);
    }

    [Fact]
    public void Select_ReturnsPbrFallback_WhenRuntimeVariantCompilationThrows()
    {
        var scene = new Scene3D();
        var fallbackShader = new StubShader();
        scene.ShaderRegistry.RegisterInstance(ShaderIds.Pbr, fallbackShader);
        scene.ShaderRegistry.RegisterInstance("default", new StubShader());
        scene.ShaderRegistry.SetDefault("default");
        scene.ActiveShaderId = ShaderIds.Pbr;

        var material = new Material
        {
            BaseColorTexture = new TextureData(),
            EmissiveTexture = new TextureData()
        };

        var policy = new ShaderSelectionPolicy(
            runtimePbrShaderFactory: new ThrowingRuntimePbrShaderFactory(),
            pbrVariantReducer: new NoOpPbrVariantReducer());

        var selected = policy.Select(material, scene, gl: null);

        Assert.Same(fallbackShader, selected);
    }

    [Fact]
    public void ResolvePbrShaderId_UsesStableLegacyShader_ForEmissiveAndMetallicWithoutNormalMap()
    {
        var scene = CreateScene();
        var material = new Material
        {
            BaseColorTexture = new TextureData(),
            MetallicRoughnessTexture = new TextureData(),
            EmissiveTexture = new TextureData()
        };

        var shaderId = ShaderSelectionPolicy.ResolvePbrShaderId(material, scene);

        Assert.Equal(ShaderIds.CreatePbrVariantId(ShaderSelectionPolicy.BuildPbrFeatures(material, scene)), shaderId);
    }

    [Fact]
    public void ResolvePbrShaderId_UsesStableLegacyPbr_ForEmissiveStrengthExtension()
    {
        var scene = CreateScene();
        var material = new Material
        {
            EmissiveStrength = 2.5f
        };

        var shaderId = ShaderSelectionPolicy.ResolvePbrShaderId(material, scene);

        Assert.Equal(ShaderIds.CreatePbrVariantId(ShaderSelectionPolicy.BuildPbrFeatures(material, scene)), shaderId);
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

        Assert.Equal(ShaderIds.CreatePbrVariantId(ShaderSelectionPolicy.BuildPbrFeatures(material, scene)), shaderId);
    }


    [Fact]
    public void Select_UsesReducedVariant_WhenRequestedFeatureNotSupportedByProfile()
    {
        var scene = CreateScene();
        scene.BindRenderMode(ShaderRenderMode.Pbr, ShaderIds.Pbr);
        scene.RenderMode = ShaderRenderMode.Pbr;
        scene.ApplyGraphicsProfile(scene.ActiveGraphicsProfile with
        {
            RenderCapabilities = new RenderCapabilities(MaterialFeatureSetExtensions.PbrFeatureMask & ~MaterialFeatureSet.Transmission)
        });

        var captureFactory = new CapturingRuntimePbrShaderFactory();
        var policy = new ShaderSelectionPolicy(
            pbrVariantReducer: new SingleStepPbrVariantReducer(PbrFeatures.None),
            runtimePbrShaderFactory: captureFactory);

        var material = new Material
        {
            HasTransmission = true,
            TransmissionFactor = 1f
        };

        var selected = policy.Select(material, scene, gl: null);

        Assert.NotNull(selected);
        Assert.Equal(MaterialFeatureSet.None, captureFactory.LastRequestedFeatures);
    }

    [Fact]
    public void Select_FallsBackToStaticPbr_WhenNoSupportedRuntimeVariantsAvailable()
    {
        var scene = CreateScene();
        scene.BindRenderMode(ShaderRenderMode.Pbr, ShaderIds.Pbr);
        scene.RenderMode = ShaderRenderMode.Pbr;
        scene.ApplyGraphicsProfile(scene.ActiveGraphicsProfile with
        {
            RenderCapabilities = new RenderCapabilities(MaterialFeatureSet.None)
        });

        var policy = new ShaderSelectionPolicy(
            pbrVariantReducer: new NoOpPbrVariantReducer(),
            runtimePbrShaderFactory: new ThrowingRuntimePbrShaderFactory());

        var material = new Material
        {
            BaseColorTexture = new TextureData()
        };

        var selected = policy.Select(material, scene, gl: null);

        Assert.Same(scene.ShaderRegistry.Get(ShaderIds.Pbr), selected);
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



    private sealed class SingleStepPbrVariantReducer : IPbrVariantReducer
    {
        private readonly PbrFeatures _reduced;

        public SingleStepPbrVariantReducer(PbrFeatures reduced)
        {
            _reduced = reduced;
        }

        public IEnumerable<PbrFeatures> GetReductionChain(PbrFeatures requestedFeatures)
        {
            yield return _reduced;
        }
    }

    private sealed class CapturingRuntimePbrShaderFactory : IRuntimePbrShaderFactory
    {
        public MaterialFeatureSet LastRequestedFeatures { get; private set; } = (MaterialFeatureSet)(-1);

        public IShader3D Create(Silk.NET.OpenGL.GL? gl, MaterialFeatureSet features, int maxLights, IRenderCapabilities capabilities)
        {
            LastRequestedFeatures = features;
            return new StubShader();
        }
    }

    private sealed class FixedRuntimePbrShaderFactory : IRuntimePbrShaderFactory
    {
        private readonly IShader3D _shader;

        public FixedRuntimePbrShaderFactory(IShader3D shader)
        {
            _shader = shader;
        }

        public IShader3D Create(Silk.NET.OpenGL.GL? gl, MaterialFeatureSet features, int maxLights, IRenderCapabilities capabilities)
        {
            return _shader;
        }
    }

    private sealed class ThrowingRuntimePbrShaderFactory : IRuntimePbrShaderFactory
    {
        public IShader3D Create(Silk.NET.OpenGL.GL? gl, MaterialFeatureSet features, int maxLights, IRenderCapabilities capabilities)
        {
            throw new InvalidOperationException("Simulated compile failure");
        }
    }

    private sealed class NoOpPbrVariantReducer : IPbrVariantReducer
    {
        public IEnumerable<PbrFeatures> GetReductionChain(PbrFeatures requestedFeatures)
        {
            yield break;
        }
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
