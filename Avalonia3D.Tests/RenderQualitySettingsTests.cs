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
            EnvironmentMapPath = "   ",
            MaxLights = 99
        };

        var validated = settings.Validate();

        Assert.Equal(RenderQualitySettings.MinShadowMapSize, validated.ShadowMapSize);
        Assert.Equal(3.0f, validated.Gamma);
        Assert.Equal(ToneMappingOperator.None, validated.ToneMapping);
        Assert.Equal(2f, validated.ReflectionIntensity);
        Assert.Equal(GraphicsProfile.DefaultEnvironmentMapPath, validated.EnvironmentMapPath);
        Assert.Equal(RenderQualitySettings.MaxSupportedLights, validated.MaxLights);
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
        Assert.Equal(2, RenderQualitySettings.FromPreset(RenderQualityPreset.Low).MaxLights);
        Assert.Equal(8, RenderQualitySettings.FromPreset(RenderQualityPreset.High).MaxLights);
        Assert.Equal(RenderQualitySettings.MaxSupportedLights, RenderQualitySettings.FromPreset(RenderQualityPreset.Ultra).MaxLights);

        var custom = new RenderQualitySettings { ShadowMapSize = 777, MaxLights = 7 };
        var fromCustom = RenderQualitySettings.FromPreset(RenderQualityPreset.Custom, custom);
        Assert.Equal(777, fromCustom.ShadowMapSize);
        Assert.Equal(7, fromCustom.MaxLights);
    }


    [Fact]
    public void FromPreset_MediumAndHigh_HaveEnvironmentMapPath()
    {
        var medium = RenderQualitySettings.FromPreset(RenderQualityPreset.Medium);
        var high = RenderQualitySettings.FromPreset(RenderQualityPreset.High);
        var ultra = RenderQualitySettings.FromPreset(RenderQualityPreset.Ultra);

        Assert.False(string.IsNullOrWhiteSpace(medium.EnvironmentMapPath));
        Assert.False(string.IsNullOrWhiteSpace(high.EnvironmentMapPath));
        Assert.False(string.IsNullOrWhiteSpace(ultra.EnvironmentMapPath));
    }


    [Fact]
    public void FromPreset_MediumAndHigh_HaveVisibleBloomDefaultsForLdrPipeline()
    {
        var medium = GraphicsProfile.Medium;
        var high = GraphicsProfile.High;

        Assert.True(medium.PostFx.Effects.HasFlag(PostEffectsFlags.Bloom));
        Assert.True(high.PostFx.Effects.HasFlag(PostEffectsFlags.Bloom));
        Assert.True(GraphicsProfile.Ultra.PostFx.Effects.HasFlag(PostEffectsFlags.Bloom));
        Assert.True(medium.PostFx.Bloom.Threshold < 1.0f);
        Assert.True(high.PostFx.Bloom.Threshold < 1.0f);
        Assert.True(GraphicsProfile.Ultra.PostFx.Bloom.Intensity >= high.PostFx.Bloom.Intensity);
    }

    [Fact]
    public void Validate_ClampsBloomParameters()
    {
        var profile = GraphicsProfile.Medium with
        {
            PostFx = GraphicsProfile.Medium.PostFx with
            {
                Effects = PostEffectsFlags.Bloom,
                Bloom = GraphicsProfile.Medium.PostFx.Bloom with
                {
                    Enabled = true,
                    Threshold = -2f,
                    Intensity = 99f,
                    Radius = -1f,
                    Iterations = 99
                }
            }
        };

        var validated = profile.Validate();

        Assert.Equal(0f, validated.PostFx.Bloom.Threshold);
        Assert.Equal(8f, validated.PostFx.Bloom.Intensity);
        Assert.Equal(0.1f, validated.PostFx.Bloom.Radius);
        Assert.Equal(8, validated.PostFx.Bloom.Iterations);
    }


    [Fact]
    public void Presets_AreProgressiveAndBalanced()
    {
        var low = GraphicsProfile.Low;
        var medium = GraphicsProfile.Medium;
        var high = GraphicsProfile.High;
        var ultra = GraphicsProfile.Ultra;

        Assert.True(low.MaxLights < medium.MaxLights);
        Assert.True(medium.MaxLights < high.MaxLights);
        Assert.True(high.MaxLights < ultra.MaxLights);

        Assert.False(low.Shadows.Enabled);
        Assert.True(medium.Shadows.Enabled);
        Assert.True(high.Shadows.MapSize >= medium.Shadows.MapSize);
        Assert.True(ultra.Shadows.MapSize >= high.Shadows.MapSize);

        Assert.False(low.Reflections.Enabled);
        Assert.True(medium.Reflections.Enabled);
        Assert.True(high.Reflections.Intensity >= medium.Reflections.Intensity);
        Assert.True(ultra.Reflections.Intensity >= high.Reflections.Intensity);

        Assert.False(low.PostFx.Bloom.Enabled);
        Assert.True(medium.PostFx.Bloom.Enabled);
        Assert.True(high.PostFx.Bloom.Enabled);
        Assert.True(ultra.PostFx.Bloom.Enabled);

        Assert.InRange(medium.PostFx.Bloom.Threshold, 0.2f, 1.0f);
        Assert.InRange(high.PostFx.Bloom.Threshold, 0.15f, 0.8f);
        Assert.InRange(ultra.PostFx.Bloom.Threshold, 0.1f, 0.6f);

        Assert.True(ultra.PostFx.Bloom.Intensity >= high.PostFx.Bloom.Intensity);
        Assert.True(high.PostFx.Bloom.Intensity >= medium.PostFx.Bloom.Intensity);
        Assert.InRange(ultra.PostFx.Bloom.Intensity, 1.5f, 4.0f);
    }

    [Fact]
    public void GraphicsProfile_JsonRoundTrip_KeepsQualityData()
    {
        var profile = GraphicsProfile.High with
        {
            Name = "QA-High",
            PbrTuning = GraphicsProfile.High.PbrTuning with
            {
                Exposure = 1.35f,
                PbrWhitePoint = 1.5f,
                IblDiffuseIntensity = 0.33f,
                IblSpecularIntensity = 1.7f,
                ReflectionContributionClamp = 0.95f,
                AmbientStrengthClamp = 0.29f
            },
            Background = new BackgroundProfile { Red = 0.2f, Green = 0.25f, Blue = 0.3f },
            MaxLights = 6
        };

        var json = profile.ToJson();
        var restored = GraphicsProfile.FromJson(json);

        Assert.Equal("QA-High", restored.Name);
        Assert.Equal(RenderQualityPreset.High, restored.QualityPreset);
        Assert.Equal(1.35f, restored.PbrTuning.Exposure);
        Assert.Equal(1.5f, restored.PbrTuning.PbrWhitePoint);
        Assert.Equal(0.33f, restored.PbrTuning.IblDiffuseIntensity);
        Assert.Equal(1.7f, restored.PbrTuning.IblSpecularIntensity);
        Assert.Equal(0.95f, restored.PbrTuning.ReflectionContributionClamp);
        Assert.Equal(0.29f, restored.PbrTuning.AmbientStrengthClamp);
        Assert.Equal(0.2f, restored.Background.Red);
        Assert.Equal(0.25f, restored.Background.Green);
        Assert.Equal(0.3f, restored.Background.Blue);
        Assert.Equal(6, restored.MaxLights);
    }

    [Fact]
    public void PbrDebugNeutralPreset_DisablesBloomWithModerateToneMapping()
    {
        var profile = GraphicsProfile.PbrDebugNeutral.Validate();

        Assert.Equal(RenderQualityPreset.PbrDebugNeutral, profile.QualityPreset);
        Assert.False(profile.PostFx.Bloom.Enabled);
        Assert.False(profile.PostFx.Effects.HasFlag(PostEffectsFlags.Bloom));
        Assert.True(profile.PostFx.Effects.HasFlag(PostEffectsFlags.ToneMapping));
        Assert.True(profile.PostFx.Effects.HasFlag(PostEffectsFlags.GammaCorrection));
        Assert.InRange(profile.PbrTuning.Exposure, 0.9f, 1.1f);
        Assert.InRange(profile.PbrTuning.ReflectionContributionClamp, 0.5f, 1.25f);
    }
}
