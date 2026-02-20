using Avalonia3D.Sandbox.Rendering;
using Xunit;

namespace Avalonia3D.Tests;

[Trait("TestTarget", "DomainLogic")]
public class FramebufferTargetResolverTests
{
    [Fact]
    public void Resolve_WhenForceDefaultModeEnabled_AlwaysReturnsDefaultFramebuffer()
    {
        var resolver = new FramebufferTargetResolver(FramebufferResolutionMode.ForceDefault);

        var a = resolver.Resolve(7);
        var b = resolver.Resolve(0);
        var c = resolver.Resolve(-1);

        Assert.Equal((uint)0, a);
        Assert.Equal((uint)0, b);
        Assert.Equal((uint)0, c);
    }

    [Fact]
    public void Resolve_WhenInputIsStable_KeepsCurrentFramebuffer()
    {
        var resolver = new FramebufferTargetResolver();

        var first = resolver.Resolve(7);
        var second = resolver.Resolve(7);

        Assert.Equal((uint)7, first);
        Assert.Equal((uint)7, second);
    }
}
