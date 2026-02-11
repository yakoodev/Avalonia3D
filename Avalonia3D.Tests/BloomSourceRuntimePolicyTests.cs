using Avalonia3D.Rendering;
using Xunit;

namespace Avalonia3D.Tests;

[Trait("TestTarget", "Rendering")]
public class BloomSourceRuntimePolicyTests
{
    [Fact]
    public void Resolve_EmissiveSource_AttenuatesIntensityAndOverridesMinContribution()
    {
        var runtime = (Threshold: 0.4f, Intensity: 2.0f, MinContribution: 0.12f);
        var bloom = new BloomProfile
        {
            EmissivePrimaryIntensityScale = 0.5f,
            EmissivePrimaryMinContribution = 0.0f
        };

        var resolved = BloomSourceRuntimePolicy.Resolve(runtime, bloom, "emissive+color");

        Assert.InRange(resolved.Intensity, 0.99f, 1.01f);
        Assert.InRange(resolved.MinContribution, -0.0001f, 0.0001f);
        Assert.Equal(runtime.Threshold, resolved.Threshold);
    }

    [Fact]
    public void Resolve_ColorFallback_KeepsRuntimeUnchanged()
    {
        var runtime = (Threshold: 0.4f, Intensity: 2.0f, MinContribution: 0.12f);
        var bloom = new BloomProfile
        {
            EmissivePrimaryIntensityScale = 0.5f,
            EmissivePrimaryMinContribution = 0.0f
        };

        var resolved = BloomSourceRuntimePolicy.Resolve(runtime, bloom, "color-fallback");

        Assert.Equal(runtime, resolved);
    }
}
