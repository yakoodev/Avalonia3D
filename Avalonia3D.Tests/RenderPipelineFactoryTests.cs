using Avalonia3D.Rendering;
using System;
using System.Linq;
using Xunit;

namespace Avalonia3D.Tests;

public class RenderPipelineFactoryTests
{
    private readonly RenderPipelineFactory _factory = new();

    [Fact]
    public void CreatePasses_LowPreset_IncludesForwardAndPostEffectsOnly()
    {
        var passes = _factory.CreatePasses(GraphicsProfile.Low);
        var names = passes.Select(p => p.Name).ToArray();

        Assert.DoesNotContain("ShadowPass", names);
        Assert.DoesNotContain("EnvironmentLightingPass", names);
        Assert.DoesNotContain("BloomPass", names);
        Assert.Contains("ForwardPass", names);
        Assert.Contains("PostEffectsPass", names);
    }

    [Fact]
    public void CreatePasses_HighPreset_IncludesBloomBeforePostEffects()
    {
        var passes = _factory.CreatePasses(GraphicsProfile.High);
        var names = passes.Select(p => p.Name).ToArray();

        Assert.Equal(new[] { "ShadowPass", "EnvironmentLightingPass", "ForwardPass", "BloomPass", "PostEffectsPass" }, names);
    }

    [Theory]
    [InlineData(ReflectionMode.Off, false)]
    [InlineData(ReflectionMode.Off, true)]
    [InlineData(ReflectionMode.IBL, false)]
    public void CreatePasses_ReflectionDisabledOrOff_DropsEnvironmentPass(ReflectionMode mode, bool enabled)
    {
        var settings = GraphicsProfile.Medium with
        {
            Reflections = GraphicsProfile.Medium.Reflections with
            {
                Mode = mode,
                Enabled = enabled
            }
        };

        var passes = _factory.CreatePasses(settings);
        var names = passes.Select(p => p.Name).ToArray();

        Assert.DoesNotContain("EnvironmentLightingPass", names);
    }

    [Fact]
    public void CreatePasses_NoPostEffects_DropsPostEffectsPass()
    {
        var settings = GraphicsProfile.Medium with
        {
            PostFx = GraphicsProfile.Medium.PostFx with { Effects = PostEffectsFlags.None }
        };

        var passes = _factory.CreatePasses(settings);
        var names = passes.Select(p => p.Name).ToArray();

        Assert.DoesNotContain("PostEffectsPass", names);
        Assert.DoesNotContain("BloomPass", names);
        Assert.Equal(new[] { "ShadowPass", "EnvironmentLightingPass", "ForwardPass" }, names);
    }

    [Fact]
    public void CreatePasses_OnlyBloom_KeepsPipelineModularWithoutToneGammaPass()
    {
        var settings = GraphicsProfile.Medium with
        {
            PostFx = GraphicsProfile.Medium.PostFx with
            {
                Effects = PostEffectsFlags.Bloom,
                ToneMapping = ToneMappingOperator.None,
                Bloom = GraphicsProfile.Medium.PostFx.Bloom with { Enabled = true }
            }
        };

        var passes = _factory.CreatePasses(settings);
        var names = passes.Select(p => p.Name).ToArray();

        Assert.Contains("BloomPass", names);
        Assert.DoesNotContain("PostEffectsPass", names);
    }
    [Fact]
    public void CreatePasses_BloomEnabled_ContainsForwardBloomAndPostEffectsInOrder()
    {
        var settings = GraphicsProfile.High with
        {
            PostFx = GraphicsProfile.High.PostFx with
            {
                Effects = PostEffectsFlags.Bloom | PostEffectsFlags.ToneMapping | PostEffectsFlags.GammaCorrection,
                Bloom = GraphicsProfile.High.PostFx.Bloom with { Enabled = true, Intensity = 1.2f }
            }
        };

        var passes = _factory.CreatePasses(settings);
        var names = passes.Select(p => p.Name).ToArray();

        var forwardIndex = Array.IndexOf(names, "ForwardPass");
        var bloomIndex = Array.IndexOf(names, "BloomPass");
        var postIndex = Array.IndexOf(names, "PostEffectsPass");

        Assert.True(forwardIndex >= 0);
        Assert.True(bloomIndex > forwardIndex);
        Assert.True(postIndex > bloomIndex);
    }

}
