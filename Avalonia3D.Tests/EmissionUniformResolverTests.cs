using System.Numerics;
using Avalonia3D.Interfaces;
using Avalonia3D.Loaders;
using Avalonia3D.Model;
using Avalonia3D.Model.StandObjects;
using Avalonia3D.Shaders;
using Xunit;

namespace Avalonia3D.Tests;

public class EmissionUniformResolverTests
{
    [Fact]
    public void ResolveSceneEmissionColor_WithMaterial_AndNoOverride_ReturnsZero()
    {
        var mesh = new MeshObject
        {
            EmissionColor = new Vector3(3f, 0.5f, 0.25f)
        };

        var material = new Material { EmissiveFactor = Vector3.Zero, EmissiveIntensity = 1f };

        var result = EmissionUniformResolver.ResolveSceneEmissionColor(material, mesh);

        Assert.Equal(Vector3.Zero, result);
    }

    [Fact]
    public void ResolveSceneEmissionColor_WithMaterial_AndOverride_ReturnsOverrideColor()
    {
        var obj = new AdditiveEmissionSceneObject
        {
            EmissionColor = new Vector3(1f, 2f, 3f),
            HasAdditiveSceneEmission = true,
            AdditiveSceneEmissionColor = new Vector3(7f, 8f, 9f)
        };

        var material = new Material();
        var result = EmissionUniformResolver.ResolveSceneEmissionColor(material, obj);

        Assert.Equal(new Vector3(7f, 8f, 9f), result);
    }

    [Fact]
    public void ResolveSceneEmissionColor_WithoutMaterial_UsesSceneObjectEmission()
    {
        var mesh = new MeshObject
        {
            EmissionColor = new Vector3(1f, 2f, 3f)
        };

        var result = EmissionUniformResolver.ResolveSceneEmissionColor(null, mesh);

        Assert.Equal(mesh.EmissionColor, result);
    }



    [Fact]
    public void EmissiveStrengthExtension_ProducesComparableEmissiveBrightness_ForPbrAndUnlit()
    {
        var path = Path.Combine(Path.GetTempPath(), $"emissive-strength-parity-{Guid.NewGuid():N}.gltf");
        File.WriteAllText(path, GetEmissiveStrengthGltfJson());

        var gltf = SharpGLTF.Schema2.ModelRoot.Load(path);
        var material = Assert.Single(ModelLoader.LoadModels(gltf)).Material;
        File.Delete(path);

        Assert.NotNull(material);

        var emissiveFactor = material!.EmissiveFactor;
        var pbrEmissive = emissiveFactor * MathF.Max(material.EmissiveIntensity, 0f) * MathF.Max(material.EmissiveStrength, 0f);
        var unlitEmissive = emissiveFactor * MathF.Max(material.EmissiveIntensity, 0f) * MathF.Max(material.EmissiveStrength, 0f);

        Assert.InRange(Vector3.Distance(pbrEmissive, unlitEmissive), 0f, 0.00001f);
        var (_, pbrFragmentSource) = new PbrShaderSourceBuilder().Build(PbrFeatures.EmissiveStrength, maxLights: 1);
        Assert.Contains("emissive*=max(uMaterialEmissiveStrength,0.0);", pbrFragmentSource);
        Assert.Contains("emissive *= max(uMaterialEmissiveStrength, 0.0);", UnlitShader.FragmentShaderSource);
    }

    [Fact]
    public void EmissiveTextureDebugMode_IgnoreTexture_DisablesTextureSampling()
    {
        EmissionUniformResolver.EmissiveTextureMode = EmissiveTextureDebugMode.IgnoreTexture;

        Assert.False(EmissionUniformResolver.ShouldSampleEmissiveTexture());
        Assert.False(EmissionUniformResolver.ShouldForceWhiteEmissiveTexture());

        EmissionUniformResolver.EmissiveTextureMode = EmissiveTextureDebugMode.Normal;
    }

    [Fact]
    public void EmissiveTextureDebugMode_ForceWhite_EnablesWhiteOverride()
    {
        EmissionUniformResolver.EmissiveTextureMode = EmissiveTextureDebugMode.ForceWhite;

        Assert.True(EmissionUniformResolver.ShouldSampleEmissiveTexture());
        Assert.True(EmissionUniformResolver.ShouldForceWhiteEmissiveTexture());

        EmissionUniformResolver.EmissiveTextureMode = EmissiveTextureDebugMode.Normal;
    }


    private static string GetEmissiveStrengthGltfJson() =>
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

    private sealed class AdditiveEmissionSceneObject : SceneObject, IAdditiveSceneEmissionProvider
    {
        public bool HasAdditiveSceneEmission { get; set; }
        public Vector3 AdditiveSceneEmissionColor { get; set; }
        public override void Dispose() { }
        public override void Render(IRenderContext context) { }
    }
}
