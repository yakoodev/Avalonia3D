using Avalonia3D.Rendering;
using System.Linq;
using Xunit;

namespace Avalonia3D.Tests;

public class RenderPipelineFactoryTests
{
    private readonly RenderPipelineFactory _factory = new();

    [Fact]
    public void CreatePasses_LowPreset_IncludesForwardAndPostEffectsOnly()
    {
        var passes = _factory.CreatePasses(RenderQualitySettings.Low);
        var names = passes.Select(p => p.Name).ToArray();

        Assert.DoesNotContain("ShadowPass", names);
        Assert.Contains("ForwardPass", names);
        Assert.Contains("PostEffectsPass", names);
    }

    [Fact]
    public void CreatePasses_HighPreset_IncludesShadowForwardAndPostEffects()
    {
        var passes = _factory.CreatePasses(RenderQualitySettings.High);
        var names = passes.Select(p => p.Name).ToArray();

        Assert.Equal(new[] { "ShadowPass", "ForwardPass", "PostEffectsPass" }, names);
    }

    [Fact]
    public void CreatePasses_NoPostEffects_DropsPostEffectsPass()
    {
        var settings = RenderQualitySettings.Medium with { PostEffects = PostEffectsFlags.None };

        var passes = _factory.CreatePasses(settings);
        var names = passes.Select(p => p.Name).ToArray();

        Assert.DoesNotContain("PostEffectsPass", names);
        Assert.Equal(new[] { "ShadowPass", "ForwardPass" }, names);
    }
}
