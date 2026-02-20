using Avalonia3D.Sandbox.Rendering;
using Xunit;

namespace Avalonia3D.Tests;

[Trait("TestTarget", "DomainLogic")]
public class FramebufferTargetResolverTests
{
    [Fact]
    public void Resolve_WhenInputIsStable_KeepsCurrentFramebuffer()
    {
        var resolver = new FramebufferTargetResolver();

        var first = resolver.Resolve(7);
        var second = resolver.Resolve(7);

        Assert.Equal((uint)7, first);
        Assert.Equal((uint)7, second);
    }

    [Fact]
    public void Resolve_WhenSwitchBetweenOffscreenAndDefaultOscillates_KeepsStableFramebuffer()
    {
        var resolver = new FramebufferTargetResolver(switchStabilizationFrames: 2);

        var frame1 = resolver.Resolve(5);
        var frame2 = resolver.Resolve(0);
        var frame3 = resolver.Resolve(5);
        var frame4 = resolver.Resolve(0);

        Assert.Equal((uint)5, frame1);
        Assert.Equal((uint)5, frame2);
        Assert.Equal((uint)5, frame3);
        Assert.Equal((uint)5, frame4);
    }

    [Fact]
    public void Resolve_WhenSwitchToDefaultIsStable_SwitchesAfterThreshold()
    {
        var resolver = new FramebufferTargetResolver(switchStabilizationFrames: 2);

        resolver.Resolve(5);
        var beforeThreshold = resolver.Resolve(0);
        var afterThreshold = resolver.Resolve(0);

        Assert.Equal((uint)5, beforeThreshold);
        Assert.Equal((uint)0, afterThreshold);
    }

    [Fact]
    public void Resolve_WhenIncomingIsNegative_UsesStableFramebuffer()
    {
        var resolver = new FramebufferTargetResolver();

        resolver.Resolve(9);
        var output = resolver.Resolve(-1);

        Assert.Equal((uint)9, output);
    }
}
