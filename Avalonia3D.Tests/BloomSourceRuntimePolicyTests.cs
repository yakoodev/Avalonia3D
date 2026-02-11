using Avalonia3D.Rendering;
using Xunit;

namespace Avalonia3D.Tests;

[Trait("TestTarget", "Rendering")]
public class BloomSourceRuntimePolicyTests
{
    [Fact]
    public void Resolve_EmissiveSource_AttenuatesIntensityAndOverridesMinContribution()
    {
        var runtime = new BloomRuntimeSettings(0.4f, 2.0f, 0.12f, 0.75f, 1.8f);
        var bloom = new BloomProfile
        {
            EmissivePrimaryThresholdScale = 1.35f,
            EmissivePrimaryIntensityScale = 0.5f,
            EmissivePrimaryWithColorIntensityScale = 0.8f,
            EmissivePrimaryNormalizationScale = 0.5f,
            EmissivePrimaryMinContribution = 0.0f
        };

        var resolved = BloomSourceRuntimePolicy.Resolve(runtime, bloom, "emissive+color");

        Assert.InRange(resolved.Threshold, 0.539f, 0.541f);
        Assert.InRange(resolved.Intensity, 0.79f, 0.81f);
        Assert.InRange(resolved.MinContribution, -0.0001f, 0.0001f);
        Assert.InRange(resolved.NormalizationBoost, 0.899f, 0.901f);
    }

    [Fact]
    public void Resolve_EmissiveWithoutColor_DoesNotApplyWithColorScale()
    {
        var runtime = new BloomRuntimeSettings(0.4f, 2.0f, 0.12f, 0.75f, 1.8f);
        var bloom = new BloomProfile
        {
            EmissivePrimaryThresholdScale = 1.35f,
            EmissivePrimaryIntensityScale = 0.5f,
            EmissivePrimaryWithColorIntensityScale = 0.1f,
            EmissivePrimaryNormalizationScale = 0.5f,
            EmissivePrimaryMinContribution = 0.0f
        };

        var resolved = BloomSourceRuntimePolicy.Resolve(runtime, bloom, "emissive");

        Assert.InRange(resolved.Threshold, 0.539f, 0.541f);
        Assert.InRange(resolved.Intensity, 0.99f, 1.01f);
        Assert.InRange(resolved.NormalizationBoost, 0.899f, 0.901f);
    }

    [Fact]
    public void Resolve_ColorFallback_KeepsRuntimeUnchanged()
    {
        var runtime = new BloomRuntimeSettings(0.4f, 2.0f, 0.12f, 0.75f, 1.8f);
        var bloom = new BloomProfile
        {
            EmissivePrimaryIntensityScale = 0.5f,
            EmissivePrimaryNormalizationScale = 0.5f,
            EmissivePrimaryMinContribution = 0.0f
        };

        var resolved = BloomSourceRuntimePolicy.Resolve(runtime, bloom, "color-fallback");

        Assert.Equal(runtime, resolved);
    }
}
