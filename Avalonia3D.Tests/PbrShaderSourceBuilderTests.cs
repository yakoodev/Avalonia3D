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

}
