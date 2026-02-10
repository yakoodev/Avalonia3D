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
}
