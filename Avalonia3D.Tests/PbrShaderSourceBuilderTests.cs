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

}
