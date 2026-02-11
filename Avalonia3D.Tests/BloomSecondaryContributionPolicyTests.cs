using Avalonia3D.Rendering;
using Xunit;

namespace Avalonia3D.Tests;

[Trait("TestTarget", "Rendering")]
public class BloomSecondaryContributionPolicyTests
{
    [Fact]
    public void Resolve_WithEmissivePrimary_InPbrMode_AttenuatesConfiguredContribution()
    {
        var bloom = new BloomProfile
        {
            ColorAdditiveContribution = 0.6f,
            ColorAdditiveWhenEmissivePresentScale = 0.02f
        };

        var value = BloomSecondaryContributionPolicy.Resolve(ShaderRenderMode.Pbr, bloom, hasEmissivePrimary: true);

        Assert.InRange(value, 0.011f, 0.013f);
    }

    [Fact]
    public void Resolve_WithEmissivePrimary_CapsContributionToPreventWhiteHaze()
    {
        var bloom = new BloomProfile
        {
            ColorAdditiveContribution = 0.9f,
            ColorAdditiveWhenEmissivePresentScale = 0.8f
        };

        var value = BloomSecondaryContributionPolicy.Resolve(ShaderRenderMode.Pbr, bloom, hasEmissivePrimary: true);

        Assert.InRange(value, 0.029f, 0.031f);
    }

    [Fact]
    public void Resolve_WithoutEmissivePrimary_ReturnsConfiguredContribution()
    {
        var bloom = new BloomProfile
        {
            ColorAdditiveContribution = 0.4f,
            ColorAdditiveWhenEmissivePresentScale = 0.01f
        };

        var value = BloomSecondaryContributionPolicy.Resolve(ShaderRenderMode.Pbr, bloom, hasEmissivePrimary: false);

        Assert.InRange(value, 0.399f, 0.401f);
    }
}
