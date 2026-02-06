using Avalonia3D.Rendering;
using Xunit;

namespace Avalonia3D.Tests;

public class RenderQualitySettingsTests
{
    [Fact]
    public void Validate_ClampsShadowMapSizeGammaAndReflectionSettings()
    {
        var settings = new RenderQualitySettings
        {
            ShadowsEnabled = true,
            ShadowMapSize = 64,
            PostEffects = PostEffectsFlags.GammaCorrection,
            ToneMapping = ToneMappingOperator.Reinhard,
            Gamma = 5.0f,
            MsaaPolicy = MsaaPolicy.X8,
            ReflectionsEnabled = true,
            ReflectionMode = ReflectionMode.IBL,
            ReflectionIntensity = 8f,
            EnvironmentMapPath = "   "
        };

        var validated = settings.Validate();

        Assert.Equal(RenderQualitySettings.MinShadowMapSize, validated.ShadowMapSize);
        Assert.Equal(3.0f, validated.Gamma);
        Assert.Equal(ToneMappingOperator.None, validated.ToneMapping);
        Assert.Equal(2f, validated.ReflectionIntensity);
        Assert.Null(validated.EnvironmentMapPath);
    }

    [Fact]
    public void Validate_DisablesReflectionMode_WhenReflectionsDisabled()
    {
        var settings = RenderQualitySettings.Medium with
        {
            ReflectionsEnabled = false,
            ReflectionMode = ReflectionMode.Planar
        };

        var validated = settings.Validate();

        Assert.Equal(ReflectionMode.Off, validated.ReflectionMode);
    }

    [Fact]
    public void FromPreset_ReturnsExpectedDefaults()
    {
        Assert.False(RenderQualitySettings.FromPreset(RenderQualityPreset.Low).ShadowsEnabled);
        Assert.Equal(4096, RenderQualitySettings.FromPreset(RenderQualityPreset.High).ShadowMapSize);
        Assert.Equal(ReflectionMode.Off, RenderQualitySettings.FromPreset(RenderQualityPreset.Low).ReflectionMode);
        Assert.Equal(ReflectionMode.IBL, RenderQualitySettings.FromPreset(RenderQualityPreset.High).ReflectionMode);

        var custom = new RenderQualitySettings { ShadowMapSize = 777 };
        var fromCustom = RenderQualitySettings.FromPreset(RenderQualityPreset.Custom, custom);
        Assert.Equal(777, fromCustom.ShadowMapSize);
    }

    [Fact]
    public void GraphicsProfile_JsonRoundTrip_KeepsQualityData()
    {
        var profile = GraphicsProfile.High with
        {
            Name = "QA-High",
            PbrTuning = GraphicsProfile.High.PbrTuning with { Exposure = 1.35f, IblIntensity = 1.7f },
            Background = new BackgroundProfile { Red = 0.2f, Green = 0.25f, Blue = 0.3f }
        };

        var json = profile.ToJson();
        var restored = GraphicsProfile.FromJson(json);

        Assert.Equal("QA-High", restored.Name);
        Assert.Equal(RenderQualityPreset.High, restored.QualityPreset);
        Assert.Equal(1.35f, restored.PbrTuning.Exposure);
        Assert.Equal(1.7f, restored.PbrTuning.IblIntensity);
        Assert.Equal(0.2f, restored.Background.Red);
        Assert.Equal(0.25f, restored.Background.Green);
        Assert.Equal(0.3f, restored.Background.Blue);
    }
}
