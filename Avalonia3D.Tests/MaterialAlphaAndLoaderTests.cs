using System;
using System.IO;
using Avalonia3D.Loaders;
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
        Assert.InRange(material.EmissiveIntensity, 2.49f, 2.51f);
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
    public void LoadModels_TransmissionBlend_FallsBackToOpaqueForCurrentPipeline()
    {
        var path = Path.Combine(Path.GetTempPath(), $"mat-loader-transmission-{Guid.NewGuid():N}.gltf");
        File.WriteAllText(path, GetTransmissionBlendGltfJson());

        var gltf = SharpGLTF.Schema2.ModelRoot.Load(path);
        var models = ModelLoader.LoadModels(gltf);
        File.Delete(path);
        var material = Assert.Single(models).Material;

        Assert.NotNull(material);
        Assert.True(material!.HasTransmission);
        Assert.Equal(MaterialAlphaMode.Opaque, material.AlphaMode);
        Assert.False(material.IsTransparent);
    }

    [Fact]
    public void OpaqueMaterialDefaults_DoNotRegress()
    {
        var material = new Avalonia3D.Model.Material();

        Assert.Equal(MaterialAlphaMode.Opaque, material.AlphaMode);
        Assert.InRange(material.AlphaCutoff, 0.499f, 0.501f);
        Assert.False(material.DoubleSided);
        Assert.InRange(material.EmissiveIntensity, 0.999f, 1.001f);
        Assert.False(material.IsTransparent);
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

}
