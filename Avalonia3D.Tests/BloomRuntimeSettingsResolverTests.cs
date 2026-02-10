using Avalonia3D.Rendering;
using Xunit;

namespace Avalonia3D.Tests;

public class BloomRuntimeSettingsResolverTests
{
    [Theory]
    [InlineData(RenderQualityPreset.Low)]
    [InlineData(RenderQualityPreset.Medium)]
    [InlineData(RenderQualityPreset.High)]
    [InlineData(RenderQualityPreset.Ultra)]
    public void Resolve_DefaultMode_ReturnsStableThresholdIntensityAndContribution(RenderQualityPreset preset)
    {
        var profile = GraphicsProfile.FromPreset(preset).Validate();
        var bloom = profile.PostFx.Bloom;

        var runtime = BloomRuntimeSettingsResolver.Resolve(bloom, ShaderRenderMode.Default, profile);

        if (preset == RenderQualityPreset.Low)
        {
            Assert.Equal(bloom.Threshold, runtime.Threshold);
            Assert.Equal(bloom.Intensity, runtime.Intensity);
            Assert.Equal(0f, runtime.MinContribution);
            return;
        }

        Assert.InRange(runtime.Threshold, 0f, bloom.Threshold);
        Assert.True(runtime.Intensity >= bloom.Intensity);
        Assert.True(runtime.MinContribution > 0f);
    }

    [Fact]
    public void Resolve_DefaultMode_ProgressivelyScalesWithPresetQuality()
    {
        var medium = BloomRuntimeSettingsResolver.Resolve(GraphicsProfile.Medium.PostFx.Bloom, ShaderRenderMode.Default, GraphicsProfile.Medium.Validate());
        var high = BloomRuntimeSettingsResolver.Resolve(GraphicsProfile.High.PostFx.Bloom, ShaderRenderMode.Default, GraphicsProfile.High.Validate());
        var ultra = BloomRuntimeSettingsResolver.Resolve(GraphicsProfile.Ultra.PostFx.Bloom, ShaderRenderMode.Default, GraphicsProfile.Ultra.Validate());

        Assert.True(high.Intensity >= medium.Intensity);
        Assert.True(ultra.Intensity >= high.Intensity);

        Assert.True(high.Threshold <= medium.Threshold);
        Assert.True(ultra.Threshold <= high.Threshold);

        Assert.True(high.MinContribution >= medium.MinContribution);
        Assert.True(ultra.MinContribution >= high.MinContribution);
    }

    [Fact]
    public void Resolve_UnlitMode_UsesConfigurableBoostsFromBloomProfile()
    {
        var profile = GraphicsProfile.High.Validate();
        var bloom = profile.PostFx.Bloom with
        {
            Threshold = 0.4f,
            Intensity = 1.2f,
            EmissiveMinContribution = 0.07f,
            UnlitIntensityBoost = 3.0f
        };

        var runtime = BloomRuntimeSettingsResolver.Resolve(bloom, ShaderRenderMode.Unlit, profile);

        Assert.Equal(0.1f, runtime.Threshold, 0.001f);
        Assert.Equal(3.6f, runtime.Intensity, 0.001f);
        Assert.Equal(0.07f, runtime.MinContribution, 0.001f);
    }
}
