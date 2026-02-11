using System;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Reflection;
using Avalonia3D.Loaders;
using Avalonia3D.Loaders.Policies;
using Avalonia3D.Model;
using Avalonia3D.Model.StandObjects;
using Avalonia3D.Rendering;
using Xunit;

namespace Avalonia3D.Tests;

[Trait("TestTarget", "DomainLogic")]
public sealed class MaterialAlphaAndLoaderTests
{

    [Fact]
    public void ResolveAlphaMode_NormalsDebug_AlwaysOpaque()
    {
        var material = new Avalonia3D.Model.Material { AlphaMode = MaterialAlphaMode.Blend };

        var mode = MeshObject.ResolveAlphaMode(material, opacity: 0.3f, ShaderRenderMode.NormalsDebug);

        Assert.Equal(MaterialAlphaMode.Opaque, mode);
    }


    [Fact]
    public void ResolveAlphaMode_Unlit_BlendWithoutTransparency_FallsBackToOpaque()
    {
        var material = new Avalonia3D.Model.Material
        {
            AlphaMode = MaterialAlphaMode.Blend,
            Opacity = 1f,
            HasTextureTransparency = false
        };

        var mode = MeshObject.ResolveAlphaMode(material, opacity: 1f, ShaderRenderMode.Unlit);

        Assert.Equal(MaterialAlphaMode.Opaque, mode);
    }


    [Fact]
    public void ResolveAlphaMode_RenderPipelineTransparency_UsesResolvedModeNotLegacyFlag()
    {
        var material = new Avalonia3D.Model.Material
        {
            AlphaMode = MaterialAlphaMode.Blend,
            Opacity = 1f,
            HasTextureTransparency = false,
            IsTransparent = true
        };

        var mode = MeshObject.ResolveAlphaMode(material, opacity: 1f, ShaderRenderMode.Unlit);

        Assert.Equal(MaterialAlphaMode.Opaque, mode);
    }

    [Fact]
    public void TextureTransparencyHeuristic_DetectsMeaningfulDeepAlphaCuts()
    {
        var method = typeof(DefaultMaterialImportPolicy).GetMethod("HasMeaningfulTextureTransparency", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
        Assert.NotNull(method);

        var texture = new TextureData
        {
            Width = 16,
            Height = 16,
            Data = BuildTextureAlphaData(16, 16, 255)
        };

        // Достаточно глубокие alpha-пиксели должны считаться осмысленной прозрачностью.
        SetAlpha(texture.Data, pixelIndex: 10, alpha: 32);
        SetAlpha(texture.Data, pixelIndex: 50, alpha: 48);
        SetAlpha(texture.Data, pixelIndex: 90, alpha: 64);

        var result = (bool)method!.Invoke(null, new object?[] { texture })!;

        Assert.True(result);
    }



    [Fact]
    public void TextureTransparencyHeuristic_IgnoresDenseDeepTransparencyMasks()
    {
        var method = typeof(DefaultMaterialImportPolicy).GetMethod("HasMeaningfulTextureTransparency", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
        Assert.NotNull(method);

        var texture = new TextureData
        {
            Width = 16,
            Height = 16,
            Data = BuildTextureAlphaData(16, 16, 255)
        };

        // Эмулируем плотную маску: 25% пикселей с глубокой прозрачностью.
        for (var i = 0; i < 64; i++)
        {
            SetAlpha(texture.Data, pixelIndex: i, alpha: 0);
        }

        var result = (bool)method!.Invoke(null, new object?[] { texture })!;

        Assert.False(result);
    }

    [Fact]
    public void TextureTransparencyHeuristic_PreservesTransparentLayer_WhenDenseDeepAndLowOpaque()
    {
        var method = typeof(DefaultMaterialImportPolicy).GetMethod("HasMeaningfulTextureTransparency", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
        Assert.NotNull(method);

        var texture = new TextureData
        {
            Width = 16,
            Height = 16,
            Data = BuildTextureAlphaData(16, 16, 0)
        };

        // 10% пикселей делаем полностью непрозрачными: остаётся плотная прозрачность,
        // но без масочного паттерна «много deep + много opaque».
        for (var i = 0; i < 26; i++)
        {
            SetAlpha(texture.Data, pixelIndex: i, alpha: 255);
        }

        var result = (bool)method!.Invoke(null, new object?[] { texture })!;

        Assert.True(result);
    }

    [Fact]
    public void TextureTransparencyHeuristic_IgnoresSparseNearOpaqueAlphaNoise()
    {
        var method = typeof(DefaultMaterialImportPolicy).GetMethod("HasMeaningfulTextureTransparency", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
        Assert.NotNull(method);

        var texture = new TextureData
        {
            Width = 16,
            Height = 16,
            Data = BuildTextureAlphaData(16, 16, 255)
        };

        SetAlpha(texture.Data, pixelIndex: 10, alpha: 252);
        SetAlpha(texture.Data, pixelIndex: 50, alpha: 252);
        SetAlpha(texture.Data, pixelIndex: 90, alpha: 252);

        var result = (bool)method!.Invoke(null, new object?[] { texture })!;

        Assert.False(result);
    }

    [Fact]
    public void TextureTransparencyHeuristic_IgnoresAlmostFullyTransparentTextures()
    {
        var method = typeof(DefaultMaterialImportPolicy).GetMethod("HasMeaningfulTextureTransparency", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
        Assert.NotNull(method);

        var texture = new TextureData
        {
            Width = 16,
            Height = 16,
            Data = BuildTextureAlphaData(16, 16, 0)
        };

        var result = (bool)method!.Invoke(null, new object?[] { texture })!;

        Assert.False(result);
    }

    [Fact]
    public void ResolveAlphaMode_UsesExplicitMaterialMode()
    {
        var material = new Avalonia3D.Model.Material { AlphaMode = MaterialAlphaMode.Mask };

        var mode = MeshObject.ResolveAlphaMode(material, opacity: 0.1f);

        Assert.Equal(MaterialAlphaMode.Mask, mode);
    }

    [Fact]
    public void ResolveAlphaMode_UsesOpacityFallbackWithoutMaterial()
    {
        Assert.Equal(MaterialAlphaMode.Opaque, MeshObject.ResolveAlphaMode(null, opacity: 1f));
        Assert.Equal(MaterialAlphaMode.Blend, MeshObject.ResolveAlphaMode(null, opacity: 0.8f));
    }

    [Fact]
    public void LoadModels_ReadsMaterialSurfaceSettings_FromGltf()
    {
        var path = Path.Combine(Path.GetTempPath(), $"mat-loader-{Guid.NewGuid():N}.gltf");
        File.WriteAllText(path, GetMinimalGltfJson());

        var gltf = SharpGLTF.Schema2.ModelRoot.Load(path);
        var models = ModelLoader.LoadModels(gltf);
        File.Delete(path);
        var material = Assert.Single(models).Material;

        Assert.NotNull(material);
        Assert.Equal(MaterialAlphaMode.Mask, material!.AlphaMode);
        Assert.InRange(material.AlphaCutoff, 0.329f, 0.331f);
        Assert.True(material.DoubleSided);
        Assert.InRange(material.EmissiveStrength, 2.49f, 2.51f);
        Assert.InRange(material.EmissiveIntensity, 0.999f, 1.001f);
    }


    [Fact]
    public void LoadModels_OpaqueMode_DoesNotMarkMaterialTransparent_WhenBaseAlphaIsLow()
    {
        var path = Path.Combine(Path.GetTempPath(), $"mat-loader-opaque-{Guid.NewGuid():N}.gltf");
        File.WriteAllText(path, GetOpaqueLowAlphaGltfJson());

        var gltf = SharpGLTF.Schema2.ModelRoot.Load(path);
        var models = ModelLoader.LoadModels(gltf);
        File.Delete(path);
        var material = Assert.Single(models).Material;

        Assert.NotNull(material);
        Assert.Equal(MaterialAlphaMode.Opaque, material!.AlphaMode);
        Assert.False(material.IsTransparent);
        Assert.InRange(material.Opacity, 0.49f, 0.51f);
    }



    [Fact]
    public void LoadModels_BlendWithFactorAlpha_KeepsBlendMode()
    {
        var path = Path.Combine(Path.GetTempPath(), $"mat-loader-blend-factor-{Guid.NewGuid():N}.gltf");
        File.WriteAllText(path, GetBlendFactorAlphaGltfJson());

        var gltf = SharpGLTF.Schema2.ModelRoot.Load(path);
        var models = ModelLoader.LoadModels(gltf);
        File.Delete(path);
        var material = Assert.Single(models).Material;

        Assert.NotNull(material);
        Assert.Equal(MaterialAlphaMode.Blend, material!.AlphaMode);
        Assert.True(material.IsTransparent);
    }

    [Fact]
    public void LoadModels_BlendModeWithoutActualAlpha_FallsBackToOpaque()
    {
        var path = Path.Combine(Path.GetTempPath(), $"mat-loader-blend-fallback-{Guid.NewGuid():N}.gltf");
        File.WriteAllText(path, GetBlendNoAlphaSignalGltfJson());

        var gltf = SharpGLTF.Schema2.ModelRoot.Load(path);
        var models = ModelLoader.LoadModels(gltf);
        File.Delete(path);
        var material = Assert.Single(models).Material;

        Assert.NotNull(material);
        Assert.Equal(MaterialAlphaMode.Opaque, material!.AlphaMode);
        Assert.False(material.IsTransparent);
    }


    [Fact]
    public void LoadModels_TransmissionBlend_PreservesBlendWhenAlphaSignalExists()
    {
        var path = Path.Combine(Path.GetTempPath(), $"mat-loader-transmission-{Guid.NewGuid():N}.gltf");
        File.WriteAllText(path, GetTransmissionBlendGltfJson());

        var gltf = SharpGLTF.Schema2.ModelRoot.Load(path);
        var models = ModelLoader.LoadModels(gltf);
        File.Delete(path);
        var material = Assert.Single(models).Material;

        Assert.NotNull(material);
        Assert.True(material!.HasTransmission);
        Assert.Equal(MaterialAlphaMode.Blend, material.AlphaMode);
        Assert.True(material.IsTransparent);
    }


    [Fact]
    public void LoadModels_TransmissionVolumeAndIor_ReadsExtendedTransmissionSettings()
    {
        var path = Path.Combine(Path.GetTempPath(), $"mat-loader-transmission-volume-{Guid.NewGuid():N}.gltf");
        File.WriteAllText(path, GetTransmissionVolumeIorGltfJson());

        var gltf = SharpGLTF.Schema2.ModelRoot.Load(path);
        var models = ModelLoader.LoadModels(gltf);
        File.Delete(path);
        var material = Assert.Single(models).Material;

        Assert.NotNull(material);
        Assert.True(material!.HasTransmission);
        Assert.InRange(material.TransmissionFactor, 0.799f, 0.801f);
        Assert.InRange(material.TransmissionThickness, 0.299f, 0.301f);
        Assert.InRange(material.TransmissionIor, 1.699f, 1.701f);
        Assert.InRange(material.TransmissionAttenuationDistance, 1.999f, 2.001f);
        Assert.Equal(new Vector3(0.9f, 0.8f, 0.7f), material.TransmissionAttenuationColor);
    }


    [Fact]
    public void LoadModels_KhrAdvancedExtensions_ReadsAdvancedSurfaceSettings()
    {
        var path = Path.Combine(Path.GetTempPath(), $"mat-loader-advanced-{Guid.NewGuid():N}.gltf");
        File.WriteAllText(path, GetAdvancedExtensionsGltfJson());

        var gltf = SharpGLTF.Schema2.ModelRoot.Load(path);
        var models = ModelLoader.LoadModels(gltf);
        File.Delete(path);
        var material = Assert.Single(models).Material;

        Assert.NotNull(material);
        Assert.InRange(material!.ClearcoatFactor, 0.59f, 0.61f);
        Assert.InRange(material.ClearcoatRoughness, 0.19f, 0.21f);
        Assert.Equal(new Vector3(0.1f, 0.2f, 0.3f), material.SheenColorFactor);
        Assert.InRange(material.SheenRoughnessFactor, 0.39f, 0.41f);
        Assert.InRange(material.SpecularFactor, 0.89f, 0.91f);
        Assert.Equal(new Vector3(0.9f, 0.8f, 0.7f), material.SpecularColorFactor);
        Assert.InRange(material.Ior, 1.69f, 1.71f);
        Assert.InRange(material.EmissiveStrength, 2.49f, 2.51f);
        Assert.InRange(material.EmissiveIntensity, 0.999f, 1.001f);
        Assert.True(material.HasTransmission);
        Assert.InRange(material.TransmissionFactor, 0.79f, 0.81f);
    }


    [Fact]
    public void LoadModels_AdvancedExtensions_LoadsExtensionTextureSignals()
    {
        var path = Path.Combine(Path.GetTempPath(), $"mat-loader-advanced-textures-{Guid.NewGuid():N}.gltf");
        File.WriteAllText(path, GetAdvancedExtensionsWithTextureSignalsGltfJson());

        var gltf = SharpGLTF.Schema2.ModelRoot.Load(path);
        var models = ModelLoader.LoadModels(gltf);
        File.Delete(path);
        var material = Assert.Single(models).Material;

        Assert.NotNull(material);
        Assert.InRange(material!.ClearcoatFactor, 0.59f, 0.61f);
        Assert.InRange(material.ClearcoatRoughness, 0.19f, 0.21f);
        Assert.Equal(new Vector3(0.1f, 0.2f, 0.3f), material.SheenColorFactor);
        Assert.InRange(material.SheenRoughnessFactor, 0.39f, 0.41f);
        Assert.InRange(material.SpecularFactor, 0.89f, 0.91f);
        Assert.Equal(new Vector3(0.9f, 0.8f, 0.7f), material.SpecularColorFactor);
        Assert.InRange(material.TransmissionFactor, 0.79f, 0.81f);
        Assert.InRange(material.TransmissionThickness, 0.19f, 0.21f);
        Assert.NotNull(material.ExtensionTextures);
    }

    [Fact]
    public void LoadModels_UnknownMaterialExtension_IsIgnoredWithoutCrash()
    {
        var path = Path.Combine(Path.GetTempPath(), $"mat-loader-unknown-ext-{Guid.NewGuid():N}.gltf");
        File.WriteAllText(path, GetUnknownExtensionGltfJson());

        var gltf = SharpGLTF.Schema2.ModelRoot.Load(path);
        var models = ModelLoader.LoadModels(gltf);
        File.Delete(path);

        var material = Assert.Single(models).Material;
        Assert.NotNull(material);
        Assert.Equal(MaterialAlphaMode.Opaque, material!.AlphaMode);
    }


    [Fact]
    public void MaterialAlphaImportPolicy_Balanced_PreservesBlendForEmissiveMaterials()
    {
        var material = new Avalonia3D.Model.Material
        {
            AlphaMode = MaterialAlphaMode.Blend,
            BaseColorFactor = new Vector4(1f, 1f, 1f, 1f),
            HasTextureTransparency = false,
            EmissiveFactor = new Vector3(0.2f, 0f, 0f)
        };

        var policy = new MaterialAlphaImportPolicy();
        policy.Apply(material, MaterialAlphaImportProfile.Balanced);

        Assert.Equal(MaterialAlphaMode.Blend, material.AlphaMode);
    }

    [Fact]
    public void MaterialAlphaImportPolicy_Legacy_PreservesBlendForEmissiveMaterials()
    {
        var material = new Avalonia3D.Model.Material
        {
            AlphaMode = MaterialAlphaMode.Blend,
            BaseColorFactor = new Vector4(1f, 1f, 1f, 1f),
            HasTextureTransparency = false,
            EmissiveFactor = new Vector3(0.2f, 0f, 0f)
        };

        var policy = new MaterialAlphaImportPolicy();
        policy.Apply(material, MaterialAlphaImportProfile.Legacy);

        Assert.Equal(MaterialAlphaMode.Blend, material.AlphaMode);
    }

    [Fact]
    public void MaterialAlphaImportPolicy_Strict_DoesNotFallbackBlendWithoutAlphaSignal()
    {
        var material = new Avalonia3D.Model.Material
        {
            AlphaMode = MaterialAlphaMode.Blend,
            BaseColorFactor = new Vector4(1f, 1f, 1f, 1f),
            HasTextureTransparency = false
        };

        var policy = new MaterialAlphaImportPolicy();
        policy.Apply(material, MaterialAlphaImportProfile.Strict);

        Assert.Equal(MaterialAlphaMode.Blend, material.AlphaMode);
    }

    [Fact]
    public void LoadModels_BlendWithoutAlphaSignal_BalancedProfile_PreservesEmissiveBlend()
    {
        var previous = MaterialAlphaImportConfiguration.CurrentProfile;
        MaterialAlphaImportConfiguration.Configure(MaterialAlphaImportProfile.Balanced);

        try
        {
            var path = Path.Combine(Path.GetTempPath(), $"mat-loader-emissive-blend-{Guid.NewGuid():N}.gltf");
            File.WriteAllText(path, GetBlendNoAlphaButEmissiveSignalGltfJson());

            var gltf = SharpGLTF.Schema2.ModelRoot.Load(path);
            var models = ModelLoader.LoadModels(gltf);
            File.Delete(path);
            var material = Assert.Single(models).Material;

            Assert.NotNull(material);
            Assert.Equal(MaterialAlphaMode.Blend, material!.AlphaMode);
            Assert.True(material.IsTransparent);
        }
        finally
        {
            MaterialAlphaImportConfiguration.Configure(previous);
        }
    }

    [Fact]
    public void LoadModels_BlendWithoutAlphaSignal_LegacyProfile_PreservesEmissiveBlend()
    {
        var previous = MaterialAlphaImportConfiguration.CurrentProfile;
        MaterialAlphaImportConfiguration.Configure(MaterialAlphaImportProfile.Legacy);

        try
        {
            var path = Path.Combine(Path.GetTempPath(), $"mat-loader-emissive-legacy-{Guid.NewGuid():N}.gltf");
            File.WriteAllText(path, GetBlendNoAlphaButEmissiveSignalGltfJson());

            var gltf = SharpGLTF.Schema2.ModelRoot.Load(path);
            var models = ModelLoader.LoadModels(gltf);
            File.Delete(path);
            var material = Assert.Single(models).Material;

            Assert.NotNull(material);
            Assert.Equal(MaterialAlphaMode.Blend, material!.AlphaMode);
            Assert.True(material.IsTransparent);
        }
        finally
        {
            MaterialAlphaImportConfiguration.Configure(previous);
        }
    }

    [Fact]
    public void LoadModels_BlendEmissiveTextureAndStrength_DoesNotFallbackToOpaque_InBalancedProfile()
    {
        var previous = MaterialAlphaImportConfiguration.CurrentProfile;
        MaterialAlphaImportConfiguration.Configure(MaterialAlphaImportProfile.Balanced);

        try
        {
            var path = Path.Combine(Path.GetTempPath(), $"mat-loader-emissive-texture-strength-{Guid.NewGuid():N}.gltf");
            File.WriteAllText(path, GetBlendNoAlphaButEmissiveTextureAndStrengthGltfJson());

            var gltf = SharpGLTF.Schema2.ModelRoot.Load(path);
            var models = ModelLoader.LoadModels(gltf);
            File.Delete(path);
            var material = Assert.Single(models).Material;

            Assert.NotNull(material);
            Assert.Equal(MaterialAlphaMode.Blend, material!.AlphaMode);
            Assert.True(material.IsTransparent);
            Assert.InRange(material.EmissiveStrength, 2.49f, 2.51f);
        }
        finally
        {
            MaterialAlphaImportConfiguration.Configure(previous);
        }
    }

    [Fact]
    public void LoadModels_BlendWithoutAlphaAndEmission_LegacyProfile_FallsBackToOpaque()
    {
        var previous = MaterialAlphaImportConfiguration.CurrentProfile;
        MaterialAlphaImportConfiguration.Configure(MaterialAlphaImportProfile.Legacy);

        try
        {
            var path = Path.Combine(Path.GetTempPath(), $"mat-loader-legacy-opaque-fallback-{Guid.NewGuid():N}.gltf");
            File.WriteAllText(path, GetBlendNoAlphaSignalGltfJson());

            var gltf = SharpGLTF.Schema2.ModelRoot.Load(path);
            var models = ModelLoader.LoadModels(gltf);
            File.Delete(path);
            var material = Assert.Single(models).Material;

            Assert.NotNull(material);
            Assert.Equal(MaterialAlphaMode.Opaque, material!.AlphaMode);
            Assert.False(material.IsTransparent);
            Assert.Null(material.EmissiveTexture);
            Assert.InRange(material.EmissiveStrength, 0.999f, 1.001f);
        }
        finally
        {
            MaterialAlphaImportConfiguration.Configure(previous);
        }
    }

    [Fact]
    public void ResolveAlphaMode_And_RenderPipelineTransparency_AreConsistent_ForProblematicEmissiveBlendMaterial()
    {
        var material = new Avalonia3D.Model.Material
        {
            AlphaMode = MaterialAlphaMode.Blend,
            Opacity = 1f,
            HasTextureTransparency = true,
            EmissiveTexture = new TextureData
            {
                Width = 1,
                Height = 1,
                Data = BuildTextureAlphaData(1, 1, 255)
            },
            EmissiveStrength = 2.5f
        };

        var mesh = new MeshObject
        {
            Opacity = 1f
        };
        mesh.AssignModel(new Avalonia3D.Model.Model { Material = material });

        var resolvedMode = MeshObject.ResolveAlphaMode(mesh.Material, mesh.Opacity, ShaderRenderMode.Unlit);

        var method = typeof(RenderPipeline).GetMethod("IsTransparent", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);
        var isTransparent = (bool)method!.Invoke(null, [mesh, ShaderRenderMode.Unlit])!;

        Assert.Equal(MaterialAlphaMode.Blend, resolvedMode);
        Assert.True(isTransparent);
    }

    [Fact]
    public void DroidSceneFixture_ContainsWeightsChannel_And_EmissiveBlendMaterial()
    {
        var scenePath = ResolveDroidScenePath();

        var model = SharpGLTF.Schema2.ModelRoot.Load(scenePath);
        var hasWeightsChannel = model.LogicalAnimations
            .SelectMany(animation => animation.Channels)
            .Any(channel => channel.TargetNodePath == SharpGLTF.Schema2.PropertyPath.weights);
        var hasEmissiveBlendMaterial = model.LogicalMaterials.Any(material =>
            material.Alpha == SharpGLTF.Schema2.AlphaMode.BLEND
            && material.FindChannel("Emissive") != null);

        Assert.True(hasWeightsChannel);
        Assert.True(hasEmissiveBlendMaterial);
    }

    [Fact]
    public void MaterialAlphaImportConfiguration_ResolveFromArgsAndEnvironment()
    {
        var fromArgs = MaterialAlphaImportConfiguration.ResolveFrom(new[] { "--material-alpha-import=legacy" });
        Assert.Equal(MaterialAlphaImportProfile.Legacy, fromArgs);

        var fromEnv = MaterialAlphaImportConfiguration.ResolveFrom(
            args: Array.Empty<string>(),
            environment: new System.Collections.Generic.Dictionary<string, string?>
            {
                ["AVALONIA3D_MATERIAL_ALPHA_IMPORT"] = "strict"
            });

        Assert.Equal(MaterialAlphaImportProfile.Strict, fromEnv);
    }

    [Fact]
    public void MaterialImportOverrideConfiguration_ResolveFromArgsAndEnvironment()
    {
        var fromArgs = MaterialImportOverrideConfiguration.ResolveConfigPath(new[] { "--material-import-overrides=cfg/materials.json" });
        Assert.Equal("cfg/materials.json", fromArgs);

        var fromEnv = MaterialImportOverrideConfiguration.ResolveConfigPath(
            args: Array.Empty<string>(),
            environment: new System.Collections.Generic.Dictionary<string, string?>
            {
                ["AVALONIA3D_MATERIAL_IMPORT_OVERRIDES"] = "cfg/env.json"
            });

        Assert.Equal("cfg/env.json", fromEnv);
    }

    [Fact]
    public void DefaultMaterialImportPolicy_SceneOverride_CanForceOpaqueForDroidScene()
    {
        MaterialImportOverrideConfiguration.Configure(new System.Collections.Generic.Dictionary<string, MaterialSceneImportOverride>
        {
            ["Assets/TestScenes/droid/scene.gltf"] = new()
            {
                ForceAlphaMode = MaterialAlphaMode.Opaque
            }
        });

        try
        {
            var sceneOverride = MaterialImportOverrideConfiguration.ResolveForAsset("/workspace/Avalonia3D/Avalonia3D.Sandbox/Assets/TestScenes/droid/scene.gltf");
            var context = new MaterialImportPolicyContext
            {
                AlphaProfile = MaterialAlphaImportProfile.Balanced,
                SceneOverride = sceneOverride
            };

            var policy = new DefaultMaterialImportPolicy();
            var material = new Avalonia3D.Model.Material
            {
                AlphaMode = MaterialAlphaMode.Blend,
                BaseColorFactor = new Vector4(1f, 1f, 1f, 1f)
            };

            var mode = policy.ResolveAlphaMode(material, context);

            Assert.Equal(MaterialAlphaMode.Opaque, mode);
        }
        finally
        {
            MaterialImportOverrideConfiguration.Configure(null);
        }
    }

    [Fact]
    public void MaterialImportOverrideConfiguration_ResolveForMaterial_UsesMaterialLevelOverride()
    {
        MaterialImportOverrideConfiguration.ConfigureAssetOverrides(new System.Collections.Generic.Dictionary<string, MaterialAssetImportOverride>
        {
            ["Assets/TestScenes/droid/scene.gltf"] = new()
            {
                AlphaProfile = MaterialAlphaImportProfile.Balanced,
                Materials = new System.Collections.Generic.Dictionary<string, MaterialSceneImportOverride>
                {
                    ["Glow"] = new() { ForceAlphaMode = MaterialAlphaMode.Mask }
                }
            }
        });

        try
        {
            var resolved = MaterialImportOverrideConfiguration.ResolveForMaterial(
                "/workspace/Avalonia3D/Avalonia3D.Sandbox/Assets/TestScenes/droid/scene.gltf",
                "Glow");

            Assert.NotNull(resolved);
            Assert.Equal(MaterialAlphaMode.Mask, resolved!.ForceAlphaMode);
            Assert.Equal(MaterialAlphaImportProfile.Balanced, resolved.AlphaProfile);
        }
        finally
        {
            MaterialImportOverrideConfiguration.Configure(null);
        }
    }

    [Fact]
    public void DefaultMaterialImportPolicy_AnimatedEmissiveContract_PreservesBlendInLegacy()
    {
        var material = new Avalonia3D.Model.Material
        {
            AlphaMode = MaterialAlphaMode.Blend,
            BaseColorFactor = new Vector4(1f, 1f, 1f, 1f),
            EmissiveFactor = new Vector3(0.3f, 0f, 0f)
        };

        var context = new MaterialImportPolicyContext
        {
            AlphaProfile = MaterialAlphaImportProfile.Legacy,
            SourceAlphaMode = MaterialAlphaMode.Blend,
            IsAnimatedMaterial = true
        };

        var policy = new DefaultMaterialImportPolicy();
        var mode = policy.ResolveAlphaMode(material, context);

        Assert.Equal(MaterialAlphaMode.Blend, mode);
    }

    [Fact]
    public void OpaqueMaterialDefaults_DoNotRegress()
    {
        var material = new Avalonia3D.Model.Material();

        Assert.Equal(MaterialAlphaMode.Opaque, material.AlphaMode);
        Assert.InRange(material.AlphaCutoff, 0.499f, 0.501f);
        Assert.False(material.DoubleSided);
        Assert.InRange(material.EmissiveIntensity, 0.999f, 1.001f);
        Assert.InRange(material.EmissiveStrength, 0.999f, 1.001f);
        Assert.False(material.IsTransparent);
    }


    [Fact]
    public void ModelLoader_TransfersChannelTexCoordToRuntimeParameters()
    {
        var getChannelTexCoord = typeof(ModelLoader).GetMethod("GetChannelTexCoord", BindingFlags.NonPublic | BindingFlags.Static);
        var syncRuntimeTransform = typeof(ModelLoader).GetMethod("SyncTextureRuntimeTransform", BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(getChannelTexCoord);
        Assert.NotNull(syncRuntimeTransform);

        var channel = new FakeTextureChannel { TextureCoordinate = 1 };
        var texCoord = (int)getChannelTexCoord!.Invoke(null, new object?[] { channel })!;

        var texture = new TextureData();
        texture.Transform.TexCoord = texCoord;

        var material = new Avalonia3D.Model.Material();
        syncRuntimeTransform!.Invoke(null, new object?[] { material, TextureSemantic.BaseColor, texture });

        Assert.Equal(1, material.TextureRuntime.BaseColor.TexCoordSet);
    }

    private sealed class FakeTextureChannel
    {
        public int TextureCoordinate { get; set; }
    }
    private static string GetMinimalGltfJson() =>
        """
        {
          "asset": { "version": "2.0" },
          "extensionsUsed": ["KHR_materials_emissive_strength"],
          "scenes": [ { "nodes": [0] } ],
          "nodes": [ { "mesh": 0, "name": "n" } ],
          "meshes": [
            {
              "primitives": [
                {
                  "attributes": { "POSITION": 0 },
                  "indices": 1,
                  "material": 0
                }
              ]
            }
          ],
          "materials": [
            {
              "alphaMode": "MASK",
              "alphaCutoff": 0.33,
              "doubleSided": true,
              "emissiveFactor": [0.5, 0.25, 0.75],
              "extensions": {
                "KHR_materials_emissive_strength": {
                  "emissiveStrength": 2.5
                }
              },
              "pbrMetallicRoughness": {
                "baseColorFactor": [1,1,1,1],
                "metallicFactor": 0,
                "roughnessFactor": 1
              }
            }
          ],
          "buffers": [
            {
              "uri": "data:application/octet-stream;base64,AAAAAAAAAAAAAAAAAACAPwAAAAAAAAAAAAAAAAAAgD8AAAAAAAABAAIA",
              "byteLength": 42
            }
          ],
          "bufferViews": [
            { "buffer": 0, "byteOffset": 0, "byteLength": 36, "target": 34962 },
            { "buffer": 0, "byteOffset": 36, "byteLength": 6, "target": 34963 }
          ],
          "accessors": [
            { "bufferView": 0, "componentType": 5126, "count": 3, "type": "VEC3", "min": [0,0,0], "max": [1,1,0] },
            { "bufferView": 1, "componentType": 5123, "count": 3, "type": "SCALAR" }
          ]
        }
        """;
    private static string GetOpaqueLowAlphaGltfJson() =>
        """
        {
          "asset": { "version": "2.0" },
          "scenes": [ { "nodes": [0] } ],
          "nodes": [ { "mesh": 0, "name": "n" } ],
          "meshes": [
            {
              "primitives": [
                {
                  "attributes": { "POSITION": 0 },
                  "indices": 1,
                  "material": 0
                }
              ]
            }
          ],
          "materials": [
            {
              "alphaMode": "OPAQUE",
              "pbrMetallicRoughness": {
                "baseColorFactor": [1,1,1,0.5],
                "metallicFactor": 0,
                "roughnessFactor": 1
              }
            }
          ],
          "buffers": [
            {
              "uri": "data:application/octet-stream;base64,AAAAAAAAAAAAAAAAAACAPwAAAAAAAAAAAAAAAAAAgD8AAAAAAAABAAIA",
              "byteLength": 42
            }
          ],
          "bufferViews": [
            { "buffer": 0, "byteOffset": 0, "byteLength": 36, "target": 34962 },
            { "buffer": 0, "byteOffset": 36, "byteLength": 6, "target": 34963 }
          ],
          "accessors": [
            { "bufferView": 0, "componentType": 5126, "count": 3, "type": "VEC3", "min": [0,0,0], "max": [1,1,0] },
            { "bufferView": 1, "componentType": 5123, "count": 3, "type": "SCALAR" }
          ]
        }
        """;


    private static string GetBlendFactorAlphaGltfJson() =>
        """
        {
          "asset": { "version": "2.0" },
          "scenes": [ { "nodes": [0] } ],
          "nodes": [ { "mesh": 0, "name": "n" } ],
          "meshes": [
            {
              "primitives": [
                {
                  "attributes": { "POSITION": 0 },
                  "indices": 1,
                  "material": 0
                }
              ]
            }
          ],
          "materials": [
            {
              "alphaMode": "BLEND",
              "pbrMetallicRoughness": {
                "baseColorFactor": [1,1,1,0.35],
                "metallicFactor": 0,
                "roughnessFactor": 1
              }
            }
          ],
          "buffers": [
            {
              "uri": "data:application/octet-stream;base64,AAAAAAAAAAAAAAAAAACAPwAAAAAAAAAAAAAAAAAAgD8AAAAAAAABAAIA",
              "byteLength": 42
            }
          ],
          "bufferViews": [
            { "buffer": 0, "byteOffset": 0, "byteLength": 36, "target": 34962 },
            { "buffer": 0, "byteOffset": 36, "byteLength": 6, "target": 34963 }
          ],
          "accessors": [
            { "bufferView": 0, "componentType": 5126, "count": 3, "type": "VEC3", "min": [0,0,0], "max": [1,1,0] },
            { "bufferView": 1, "componentType": 5123, "count": 3, "type": "SCALAR" }
          ]
        }
        """;

    private static string GetBlendNoAlphaSignalGltfJson() =>
        """
        {
          "asset": { "version": "2.0" },
          "scenes": [ { "nodes": [0] } ],
          "nodes": [ { "mesh": 0, "name": "n" } ],
          "meshes": [
            {
              "primitives": [
                {
                  "attributes": { "POSITION": 0 },
                  "indices": 1,
                  "material": 0
                }
              ]
            }
          ],
          "materials": [
            {
              "alphaMode": "BLEND",
              "pbrMetallicRoughness": {
                "baseColorFactor": [1,1,1,1],
                "metallicFactor": 0,
                "roughnessFactor": 1
              }
            }
          ],
          "buffers": [
            {
              "uri": "data:application/octet-stream;base64,AAAAAAAAAAAAAAAAAACAPwAAAAAAAAAAAAAAAAAAgD8AAAAAAAABAAIA",
              "byteLength": 42
            }
          ],
          "bufferViews": [
            { "buffer": 0, "byteOffset": 0, "byteLength": 36, "target": 34962 },
            { "buffer": 0, "byteOffset": 36, "byteLength": 6, "target": 34963 }
          ],
          "accessors": [
            { "bufferView": 0, "componentType": 5126, "count": 3, "type": "VEC3", "min": [0,0,0], "max": [1,1,0] },
            { "bufferView": 1, "componentType": 5123, "count": 3, "type": "SCALAR" }
          ]
        }
        """;


    private static string GetBlendNoAlphaButEmissiveSignalGltfJson() =>
        """
        {
          "asset": { "version": "2.0" },
          "extensionsUsed": ["KHR_materials_emissive_strength"],
          "scenes": [ { "nodes": [0] } ],
          "nodes": [ { "mesh": 0, "name": "n" } ],
          "meshes": [
            {
              "primitives": [
                {
                  "attributes": { "POSITION": 0 },
                  "indices": 1,
                  "material": 0
                }
              ]
            }
          ],
          "materials": [
            {
              "alphaMode": "BLEND",
              "emissiveFactor": [0.4,0.2,0.1],
              "extensions": {
                "KHR_materials_emissive_strength": {
                  "emissiveStrength": 2.0
                }
              },
              "pbrMetallicRoughness": {
                "baseColorFactor": [1,1,1,1],
                "metallicFactor": 0,
                "roughnessFactor": 1
              }
            }
          ],
          "buffers": [
            {
              "uri": "data:application/octet-stream;base64,AAAAAAAAAAAAAAAAAACAPwAAAAAAAAAAAAAAAAAAgD8AAAAAAAABAAIA",
              "byteLength": 42
            }
          ],
          "bufferViews": [
            { "buffer": 0, "byteOffset": 0, "byteLength": 36, "target": 34962 },
            { "buffer": 0, "byteOffset": 36, "byteLength": 6, "target": 34963 }
          ],
          "accessors": [
            { "bufferView": 0, "componentType": 5126, "count": 3, "type": "VEC3", "min": [0,0,0], "max": [1,1,0] },
            { "bufferView": 1, "componentType": 5123, "count": 3, "type": "SCALAR" }
          ]
        }
        """;

    private static string GetTransmissionBlendGltfJson() =>
        """
        {
          "asset": { "version": "2.0" },
          "extensionsUsed": ["KHR_materials_transmission"],
          "scenes": [ { "nodes": [0] } ],
          "nodes": [ { "mesh": 0, "name": "n" } ],
          "meshes": [
            {
              "primitives": [
                {
                  "attributes": { "POSITION": 0 },
                  "indices": 1,
                  "material": 0
                }
              ]
            }
          ],
          "materials": [
            {
              "alphaMode": "BLEND",
              "extensions": {
                "KHR_materials_transmission": {
                  "transmissionFactor": 1.0
                }
              },
              "pbrMetallicRoughness": {
                "baseColorFactor": [1,1,1,0.25],
                "metallicFactor": 0,
                "roughnessFactor": 0
              }
            }
          ],
          "buffers": [
            {
              "uri": "data:application/octet-stream;base64,AAAAAAAAAAAAAAAAAACAPwAAAAAAAAAAAAAAAAAAgD8AAAAAAAABAAIA",
              "byteLength": 42
            }
          ],
          "bufferViews": [
            { "buffer": 0, "byteOffset": 0, "byteLength": 36, "target": 34962 },
            { "buffer": 0, "byteOffset": 36, "byteLength": 6, "target": 34963 }
          ],
          "accessors": [
            { "bufferView": 0, "componentType": 5126, "count": 3, "type": "VEC3", "min": [0,0,0], "max": [1,1,0] },
            { "bufferView": 1, "componentType": 5123, "count": 3, "type": "SCALAR" }
          ]
        }
        """;

    private static string GetTransmissionVolumeIorGltfJson() =>
        """
        {
          "asset": { "version": "2.0" },
          "extensionsUsed": ["KHR_materials_transmission", "KHR_materials_volume", "KHR_materials_ior"],
          "scenes": [ { "nodes": [0] } ],
          "nodes": [ { "mesh": 0, "name": "n" } ],
          "meshes": [
            {
              "primitives": [
                {
                  "attributes": { "POSITION": 0 },
                  "indices": 1,
                  "material": 0
                }
              ]
            }
          ],
          "materials": [
            {
              "alphaMode": "OPAQUE",
              "extensions": {
                "KHR_materials_transmission": {
                  "transmissionFactor": 0.8
                },
                "KHR_materials_volume": {
                  "thicknessFactor": 0.3,
                  "attenuationDistance": 2.0,
                  "attenuationColor": [0.9, 0.8, 0.7]
                },
                "KHR_materials_ior": {
                  "ior": 1.7
                }
              },
              "pbrMetallicRoughness": {
                "baseColorFactor": [1,1,1,1],
                "metallicFactor": 0,
                "roughnessFactor": 0.4
              }
            }
          ],
          "buffers": [
            {
              "uri": "data:application/octet-stream;base64,AAAAAAAAAAAAAAAAAACAPwAAAAAAAAAAAAAAAAAAgD8AAAAAAAABAAIA",
              "byteLength": 42
            }
          ],
          "bufferViews": [
            { "buffer": 0, "byteOffset": 0, "byteLength": 36, "target": 34962 },
            { "buffer": 0, "byteOffset": 36, "byteLength": 6, "target": 34963 }
          ],
          "accessors": [
            { "bufferView": 0, "componentType": 5126, "count": 3, "type": "VEC3", "min": [0,0,0], "max": [1,1,0] },
            { "bufferView": 1, "componentType": 5123, "count": 3, "type": "SCALAR" }
          ]
        }
        """;


    private static string GetAdvancedExtensionsGltfJson() =>
        """
        {
          "asset": { "version": "2.0" },
          "extensionsUsed": [
            "KHR_materials_transmission",
            "KHR_materials_ior",
            "KHR_materials_emissive_strength",
            "KHR_materials_clearcoat",
            "KHR_materials_sheen",
            "KHR_materials_specular"
          ],
          "scenes": [ { "nodes": [0] } ],
          "nodes": [ { "mesh": 0, "name": "n" } ],
          "meshes": [
            { "primitives": [ { "attributes": { "POSITION": 0 }, "indices": 1, "material": 0 } ] }
          ],
          "materials": [
            {
              "extensions": {
                "KHR_materials_transmission": { "transmissionFactor": 0.8 },
                "KHR_materials_ior": { "ior": 1.7 },
                "KHR_materials_emissive_strength": { "emissiveStrength": 2.5 },
                "KHR_materials_clearcoat": { "clearcoatFactor": 0.6, "clearcoatRoughnessFactor": 0.2 },
                "KHR_materials_sheen": { "sheenColorFactor": [0.1, 0.2, 0.3], "sheenRoughnessFactor": 0.4 },
                "KHR_materials_specular": { "specularFactor": 0.9, "specularColorFactor": [0.9, 0.8, 0.7] }
              },
              "pbrMetallicRoughness": {
                "baseColorFactor": [1,1,1,1],
                "metallicFactor": 0,
                "roughnessFactor": 1
              }
            }
          ],
          "buffers": [
            {
              "uri": "data:application/octet-stream;base64,AAAAAAAAAAAAAAAAAACAPwAAAAAAAAAAAAAAAAAAgD8AAAAAAAABAAIA",
              "byteLength": 42
            }
          ],
          "bufferViews": [
            { "buffer": 0, "byteOffset": 0, "byteLength": 36, "target": 34962 },
            { "buffer": 0, "byteOffset": 36, "byteLength": 6, "target": 34963 }
          ],
          "accessors": [
            { "bufferView": 0, "componentType": 5126, "count": 3, "type": "VEC3", "min": [0,0,0], "max": [1,1,0] },
            { "bufferView": 1, "componentType": 5123, "count": 3, "type": "SCALAR" }
          ]
        }
        """;


    private static string GetAdvancedExtensionsWithTextureSignalsGltfJson() =>
        """
        {
          "asset": { "version": "2.0" },
          "extensionsUsed": [
            "KHR_materials_transmission",
            "KHR_materials_volume",
            "KHR_materials_clearcoat",
            "KHR_materials_sheen",
            "KHR_materials_specular"
          ],
          "scenes": [ { "nodes": [0] } ],
          "nodes": [ { "mesh": 0, "name": "n" } ],
          "meshes": [
            { "primitives": [ { "attributes": { "POSITION": 0 }, "indices": 1, "material": 0 } ] }
          ],
          "materials": [
            {
              "extensions": {
                "KHR_materials_transmission": { "transmissionFactor": 0.8, "transmissionTexture": { "index": 0 } },
                "KHR_materials_volume": { "thicknessFactor": 0.2, "thicknessTexture": { "index": 0 } },
                "KHR_materials_clearcoat": {
                  "clearcoatFactor": 0.6,
                  "clearcoatTexture": { "index": 0 },
                  "clearcoatRoughnessFactor": 0.2,
                  "clearcoatRoughnessTexture": { "index": 0 },
                  "clearcoatNormalTexture": { "index": 0 }
                },
                "KHR_materials_sheen": {
                  "sheenColorFactor": [0.1, 0.2, 0.3],
                  "sheenColorTexture": { "index": 0 },
                  "sheenRoughnessFactor": 0.4,
                  "sheenRoughnessTexture": { "index": 0 }
                },
                "KHR_materials_specular": {
                  "specularFactor": 0.9,
                  "specularTexture": { "index": 0 },
                  "specularColorFactor": [0.9, 0.8, 0.7],
                  "specularColorTexture": { "index": 0 }
                }
              },
              "pbrMetallicRoughness": {
                "baseColorFactor": [1,1,1,1],
                "metallicFactor": 0,
                "roughnessFactor": 1
              }
            }
          ],
          "textures": [ { "sampler": 0, "source": 0 } ],
          "samplers": [ { "magFilter": 9729, "minFilter": 9729, "wrapS": 10497, "wrapT": 10497 } ],
          "images": [
            {
              "uri": "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAIAAACQd1PeAAAADElEQVR42mP8z8BQDwAFgwJ/lX2NDwAAAABJRU5ErkJggg=="
            }
          ],
          "buffers": [
            {
              "uri": "data:application/octet-stream;base64,AAAAAAAAAAAAAAAAAACAPwAAAAAAAAAAAAAAAAAAgD8AAAAAAAABAAIA",
              "byteLength": 42
            }
          ],
          "bufferViews": [
            { "buffer": 0, "byteOffset": 0, "byteLength": 36, "target": 34962 },
            { "buffer": 0, "byteOffset": 36, "byteLength": 6, "target": 34963 }
          ],
          "accessors": [
            { "bufferView": 0, "componentType": 5126, "count": 3, "type": "VEC3", "min": [0,0,0], "max": [1,1,0] },
            { "bufferView": 1, "componentType": 5123, "count": 3, "type": "SCALAR" }
          ]
        }
        """;

    private static string GetUnknownExtensionGltfJson() =>
        """
        {
          "asset": { "version": "2.0" },
          "extensionsUsed": ["VENDOR_unknown_material_ext"],
          "scenes": [ { "nodes": [0] } ],
          "nodes": [ { "mesh": 0, "name": "n" } ],
          "meshes": [
            {
              "primitives": [
                {
                  "attributes": { "POSITION": 0 },
                  "indices": 1,
                  "material": 0
                }
              ]
            }
          ],
          "materials": [
            {
              "extensions": {
                "VENDOR_unknown_material_ext": {
                  "foo": 1,
                  "bar": true
                }
              },
              "pbrMetallicRoughness": {
                "baseColorFactor": [1,1,1,1],
                "metallicFactor": 0,
                "roughnessFactor": 1
              }
            }
          ],
          "buffers": [
            {
              "uri": "data:application/octet-stream;base64,AAAAAAAAAAAAAAAAAACAPwAAAAAAAAAAAAAAAAAAgD8AAAAAAAABAAIA",
              "byteLength": 42
            }
          ],
          "bufferViews": [
            { "buffer": 0, "byteOffset": 0, "byteLength": 36, "target": 34962 },
            { "buffer": 0, "byteOffset": 36, "byteLength": 6, "target": 34963 }
          ],
          "accessors": [
            { "bufferView": 0, "componentType": 5126, "count": 3, "type": "VEC3", "min": [0,0,0], "max": [1,1,0] },
            { "bufferView": 1, "componentType": 5123, "count": 3, "type": "SCALAR" }
          ]
        }
        """;

    private static string GetBlendNoAlphaButEmissiveTextureAndStrengthGltfJson() =>
        """
        {
          "asset": { "version": "2.0" },
          "extensionsUsed": ["KHR_materials_emissive_strength"],
          "scenes": [ { "nodes": [0] } ],
          "nodes": [ { "mesh": 0, "name": "n" } ],
          "meshes": [
            {
              "primitives": [
                {
                  "attributes": { "POSITION": 0 },
                  "indices": 1,
                  "material": 0
                }
              ]
            }
          ],
          "materials": [
            {
              "alphaMode": "BLEND",
              "emissiveFactor": [0,0,0],
              "emissiveTexture": { "index": 0 },
              "extensions": {
                "KHR_materials_emissive_strength": {
                  "emissiveStrength": 2.5
                }
              },
              "pbrMetallicRoughness": {
                "baseColorFactor": [1,1,1,1],
                "metallicFactor": 0,
                "roughnessFactor": 1
              }
            }
          ],
          "textures": [ { "sampler": 0, "source": 0 } ],
          "samplers": [ { "magFilter": 9729, "minFilter": 9729, "wrapS": 10497, "wrapT": 10497 } ],
          "images": [
            {
              "uri": "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAIAAACQd1PeAAAADElEQVR42mP8z8BQDwAFgwJ/lX2NDwAAAABJRU5ErkJggg=="
            }
          ],
          "buffers": [
            {
              "uri": "data:application/octet-stream;base64,AAAAAAAAAAAAAAAAAACAPwAAAAAAAAAAAAAAAAAAgD8AAAAAAAABAAIA",
              "byteLength": 42
            }
          ],
          "bufferViews": [
            { "buffer": 0, "byteOffset": 0, "byteLength": 36, "target": 34962 },
            { "buffer": 0, "byteOffset": 36, "byteLength": 6, "target": 34963 }
          ],
          "accessors": [
            { "bufferView": 0, "componentType": 5126, "count": 3, "type": "VEC3", "min": [0,0,0], "max": [1,1,0] },
            { "bufferView": 1, "componentType": 5123, "count": 3, "type": "SCALAR" }
          ]
        }
        """;

    private static string ResolveDroidScenePath()
    {
        var directPath = Path.GetFullPath(Path.Combine("Avalonia3D.Sandbox", "Assets", "TestScenes", "droid", "scene.gltf"));
        if (File.Exists(directPath))
        {
            return directPath;
        }

        var baseDirectoryPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Avalonia3D.Sandbox", "Assets", "TestScenes", "droid", "scene.gltf"));
        if (File.Exists(baseDirectoryPath))
        {
            return baseDirectoryPath;
        }

        throw new FileNotFoundException("Не удалось найти fixture droid/scene.gltf для интеграционного теста.", directPath);
    }

    private static byte[] BuildTextureAlphaData(int width, int height, byte alpha)
    {
        var data = new byte[width * height * 4];
        for (var i = 0; i < data.Length; i += 4)
        {
            data[i] = 255;
            data[i + 1] = 255;
            data[i + 2] = 255;
            data[i + 3] = alpha;
        }

        return data;
    }

    private static void SetAlpha(byte[] data, int pixelIndex, byte alpha)
    {
        data[pixelIndex * 4 + 3] = alpha;
    }

}
