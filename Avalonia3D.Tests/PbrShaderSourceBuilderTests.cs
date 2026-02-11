using Avalonia3D.Shaders;
using Xunit;

namespace Avalonia3D.Tests;

[Trait("TestTarget", "DomainLogic")]
public class PbrShaderSourceBuilderTests
{
    [Fact]
    public void Build_UsesHighPrecisionInFragmentShaderSource()
    {
        var builder = new PbrShaderSourceBuilder();

        var (_, fragmentSource) = builder.Build(PbrFeatures.None, maxLights: 4);

        Assert.Contains("precision highp float;", fragmentSource);
    }

    [Fact]
    public void Build_IncludesHdrSanitizationForOutputSafety()
    {
        var builder = new PbrShaderSourceBuilder();

        var (_, fragmentSource) = builder.Build(PbrFeatures.None, maxLights: 4);

        Assert.Contains("CompressHighlights", fragmentSource);
        Assert.Contains("SanitizeHdrColor", fragmentSource);
    }

    [Fact]
    public void Build_WithEmissiveMap_IncludesForceWhiteDebugSwitch()
    {
        var builder = new PbrShaderSourceBuilder();

        var (_, fragmentSource) = builder.Build(PbrFeatures.EmissiveMap, maxLights: 4);

        Assert.Contains("uForceWhiteEmissiveMap", fragmentSource);
        Assert.Contains("uForceWhiteEmissiveMap==1?vec3(1.0)", fragmentSource);
    }

    [Fact]
    public void Build_WithBaseColorAndEmissiveMaps_AppliesPerSemanticUvTransforms()
    {
        var builder = new PbrShaderSourceBuilder();

        var (_, fragmentSource) = builder.Build(PbrFeatures.BaseColorMap | PbrFeatures.EmissiveMap, maxLights: 4);

        Assert.Contains("uBaseColorUvOffset", fragmentSource);
        Assert.Contains("uBaseColorUvScale", fragmentSource);
        Assert.Contains("uBaseColorUvRotation", fragmentSource);
        Assert.Contains("uEmissiveUvOffset", fragmentSource);
        Assert.Contains("uEmissiveUvScale", fragmentSource);
        Assert.Contains("uEmissiveUvRotation", fragmentSource);
        Assert.Contains("ApplyManualBaseColorDecode(texture(uBaseColorMap, baseColorUv))", fragmentSource);
        Assert.Contains("ApplyManualEmissiveDecode(texture(uEmissiveMap, emissiveUv).rgb)", fragmentSource);
    }
    [Fact]
    public void Build_WithBaseColorAndEmissiveMaps_IncludesManualSrgbDecodeControls()
    {
        var builder = new PbrShaderSourceBuilder();

        var (_, fragmentSource) = builder.Build(PbrFeatures.BaseColorMap | PbrFeatures.EmissiveMap, maxLights: 4);

        Assert.Contains("uManualBaseColorSrgbDecode", fragmentSource);
        Assert.Contains("uManualEmissiveSrgbDecode", fragmentSource);
        Assert.Contains("ApplyManualBaseColorDecode", fragmentSource);
        Assert.Contains("ApplyManualEmissiveDecode", fragmentSource);
    }

    [Fact]
    public void Build_WithSpecularFeatureAndNoSpecularTextures_StillDeclaresSpecularSamples()
    {
        var builder = new PbrShaderSourceBuilder();

        var (_, fragmentSource) = builder.Build(PbrFeatures.Specular, maxLights: 4);

        Assert.Contains("float specularMapSample=1.0;", fragmentSource);
        Assert.Contains("vec3 specularColorMapSample=vec3(1.0);", fragmentSource);
        Assert.Contains("specularColor*=clamp(uSpecularFactor*specularMapSample", fragmentSource);
    }

    [Fact]
    public void Build_WithSpecularAndBaseColorMap_AppliesSpecularAfterSampleDeclarations()
    {
        var builder = new PbrShaderSourceBuilder();

        var (_, fragmentSource) = builder.Build(PbrFeatures.Specular | PbrFeatures.BaseColorMap, maxLights: 4);

        var specularMapDecl = fragmentSource.IndexOf("float specularMapSample", StringComparison.Ordinal);
        var specularColorMapDecl = fragmentSource.IndexOf("vec3 specularColorMapSample", StringComparison.Ordinal);
        var specularApply = fragmentSource.IndexOf("specularColor*=clamp(uSpecularFactor*specularMapSample", StringComparison.Ordinal);

        Assert.True(specularMapDecl >= 0);
        Assert.True(specularColorMapDecl >= 0);
        Assert.True(specularApply > specularMapDecl);
        Assert.True(specularApply > specularColorMapDecl);
    }

    [Fact]
    public void Build_WithTextureMaps_IncludesPerTextureTexCoordSetSelection()
    {
        var builder = new PbrShaderSourceBuilder();

        var (vertexSource, fragmentSource) = builder.Build(
            PbrFeatures.BaseColorMap | PbrFeatures.NormalMap | PbrFeatures.MetallicRoughnessMap | PbrFeatures.OcclusionMap | PbrFeatures.EmissiveMap,
            maxLights: 4);

        Assert.Contains("layout(location = 3) in vec2 aTexCoord1;", vertexSource);
        Assert.Contains("out vec2 TexCoord1;", vertexSource);
        Assert.Contains("SelectTexCoord", fragmentSource);
        Assert.Contains("uBaseColorTexCoordSet", fragmentSource);
        Assert.Contains("uNormalTexCoordSet", fragmentSource);
        Assert.Contains("uMetallicRoughnessTexCoordSet", fragmentSource);
        Assert.Contains("uOcclusionTexCoordSet", fragmentSource);
        Assert.Contains("uEmissiveTexCoordSet", fragmentSource);
        Assert.Contains("texture(uMetallicRoughnessMap, metallicRoughnessTexCoord)", fragmentSource);
        Assert.Contains("texture(uOcclusionMap, occlusionTexCoord)", fragmentSource);
    }

    [Fact]
    public void Build_IncludesPbrDebugViewUniformAndBranches()
    {
        var builder = new PbrShaderSourceBuilder();

        var (_, fragmentSource) = builder.Build(PbrFeatures.ReflectionsIbl, maxLights: 4);

        Assert.Contains("uPbrDebugViewMode", fragmentSource);
        Assert.Contains("debugSurfaceResult", fragmentSource);
        Assert.Contains("baseColor.rgb", fragmentSource);
        Assert.Contains("directLightComponent", fragmentSource);
        Assert.Contains("iblComponent", fragmentSource);
        Assert.Contains("vec3(ao)", fragmentSource);
        Assert.Contains("norm*0.5+0.5", fragmentSource);
    }

}
