using Avalonia3D.Sandbox.Rendering;
using Xunit;

namespace Avalonia3D.Tests;

[Trait("TestTarget", "DomainLogic")]
public class FramebufferTargetResolverTests
{
    [Fact]
    public void TryResolve_WhenIncomingIsValid_UsesIncomingAndUpdatesLastValid()
    {
        var ok = FramebufferTargetResolver.TryResolve(7, 0, out var output, out var updated);

        Assert.True(ok);
        Assert.Equal((uint)7, output);
        Assert.Equal(7, updated);
    }

    [Fact]
    public void TryResolve_WhenIncomingIsNegativeAndLastValidIsDefault_UsesDefaultFramebuffer()
    {
        var ok = FramebufferTargetResolver.TryResolve(-1, 0, out var output, out var updated);

        Assert.True(ok);
        Assert.Equal((uint)0, output);
        Assert.Equal(0, updated);
    }

    [Fact]
    public void TryResolve_WhenIncomingIsNegativeAndLastValidIsNonDefault_SkipsFrame()
    {
        var ok = FramebufferTargetResolver.TryResolve(-1, 5, out var output, out var updated);

        Assert.False(ok);
        Assert.Equal((uint)0, output);
        Assert.Equal(5, updated);
    }
}
