using Avalonia3D.Rendering;
using Xunit;

namespace Avalonia3D.Tests;

public class RenderQualitySettingsTests
{
    [Fact]
    public void Validate_ClampsShadowMapSizeAndGamma_AndDisablesToneMapping_WhenFlagMissing()
    {
        var settings = new RenderQualitySettings
        {
            ShadowsEnabled = true,
            ShadowMapSize = 64,
            PostEffects = PostEffectsFlags.GammaCorrection,
            ToneMapping = ToneMappingOperator.Reinhard,
            Gamma = 5.0f,
            MsaaPolicy = MsaaPolicy.X8
        };

        var validated = settings.Validate();

        Assert.Equal(RenderQualitySettings.MinShadowMapSize, validated.ShadowMapSize);
        Assert.Equal(3.0f, validated.Gamma);
        Assert.Equal(ToneMappingOperator.None, validated.ToneMapping);
    }

    [Fact]
    public void FromPreset_ReturnsExpectedDefaults()
    {
        Assert.False(RenderQualitySettings.FromPreset(RenderQualityPreset.Low).ShadowsEnabled);
        Assert.Equal(4096, RenderQualitySettings.FromPreset(RenderQualityPreset.High).ShadowMapSize);
        var custom = new RenderQualitySettings { ShadowMapSize = 777 };
        var fromCustom = RenderQualitySettings.FromPreset(RenderQualityPreset.Custom, custom);
        Assert.Equal(777, fromCustom.ShadowMapSize);
    }
}
